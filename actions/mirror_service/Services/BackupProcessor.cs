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

    public async Task RunAsync(string[] args)
    {
        // Expected args: <source_folder> <backup_root_path>
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: mirror_service <source_folder> <backup_root_path>");
            return;
        }

        var sourceFolder = args[0];
        var backupFolder = args[1];

        if (!fileSystem.DirectoryExists(sourceFolder))
        {
            Console.WriteLine($"Source folder does not exist: {sourceFolder}");
            return;
        } 
        
        if (!fileSystem.DirectoryExists(backupFolder))
        {
            Console.WriteLine($"Backup folder does not exist: {sourceFolder}");
            return;
        }

        await BackupOriginalImages(sourceFolder, backupFolder);
        // await BackupPreviewImages(sourceFolder, partition, folderName);
        // await BackupDqFiles(sourceFolder, partition, folderName);
        // await BackupCommerceMarkFiles(sourceFolder, partition, folderName);
        // await BackupEmbFiles(sourceFolder, partition, folderName);
        // await BackupEng30TagsFiles(sourceFolder, partition, folderName);
        // await BackupEngShortFiles(sourceFolder, partition, folderName);
        // await BackupFvFiles(sourceFolder, partition, folderName);
    }

    private async Task BackupOriginalImages(string sourceFolder, string backupFolder)
    {
        var files = fileSystem.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            if (file.AllowImageToProcess())
            {
                var md5 = await fileHasher.ComputeMd5ForceAsync(file);
                if (await IsAlreadyCompleted(md5))
                {
                    Console.WriteLine($"File {file} already backed up. Skipping.");
                    continue;
                }

                await UpdateStatus(md5, "InProgress");
                try
                {
                    await CopyAndVerify(file, sourceFolder, backupFolder, md5);
                    await UpdateStatus(md5, "Completed");
                }
                catch (Exception ex)
                {
                    await UpdateStatus(md5, "Failed", ex.Message);
                    Console.WriteLine($"Failed to backup {file}: {ex.Message}");
                }
            }
        }
    }

  
    // private async Task BackupPreviewImages(string sourceFolder, string partition, string folderName)
    // {
    //     var previewFolder = Path.Combine(sourceFolder, "preview");
    //     if (!fileSystem.DirectoryExists(previewFolder)) return;
    //
    //     var files = fileSystem.EnumerateFiles(previewFolder, "*.jpg", SearchOption.TopDirectoryOnly);
    //     foreach (var file in files)
    //     {
    //         var md5 = await fileHasher.ComputeMd5ForceAsync(file);
    //         if (await IsAlreadyCompleted(md5))
    //         {
    //             Console.WriteLine($"File {file} already backed up. Skipping.");
    //             continue;
    //         }
    //
    //         await UpdateStatus(md5, "InProgress");
    //         try
    //         {
    //             await CopyAndVerify(file, sourceFolder, partition, folderName, md5);
    //             await UpdateStatus(md5, "Completed");
    //         }
    //         catch (Exception ex)
    //         {
    //             await UpdateStatus(md5, "Failed", ex.Message);
    //             Console.WriteLine($"Failed to backup {file}: {ex.Message}");
    //         }
    //     }
    // }
    //
    // private async Task BackupDqFiles(string sourceFolder, string partition, string folderName)
    // {
    //     await BackupSubFolder(sourceFolder, "dq", partition, folderName);
    // }
    //
    // private async Task BackupCommerceMarkFiles(string sourceFolder, string partition, string folderName)
    // {
    //     await BackupSubFolder(sourceFolder, "commerceMark", partition, folderName);
    // }
    //
    // private async Task BackupEmbFiles(string sourceFolder, string partition, string folderName)
    // {
    //     await BackupSubFolder(sourceFolder, "emb", partition, folderName);
    // }
    //
    // private async Task BackupEng30TagsFiles(string sourceFolder, string partition, string folderName)
    // {
    //     await BackupSubFolder(sourceFolder, "eng30tags", partition, folderName);
    // }
    //
    // private async Task BackupEngShortFiles(string sourceFolder, string partition, string folderName)
    // {
    //     await BackupSubFolder(sourceFolder, "engShort", partition, folderName);
    // }
    //
    // private async Task BackupFvFiles(string sourceFolder, string partition, string folderName)
    // {
    //     await BackupSubFolder(sourceFolder, "fv", partition, folderName);
    // }

    // private async Task BackupSubFolder(string sourceFolder, string subFolder, string partition, string folderName)
    // {
    //     var path = Path.Combine(sourceFolder, subFolder);
    //     if (!fileSystem.DirectoryExists(path)) return;
    //
    //     var files = fileSystem.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly);
    //     foreach (var file in files)
    //     {
    //         var md5 = await fileHasher.ComputeMd5ForceAsync(file);
    //         if (await IsAlreadyCompleted(md5))
    //         {
    //             Console.WriteLine($"File {file} already backed up. Skipping.");
    //             continue;
    //         }
    //
    //         await UpdateStatus(md5, "InProgress");
    //         try
    //         {
    //             await CopyAndVerify(file, sourceFolder, target, md5);
    //             await UpdateStatus(md5, "Completed");
    //         }
    //         catch (Exception ex)
    //         {
    //             await UpdateStatus(md5, "Failed", ex.Message);
    //             Console.WriteLine($"Failed to backup {file}: {ex.Message}");
    //         }
    //     }
    // }

    
    private async Task CopyAndVerify(string sourceFilePath, string sourceFolder, string backupFolder, string md5)
    {
        var relativeFolder = Path.GetRelativePath(sourceFolder, sourceFilePath);
        var destPath = Path.Combine(backupFolder, relativeFolder);
 
        fileSystem.CopyFile(sourceFilePath, destPath, true);

        // MD5 Verification
        var destMd5 = await fileHasher.ComputeMd5ForceAsync(destPath);

        if (md5 != destMd5)
        {
            throw new Exception($"MD5 mismatch for {sourceFilePath}. Source: {md5}, Dest: {destMd5}");
        }
    }

    private async Task<bool> IsAlreadyCompleted(string md5)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT status FROM backup_status WHERE md5_hash = @md5",
            conn);
        cmd.Parameters.AddWithValue("md5", md5);

        var status = await cmd.ExecuteScalarAsync() as string;
        return status == "Completed";
    }

    private async Task UpdateStatus(string md5, string status, string? errorMessage = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO backup_status (md5_hash, status, last_updated, error_message)
              VALUES (@md5, @status, now(), @errorMessage)
              ON CONFLICT (md5_hash) 
              DO UPDATE SET status = EXCLUDED.status, last_updated = EXCLUDED.last_updated, error_message = EXCLUDED.error_message",
            conn);
        cmd.Parameters.AddWithValue("md5", md5);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("errorMessage", (object?)errorMessage ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }
}
