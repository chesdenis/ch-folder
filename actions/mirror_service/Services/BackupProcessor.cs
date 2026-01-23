using Npgsql;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace mirror_service.Services;

public class BackupProcessor(IFileSystem fileSystem, IFileHasher fileHasher)
{
    private readonly string _connectionString = 
        string.Join(";",
            $"Host={Environment.GetEnvironmentVariable("PG_HOST")}",
            $"Port={Environment.GetEnvironmentVariable("PG_PORT")}",
            $"Database={Environment.GetEnvironmentVariable("PG_DATABASE")}",
            $"Username={Environment.GetEnvironmentVariable("PG_USERNAME")}",
            $"Password={Environment.GetEnvironmentVariable("PG_PASSWORD")}",
            "Ssl Mode=Disable",
            "Trust Server Certificate=true",
            "Include Error Detail=true"
        );
    private string? _backupRootPath;

    public async Task RunAsync(string[] args)
    {
        // Expected args: <source_folder> <backup_root_path>
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: mirror_service <source_folder> <backup_root_path>");
            return;
        }

        var sourceFolder = args[0];
        _backupRootPath = args[1];

        if (!fileSystem.DirectoryExists(sourceFolder))
        {
            Console.WriteLine($"Source folder does not exist: {sourceFolder}");
            return;
        }

        var partition = Path.GetFileName(Path.GetDirectoryName(sourceFolder.TrimEnd(Path.DirectorySeparatorChar))) ?? "unknown";
        var folderName = Path.GetFileName(sourceFolder.TrimEnd(Path.DirectorySeparatorChar)) ?? "unknown";

        Console.WriteLine($"Starting backup for partition: {partition}, folder: {folderName}");

        await ProcessActivity(partition, folderName, "OriginalImage", () => BackupOriginalImages(sourceFolder, partition, folderName));
        await ProcessActivity(partition, folderName, "PreviewImages", () => BackupPreviewImages(sourceFolder, partition, folderName));
        await ProcessActivity(partition, folderName, "DescriptionQueries", () => BackupDqFiles(sourceFolder, partition, folderName));
        await ProcessActivity(partition, folderName, "CommercialMark", () => BackupCommerceMarkFiles(sourceFolder, partition, folderName));
        await ProcessActivity(partition, folderName, "Emb", () => BackupEmbFiles(sourceFolder, partition, folderName));
        await ProcessActivity(partition, folderName, "Eng30Tags", () => BackupEng30TagsFiles(sourceFolder, partition, folderName));
        await ProcessActivity(partition, folderName, "EngShort", () => BackupEngShortFiles(sourceFolder, partition, folderName));
        await ProcessActivity(partition, folderName, "Fv", () => BackupFvFiles(sourceFolder, partition, folderName));
    }

    private async Task ProcessActivity(string partition, string folder, string activity, Func<Task> action)
    {
        if (await IsAlreadyCompleted(partition, folder, activity))
        {
            Console.WriteLine($"Activity {activity} already completed for {partition}/{folder}. Skipping.");
            return;
        }

        await UpdateStatus(partition, folder, activity, "InProgress");
        try
        {
            await action();
            await UpdateStatus(partition, folder, activity, "Completed");
            Console.WriteLine($"Activity {activity} completed for {partition}/{folder}.");
        }
        catch (Exception ex)
        {
            await UpdateStatus(partition, folder, activity, "Failed", ex.Message);
            Console.WriteLine($"Activity {activity} failed for {partition}/{folder}: {ex.Message}");
        }
    }

    private async Task BackupOriginalImages(string sourceFolder, string partition, string folderName)
    {
        var files = fileSystem.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            if (file.AllowImageToProcess())
            {
                await CopyAndVerify(file, sourceFolder, partition, folderName);
            }
        }
    }

    private async Task BackupPreviewImages(string sourceFolder, string partition, string folderName)
    {
        var previewFolder = Path.Combine(sourceFolder, "preview");
        if (!fileSystem.DirectoryExists(previewFolder)) return;

        var files = fileSystem.EnumerateFiles(previewFolder, "*.jpg", SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            await CopyAndVerify(file, sourceFolder, partition, folderName);
        }
    }

    private async Task BackupDqFiles(string sourceFolder, string partition, string folderName)
    {
        await BackupSubFolder(sourceFolder, "dq", partition, folderName);
    }

    private async Task BackupCommerceMarkFiles(string sourceFolder, string partition, string folderName)
    {
        await BackupSubFolder(sourceFolder, "commerceMark", partition, folderName);
    }

    private async Task BackupEmbFiles(string sourceFolder, string partition, string folderName)
    {
        await BackupSubFolder(sourceFolder, "emb", partition, folderName);
    }

    private async Task BackupEng30TagsFiles(string sourceFolder, string partition, string folderName)
    {
        await BackupSubFolder(sourceFolder, "eng30tags", partition, folderName);
    }

    private async Task BackupEngShortFiles(string sourceFolder, string partition, string folderName)
    {
        await BackupSubFolder(sourceFolder, "engShort", partition, folderName);
    }

    private async Task BackupFvFiles(string sourceFolder, string partition, string folderName)
    {
        await BackupSubFolder(sourceFolder, "fv", partition, folderName);
    }

    private async Task BackupSubFolder(string sourceFolder, string subFolder, string partition, string folderName)
    {
        var path = Path.Combine(sourceFolder, subFolder);
        if (!fileSystem.DirectoryExists(path)) return;

        var files = fileSystem.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            await CopyAndVerify(file, sourceFolder, partition, folderName);
        }
    }

    private async Task CopyAndVerify(string sourceFilePath, string sourceFolder, string partition, string folderName)
    {
        var relativePath = sourceFilePath.Substring(sourceFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var destFilePath = Path.Combine(_backupRootPath!, partition, folderName, relativePath);

        var destDir = Path.GetDirectoryName(destFilePath);
        if (destDir != null && !fileSystem.DirectoryExists(destDir))
        {
            fileSystem.CreateDirectory(destDir);
        }

        fileSystem.CopyFile(sourceFilePath, destFilePath, true);

        // MD5 Verification
        var sourceMd5 = await fileHasher.ComputeMd5ForceAsync(sourceFilePath);
        var destMd5 = await fileHasher.ComputeMd5ForceAsync(destFilePath);

        if (sourceMd5 != destMd5)
        {
            throw new Exception($"MD5 mismatch for {sourceFilePath}. Source: {sourceMd5}, Dest: {destMd5}");
        }
    }

    private async Task<bool> IsAlreadyCompleted(string partition, string folder, string activity)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT status FROM backup_status WHERE partition = @partition AND folder = @folder AND activity_kind = @activity",
            conn);
        cmd.Parameters.AddWithValue("partition", partition);
        cmd.Parameters.AddWithValue("folder", folder);
        cmd.Parameters.AddWithValue("activity", activity);

        var status = await cmd.ExecuteScalarAsync() as string;
        return status == "Completed";
    }

    private async Task UpdateStatus(string partition, string folder, string activity, string status, string? errorMessage = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO backup_status (partition, folder, activity_kind, status, last_updated, error_message)
              VALUES (@partition, @folder, @activity, @status, now(), @errorMessage)
              ON CONFLICT (partition, folder, activity_kind) 
              DO UPDATE SET status = EXCLUDED.status, last_updated = EXCLUDED.last_updated, error_message = EXCLUDED.error_message",
            conn);
        cmd.Parameters.AddWithValue("partition", partition);
        cmd.Parameters.AddWithValue("folder", folder);
        cmd.Parameters.AddWithValue("activity", activity);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("errorMessage", (object?)errorMessage ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }
}
