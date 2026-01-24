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
        
        await BackupFolder(sourceFolder, backupFolder);
        
    }

    private async Task BackupFolder(string sourceFolder, string backupFolder)
    {
        var files = fileSystem.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly);
        
        Directory.CreateDirectory(backupFolder);
        Directory.CreateDirectory(Path.Combine(backupFolder, "preview"));
        Directory.CreateDirectory(Path.Combine(backupFolder, "dq"));
        Directory.CreateDirectory(Path.Combine(backupFolder, "emb"));
        Directory.CreateDirectory(Path.Combine(backupFolder, "fv"));
        Directory.CreateDirectory(Path.Combine(backupFolder, "commerceMark"));
        Directory.CreateDirectory(Path.Combine(backupFolder, "engShort"));
        Directory.CreateDirectory(Path.Combine(backupFolder, "eng30tags"));
         
        foreach (var file in files)
        {
            if (!file.AllowImageToProcess()) continue;
            
            var md5 = await fileHasher.ComputeMd5Async(file);
            if (await IsAlreadyCompleted(md5))
            {
                Console.WriteLine($"File {file} already backed up. Skipping.");
                continue;
            }

            await UpdateStatus(md5, "InProgress");
            try
            {
                await CopyAndVerify(file, sourceFolder, backupFolder);
                    
                await CopyAndVerify(file.GetPreview16Path(), sourceFolder, backupFolder);
                await CopyAndVerify(file.GetPreview32Path(), sourceFolder, backupFolder);
                await CopyAndVerify(file.GetPreview64Path(), sourceFolder, backupFolder);
                await CopyAndVerify(file.GetPreview128Path(), sourceFolder, backupFolder);
                await CopyAndVerify(file.GetPreview512Path(), sourceFolder, backupFolder);
                await CopyAndVerify(file.GetPreview2000Path(), sourceFolder, backupFolder);
                
                await CopyAndVerify(PathExtensions.ResolveDqQuestionPath(file), sourceFolder, backupFolder);
                await CopyAndVerify(PathExtensions.ResolveDqAnswerPath(file), sourceFolder, backupFolder);
                await CopyAndVerify(PathExtensions.ResolveDqConversationPath(file), sourceFolder, backupFolder);
                
                await CopyAndVerify(PathExtensions.ResolveEmbAnswer(file), sourceFolder, backupFolder);
                
                await CopyAndVerify(PathExtensions.ResolveFvAnswer(file), sourceFolder, backupFolder, byPass:true);
                
                await CopyAndVerify(PathExtensions.ResolveCommerceMarkQuestionPath(file), sourceFolder, backupFolder);
                await CopyAndVerify(PathExtensions.ResolveCommerceMarkAnswerPath(file), sourceFolder, backupFolder);
                await CopyAndVerify(PathExtensions.ResolveCommerceMarkConversationPath(file), sourceFolder, backupFolder);
                
                await CopyAndVerify(PathExtensions.ResolveEngShortQuestionPath(file), sourceFolder, backupFolder);
                await CopyAndVerify(PathExtensions.ResolveEngShortAnswerPath(file), sourceFolder, backupFolder);
                await CopyAndVerify(PathExtensions.ResolveEngShortConversationPath(file), sourceFolder, backupFolder);
                
                await CopyAndVerify(PathExtensions.ResolveEng30TagsQuestionPath(file), sourceFolder, backupFolder);
                await CopyAndVerify(PathExtensions.ResolveEng30TagsAnswerPath(file), sourceFolder, backupFolder);
                await CopyAndVerify(PathExtensions.ResolveEng30TagsConversationPath(file), sourceFolder, backupFolder);
                 
                await UpdateStatus(md5, "Completed");
            }
            catch (Exception ex)
            {
                await UpdateStatus(md5, "Failed", ex.Message);
                Console.WriteLine($"Failed to backup {file}: {ex.Message}");
            }
        }
    }
 
    private async Task CopyAndVerify(string sourceFilePath, string sourceFolder, string backupFolder, bool byPass = false)
    {
        try
        {
            var relativeFolder = Path.GetRelativePath(sourceFolder, sourceFilePath);
            var destPath = Path.Combine(backupFolder, relativeFolder);

            fileSystem.CopyFile(sourceFilePath, destPath, true);

            // MD5 Verification
            var srcMd5 = await fileHasher.ComputeMd5ForceAsync(sourceFilePath);
            var destMd5 = await fileHasher.ComputeMd5ForceAsync(destPath);

            if (srcMd5 != destMd5)
            {
                throw new Exception($"MD5 mismatch for {sourceFilePath}. Source: {srcMd5}, Dest: {destMd5}");
            }
        }
        catch (Exception e)
        {
            if (!byPass)
            {
                Console.WriteLine(e);
                throw;
            }
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
