using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using shared_csharp.Extensions;
using webapp.Models;

namespace webapp.Services;

public interface IImageLocator
{
    Task<int> IdentifyImageLocations(CancellationToken ct = default);
    public ImageLinks? GetImageLinks(string md5);
}

public sealed class ImageLocator(IOptions<StorageOptions> storage, ILogger<ImageLocator> logger)
    : IImageLocator
{
    private readonly StorageOptions _storage = storage.Value;
    private readonly ConcurrentDictionary<string, string> _imageLocationsMap = new();

    public ImageLinks? GetImageLinks(string md5)
    {
        if (string.IsNullOrWhiteSpace(md5)) return null;
        if (!_imageLocationsMap.TryGetValue(md5, out var path)) return null;

        return new ImageLinks
        {
            Md5 = md5,
            Real = path,
            P16 = path.GetPreview16Path(),
            P32 = path.GetPreview32Path(),
            P64 = path.GetPreview64Path(),
            P128 = path.GetPreview128Path(),
            P512 = path.GetPreview512Path(),
            P2000 = path.GetPreview2000Path()
        };
    }

    public async Task<int> IdentifyImageLocations(CancellationToken ct = default)
    {
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
        
        logger.LogInformation("PhotoLocator: indexed {Count} files from {Root}", totalProcessed, root);
        return totalProcessed;
    }
}

public record ImageLinks
{
    public required string Md5 { get; init; }
    public required string Real { get; init; }
    public required string P16 { get; init; }
    public required string P32 { get; init; }
    public required string P64 { get; init; }
    public required string P128 { get; init; }
    public required string P512 { get; init; }
    public required string P2000 { get; init; }
}