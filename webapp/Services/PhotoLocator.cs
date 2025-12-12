using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using shared_csharp.Extensions;
using webapp.Models;

namespace webapp.Services;

public interface IPhotoLocator
{
    Task<int> BuildIndexAsync(CancellationToken ct = default);
    public PhotoContent? GetPhotoContent(string md5);
    int Count { get; }
    IReadOnlyDictionary<string, string> Snapshot();
}

public sealed class PhotoLocator : IPhotoLocator
{
    private readonly ILogger<PhotoLocator> _logger;
    private readonly StorageOptions _storage;
    private readonly ConcurrentDictionary<string, string> _md5ToPath = new();

    public PhotoLocator(IOptions<StorageOptions> storage, ILogger<PhotoLocator> logger)
    {
        _logger = logger;
        _storage = storage.Value;
    }

    public int Count => _md5ToPath.Count;

    public IReadOnlyDictionary<string, string> Snapshot() => new Dictionary<string, string>(_md5ToPath);

    public PhotoContent? GetPhotoContent(string md5)
    {
        if (string.IsNullOrWhiteSpace(md5)) return null;
        if (!_md5ToPath.TryGetValue(md5, out var path)) return null;
        
        
        var directoryName = Path.GetDirectoryName(path) ?? throw new Exception("Invalid file path.");
        var previewFolder = Path.Combine(directoryName, "preview");

        return new PhotoContent
        {
            Md5 = md5,
            Real = path,
            P16 = Path.Combine(previewFolder, $"{Path.GetFileNameWithoutExtension(path)}_p16.jpg"),
            P32 = Path.Combine(previewFolder, $"{Path.GetFileNameWithoutExtension(path)}_p32.jpg"),
            P64 = Path.Combine(previewFolder, $"{Path.GetFileNameWithoutExtension(path)}_p64.jpg"),
            P128 = Path.Combine(previewFolder, $"{Path.GetFileNameWithoutExtension(path)}_p128.jpg"),
            P512 = Path.Combine(previewFolder, $"{Path.GetFileNameWithoutExtension(path)}_p512.jpg"),
            P2000 = Path.Combine(previewFolder, $"{Path.GetFileNameWithoutExtension(path)}_p2000.jpg")
        };
    }

    public async Task<int> BuildIndexAsync(CancellationToken ct = default)
    {
        _md5ToPath.Clear();

        var root = _storage.RootPath;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            _logger.LogWarning("PhotoLocator: Storage.RootPath is not configured or does not exist: {Root}", root);
            return 0;
        }

        var storageFolders = PathExtensions.CollectStorageFolders(root);
        var totalProcessed = 0;

        // Sequential, non-parallel scan
        foreach (var entry in PathExtensions.GetFilesInFolder(root, storageFolders))
        {
            ct.ThrowIfCancellationRequested();
            var filePath = (string)entry[0];
            try
            {
                var md5 = await filePath.CalculateMd5Async();
                // last write wins if duplicates found
                _md5ToPath[md5] = filePath;
                totalProcessed++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PhotoLocator: failed to process file {File}", filePath);
            }
        }

        _logger.LogInformation("PhotoLocator: indexed {Count} files from {Root}", totalProcessed, root);
        return totalProcessed;
    }
}

public record PhotoContent
{
    public string Md5 { get; init; }
    public string Real { get; init; }
    public string P16 { get; init; }
    public string P32 { get; init; }
    public string P64 { get; init; }
    public string P128 { get; init; }
    public string P512 { get; init; }
    public string P2000 { get; init; }
}