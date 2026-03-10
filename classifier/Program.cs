using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Linq;
using System.IO;
using System.Text;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var sourceFolder = configuration["SourceFolder"] ?? throw new InvalidOperationException("SourceFolder not specified in appsettings.json");
var duplicatesFolder = configuration["DuplicatesFolder"] ?? throw new InvalidOperationException("DuplicatesFolder not specified in appsettings.json");
 
if (!Directory.Exists(sourceFolder))
{
    Console.WriteLine($"Source folder does not exist: {sourceFolder}");
    return;
}

if (!Directory.Exists(duplicatesFolder))
{
    Directory.CreateDirectory(duplicatesFolder);
}

var connectionString = configuration.GetConnectionString("PgPhMetaDb") 
    ?? throw new InvalidOperationException("Connection string 'PgPhMetaDb' not found in appsettings.json");

var existingHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

Console.WriteLine("Loading existing MD5 hashes from database...");
try
{
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand("SELECT md5_hash FROM registered_objects", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        existingHashes.Add(reader.GetString(0));
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error loading MD5 hashes: {ex.Message}");
    return;
}
Console.WriteLine($"Loaded {existingHashes.Count} hashes.");

int duplicateCount = 0;
string? currentTargetFolder = null;
int filesInCurrentFolder = 0;

Console.WriteLine("Scanning for duplicates...");

foreach (var filePath in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
{
    try
    {
        var md5 = await CalculateFileMd5Async(filePath);
        if (existingHashes.Contains(md5))
        {
            if (currentTargetFolder == null || filesInCurrentFolder >= 100)
            {
                currentTargetFolder = Path.Combine(duplicatesFolder, Guid.NewGuid().ToString());
                Directory.CreateDirectory(currentTargetFolder);
                filesInCurrentFolder = 0;
            }

            var targetFolder = currentTargetFolder;
            var fileName = Path.GetFileName(filePath);
            var targetPath = Path.Combine(targetFolder, fileName);
            
            // Handle name collision in target folder
            if (File.Exists(targetPath))
            {
                var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                targetPath = Path.Combine(targetFolder, $"{nameWithoutExtension}_{Guid.NewGuid().ToString().Substring(0, 8)}{extension}");
            }

            File.Move(filePath, targetPath);
            duplicateCount++;
            filesInCurrentFolder++;
            Console.WriteLine($"[{duplicateCount}] Moved duplicate: {filePath} -> {targetPath}");
        }
         
        
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
    }
}

Console.WriteLine($"Finished. Moved {duplicateCount} duplicates.");

async Task<string> CalculateFileMd5Async(string filePath)
{
    using var md5 = MD5.Create();
    await using var stream = File.OpenRead(filePath);
    var hashBytes = await md5.ComputeHashAsync(stream);
    
    var sb = new StringBuilder();
    foreach (var b in hashBytes)
    {
        sb.Append(b.ToString("x2"));
    }
    return sb.ToString();
}
