using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;
using webapp.Models;

namespace webapp.Services;

public interface IImageLocator
{
    public IDictionary<string, string> GetAllLocations();
    Task<int> IdentifyImageLocations(CancellationToken ct = default);
    public Task<ImageLinks?> GetImageLinksAsync(string md5);
    IEnumerable<string> GetAvailableFolders();
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
    
    public IDictionary<string, string> GetAllLocations() => _imageLocationsMap;

    private static (int width, int height)? TryReadJpegSize(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            // Check SOI marker
            if (br.ReadByte() != 0xFF || br.ReadByte() != 0xD8) return null;

            while (fs.Position < fs.Length)
            {
                // Find marker 0xFF
                byte b = br.ReadByte();
                if (b != 0xFF) continue;
                // Skip fill bytes 0xFF
                byte marker = br.ReadByte();
                while (marker == 0xFF) marker = br.ReadByte();

                // Markers without length
                if (marker == 0xD9 || marker == 0xDA) // EOI or SOS (start of scan)
                    break;

                // Read segment length
                ushort len = (ushort)((br.ReadByte() << 8) | br.ReadByte());
                if (len < 2) return null;

                // SOF0..SOF3, SOF5..SOF7, SOF9..SOF11, SOF13..SOF15 carry size
                if ((marker >= 0xC0 && marker <= 0xC3) ||
                    (marker >= 0xC5 && marker <= 0xC7) ||
                    (marker >= 0xC9 && marker <= 0xCB) ||
                    (marker >= 0xCD && marker <= 0xCF))
                {
                    // precision (1 byte) then height (2), width (2)
                    br.ReadByte();
                    int height = (br.ReadByte() << 8) | br.ReadByte();
                    int width = (br.ReadByte() << 8) | br.ReadByte();
                    return (width, height);
                }

                // Skip this segment (length includes the 2 length bytes already read)
                fs.Position += len - 2;
            }
        }
        catch
        {
            // ignore and fall back
        }

        return null;
    }

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

    public IEnumerable<string> GetAvailableFolders()
    {
        return _imageLocationsMap.Values
            .Select(Path.GetDirectoryName)
            .Where(d => !string.IsNullOrEmpty(d))
            .Select(d => Path.GetFileName(d))
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .OrderBy(n => n)!;
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

    public async Task<int> IdentifyImageLocations(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _imageLocationsMap.Clear();

        var root = _storage.RootPath;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            logger.LogWarning("PhotoLocator: Storage.RootPath is not configured or does not exist: {Root}", root);
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
                    logger.LogInformation("PhotoLocator: processed {Count} files", totalProcessed);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "PhotoLocator: failed to process file {File}", filePath);
            }
        }
        
        // Persist image locations into Postgres so other services can use them
        try
        {
            await imageLocationRepository.UpsertLocationsAsync(_imageLocationsMap, ct);
            logger.LogInformation("PhotoLocator: uploaded {Count} image locations to DB", _imageLocationsMap.Count);
            
            // Cleanup dead links
            await imageLocationRepository.DeleteMissingLocationsAsync(_imageLocationsMap.Keys, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PhotoLocator: failed to update image locations in DB");
        }
        
        sw.Stop();
        logger.LogInformation(
            "PhotoLocator: indexed {Count} files from {Root} in {ElapsedMs} ms ({Elapsed})",
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