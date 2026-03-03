using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace uploader;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Hash Storage Builder...");

        // 2. Load Configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        // Target path is either from args[0] or default to the current SSD (where the app runs)
        // Since it's expected to run on the 2TB SSD, let's assume the SSD root or a 'Storage' folder on it.
        string targetPath = (config["Storage:TargetPath"] ?? throw new ArgumentOutOfRangeException("Storage:TargetPath"));
        string sourcePath = (config["Storage:RootPath"] ?? throw new ArgumentOutOfRangeException("Storage:RootPath"));
        string contentApi =
            (config["Storage:ContentApi"] ?? throw new ArgumentOutOfRangeException("Storage:ContentApi"));
        
        Console.WriteLine($"Source Path: {sourcePath}");
        Console.WriteLine($"Target Path: {targetPath}");

        // 3. Load Processed Hashes
        string processedHashesPath = Path.Combine(targetPath, "processed_hashes.json");
        Directory.CreateDirectory(targetPath);
        if (!File.Exists(processedHashesPath))
        {
            File.WriteAllText(processedHashesPath, "[]");
        }

        var hashesJson = File.ReadAllText(processedHashesPath);
        HashSet<string> processedHashes = JsonSerializer.Deserialize<HashSet<string>>(hashesJson) ?? new();
        Console.WriteLine($"Loaded {processedHashes.Count} processed hashes.");

        // 4. Processing Loop
        var folders = StorageFolderExtensions.GetStorageFolders(sourcePath).ToList();
        Console.WriteLine($"Found {folders.Count} folders to process.");

        int count = 0;
        int skipped = 0;
        int errors = 0;

        foreach (var filePath in StorageFolderExtensions.GetFilesInFolder(sourcePath, folders))
        {
            try
            {
                // a. Calculate MD5
                var md5 = await filePath.CalculateMd5Async();
                
                // b. Skip if already processed
                if (processedHashes.Contains(md5))
                {
                    skipped++;
                    continue;
                }

                // d. Collect Metadata
                var metadata = await UploaderExtensions.CollectMetadataAsync(filePath, sourcePath);
                metadata["md5"] = md5;
                metadata["ext"] = Path.GetExtension(filePath);

                // e. Prepare Target Path (/ab/cd/md5)
                string targetDir = targetPath.GetTargetPartitionDir(md5);
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                string targetFilePath = Path.Combine(targetDir, md5);
                string targetMetaPath = Path.Combine(targetDir, md5 + ".json");

                // g. Copy File Atomically
                string tempFilePath = targetFilePath + ".tmp";
                File.Copy(filePath, tempFilePath, true);
                if (File.Exists(targetFilePath)) File.Delete(targetFilePath);
                File.Move(tempFilePath, targetFilePath);

                // h. Write Metadata
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var metaJson = JsonSerializer.Serialize(metadata, jsonOptions);
                await File.WriteAllTextAsync(targetMetaPath, metaJson);

                // i. Update Processed Hashes
                processedHashes.Add(md5);
                count++;

                if (count % 10 == 0)
                {
                    Console.WriteLine($"Processed {count} files... (Skipped: {skipped}, Errors: {errors})");
                    await File.WriteAllTextAsync(processedHashesPath, JsonSerializer.Serialize(processedHashes));
                }
            }
            catch (Exception ex)
            {
                errors++;
                Console.WriteLine($"Error processing {filePath}: {ex.Message}");
            }
        }

        // 5. Finalize
        await File.WriteAllTextAsync(processedHashesPath, JsonSerializer.Serialize(processedHashes));
        Console.WriteLine($"Finished. Processed: {count}, Skipped: {skipped}, Errors: {errors}.");
    }
}