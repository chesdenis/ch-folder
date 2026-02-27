using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;
using webapp.Models;

namespace webapp.Services;

public interface IImageLocator
{
    Task<int> FileLocations(CancellationToken ct = default);
    public Task<ImageLinks?> GetImageLinksAsync(string md5);
    IDictionary<string, List<string>> GetAvailableFoldersHierarchical();
}

public sealed class ImageLocator(
    IOptions<StorageOptions> storage,
    ILogger<ImageLocator> logger,
    IImageLocationRepository imageLocationRepository,
    IFileSystem fileSystem)
    : IImageLocator
{
    private readonly StorageOptions _storage = storage.Value;
    private readonly ConcurrentDictionary<string, string> _imageLocationsMap = new();
    
    public async Task<ImageLinks?> GetImageLinksAsync(string md5)
    {
        if (string.IsNullOrWhiteSpace(md5)) return null;
        if (!_imageLocationsMap.TryGetValue(md5, out var path)) return null;

        var metadata = await fileSystem.GetMetadata(path);

        var links = new ImageLinks
        {
            Md5 = md5,
            Real = path,
            Previews = metadata?.Previews,
            P2000Width = 2000, // Fallback or could be parsed from metadata if available
            P2000Height = 1500
        };
        return links;
    }

    public IDictionary<string, List<string>> GetAvailableFoldersHierarchical()
    {
        var result = new Dictionary<string, List<string>>();
        var root = _storage.RootPath;
        if (string.IsNullOrWhiteSpace(root)) return result;

        foreach (var path in _imageLocationsMap.Values)
        {
            var directoryPath = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directoryPath)) continue;

            var relativePath = Path.GetRelativePath(root, directoryPath);
            if (string.IsNullOrEmpty(relativePath) || relativePath == ".") continue;

            var parts = relativePath.Split(Path.DirectorySeparatorChar);
            if (parts.Length < 1) continue;

            var level1 = parts[0];
            var level2 = parts.Length > 1 ? parts[1] : null;

            if (!result.ContainsKey(level1))
            {
                result[level1] = new List<string>();
            }

            if (level2 != null && !result[level1].Contains(level2))
            {
                result[level1].Add(level2);
            }
        }

        foreach (var key in result.Keys)
        {
            result[key].Sort();
        }

        return result;
    }

    public async Task<int> FileLocations(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _imageLocationsMap.Clear();

        var root = _storage.RootPath;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            logger.LogWarning("Storage.RootPath is not configured or does not exist: {Root}", root);
            return 0;
        }

        var totalProcessed = 0;
        
        var storageFolders = PathExtensions.GetStorageFolders(root);
        var files = PathExtensions.GetFilesInFolder(root, storageFolders);
        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var md5 = await filePath.CalculateMd5Async();
                // last write wins if duplicates found
                _imageLocationsMap[md5] = filePath;
                totalProcessed++;

                if (totalProcessed % 10000 == 0)
                {
                    logger.LogInformation("processed {Count} files", totalProcessed);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "failed to process file {File}", filePath);
            }
        }
        
        // Persist image locations into Postgres so other services can use them
        try
        {
            await imageLocationRepository.UpsertLocationsAsync(_imageLocationsMap, ct);
            logger.LogInformation("uploaded {Count} image locations to DB", _imageLocationsMap.Count);
            
            // Cleanup dead links
            await imageLocationRepository.DeleteMissingLocationsAsync(_imageLocationsMap.Keys, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "failed to update image locations in DB");
        }
        
        sw.Stop();
        logger.LogInformation(
            "indexed {Count} files from {Root} in {ElapsedMs} ms ({Elapsed})",
            totalProcessed,
            root,
            sw.ElapsedMilliseconds,
            sw.Elapsed
        );
        
        return totalProcessed;
    }
}

public record ImageLinks
{
    public required string Md5 { get; init; }
    public required string Real { get; init; }
    public IReadOnlyDictionary<string, string>? Previews { get; init; }
    // Optional dimensions of the 2000px preview (read from file header if available)
    public int? P2000Width { get; init; }
    public int? P2000Height { get; init; }
}