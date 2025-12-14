using Npgsql;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace duplicate_marker;

public class DuplicateMarker(IFileSystem fileSystem)
{
    private readonly string _connectionString = string.Join(";",
        $"Host={Environment.GetEnvironmentVariable("PG_HOST")}",
        $"Port={Environment.GetEnvironmentVariable("PG_PORT")}",
        $"Database={Environment.GetEnvironmentVariable("PG_DATABASE")}",
        $"Username={Environment.GetEnvironmentVariable("PG_USERNAME")}",
        $"Password={Environment.GetEnvironmentVariable("PG_PASSWORD")}",
        "Ssl Mode=Disable",
        "Trust Server Certificate=true",
        "Include Error Detail=true"
    ) ?? throw new ArgumentNullException(nameof(_connectionString));

    public async Task RunAsync(string[] args)
    {
        args = args.ValidateArgs();
        // Preload all md5 -> real_path into memory to avoid per-file DB queries
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT md5_hash, real_path FROM image_location", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var md5 = reader.GetString(0);
                var real = reader.GetString(1);
                // last value wins if duplicates somehow exist
                map[md5] = real;
            }
        }

        async Task Handler(string filePath) => await ProcessSingleFile(filePath, map);

        await fileSystem.WalkThrough(args, Handler);
    }

    private async Task ProcessSingleFile(string filePath, IReadOnlyDictionary<string, string> md5ToRealPath)
    {
        if (!filePath.AllowImageToProcess())
        {
            return;
        }

        // If file doesn't exist, skip moving entirely
        if (!File.Exists(filePath))
        {
            return;
        }

        var md5 = await filePath.CalculateMd5Async();

        // Check presence in preloaded map
        if (!md5ToRealPath.TryGetValue(md5, out var realPath) || string.IsNullOrWhiteSpace(realPath))
        {
            // not found in DB, nothing to move
            Console.WriteLine($"No duplicate found for '{filePath}'");
        }

        // Destination folder name is the directory name of real_path
        var dirOfReal = Path.GetDirectoryName(realPath);
        if (string.IsNullOrEmpty(dirOfReal))
        {
            Console.WriteLine($"Invalid real_path '{realPath}' for '{filePath}'");
        }

        var destinationFolderName = Path.GetFileName(dirOfReal);
        if (string.IsNullOrEmpty(destinationFolderName))
        {
            Console.WriteLine($"Invalid real_path '{realPath}' for '{filePath}'");
        }

        var sourceDir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(sourceDir))
        {
            Console.WriteLine($"Invalid file path '{filePath}'");
        }

        var destinationDir = Path.Combine(sourceDir, destinationFolderName);
        Directory.CreateDirectory(destinationDir);

        var fileName = Path.GetFileName(filePath);
        var destPath = Path.Combine(destinationDir, fileName);

        // Handle name collision by appending a numeric suffix
        if (File.Exists(destPath))
        {
            Console.WriteLine($"File '{destPath}' already exists, appending numeric suffix");
            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            int i = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(destinationDir, $"{name} ({i}){ext}");
                i++;
            } while (File.Exists(candidate));
            destPath = candidate;
            
            Console.WriteLine($"Final destination path: '{destPath}'");
        }

        try
        {
            fileSystem.MoveFile(filePath, destPath);
            Console.WriteLine($"Moved '{filePath}' => '{destPath}' (duplicate md5: {md5})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to move '{filePath}' to '{destPath}': {ex.Message}");
        }
    }
}