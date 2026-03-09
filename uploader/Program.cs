using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace uploader;

class Program
{
    private static readonly HttpClient http = new HttpClient();
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
        Directory.CreateDirectory(targetPath);

        // 4. Processing Loop
        var folders = StorageFolderExtensions.GetStorageFolders(sourcePath).ToList();
        Console.WriteLine($"Found {folders.Count} folders to process.");

        int count = 0;
        int errors = 0;

        foreach (var filePath in StorageFolderExtensions.GetFilesInFolder(sourcePath, folders))
        {
            if (filePath.IndexOf("System Volume Information") > 0)
            {
                continue;
            }

            if (filePath.EndsWith(".DS_Store"))
            {
                continue;
            }
            
            try
            {
                if (!File.Exists(filePath)) { continue; }
                
                // a. Calculate MD5
                var md5 = await filePath.CalculateMd5Async(force:true);
                
                var existsRemote = await UploaderExtensions.RemoteExistsAsync(http, contentApi, md5).ConfigureAwait(false);
                if (existsRemote is null)
                {
                    // safest: skip if API is down (don’t delete/move!)
                    Console.WriteLine($"[skip] API error for {filePath}");
                    continue;
                }
                if (existsRemote.Value)
                {
                    var dupesDir = targetPath.GetTargetPartitionDirDup(md5);
                    Directory.CreateDirectory(dupesDir);

                    var dest = Path.Combine(dupesDir, md5);

                    // if duplicate already stored → discard
                    if (File.Exists(dest))
                    {
                        Console.WriteLine($"Moving {filePath} -> {filePath + ".processed"}");
                        File.Move(filePath, filePath + ".processed");
                        continue;
                    }
                    Console.WriteLine($"Moving {filePath} -> {dest}");
                    File.Move(filePath, dest);
                    continue;
                }
                
                // d. Collect Metadata
                var metadata = await UploaderExtensions.CollectMetadataAsync(filePath, sourcePath);
                metadata["md5"] = md5;
                metadata["ext"] = Path.GetExtension(filePath);
                metadata["original_name"] = Path.GetFileNameWithoutExtension(filePath);

                // e. Prepare Target Path (/ab/cd/md5)
                string targetDir = targetPath.GetTargetPartitionDir(md5);
                if (!Directory.Exists(targetDir))
                {
                    Console.WriteLine($"CreateDirectory {targetDir}");
                    Directory.CreateDirectory(targetDir);
                }

                string targetFilePath = Path.Combine(targetDir, md5);
                string targetMetaPath = Path.Combine(targetDir, md5 + ".json");

                // g. Rename file atomically
                Console.WriteLine($"Moving {filePath} -> {targetFilePath}");
                if (File.Exists(targetFilePath))
                {
                    var existedMd5 = await targetFilePath.CalculateMd5Async(force:true);
                    var candidateMd5 = await filePath.CalculateMd5Async(force:true);

                    if (!existedMd5.Equals(candidateMd5, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception($"File already exists with different MD5: {filePath} -> {targetFilePath}");
                    }

                    // dont need to move - just delete
                    File.Delete(filePath);
                    continue;
                }

                File.Move(filePath, targetFilePath);

                // h. Write Metadata
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var metaJson = JsonSerializer.Serialize(metadata, jsonOptions);
                Console.WriteLine($"Writing {targetMetaPath} -> {metaJson.Length}");
                await File.WriteAllTextAsync(targetMetaPath, metaJson);

                count++;

                if (count % 10 == 0)
                {
                    Console.WriteLine($"Processed {count} files... (Errors: {errors})");
                }
            }
            catch (Exception ex)
            {
                errors++;
                Console.WriteLine($"Error processing {filePath}: {ex.Message}");
            }
        }

        // 5. Finalize
        Console.WriteLine($"Finished. Processed: {count}, Errors: {errors}.");
    }
}