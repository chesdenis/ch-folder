using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using shared_csharp.Extensions;
using webapp.Models;

namespace webapp.Services;

public interface IImageLocator
{
    Task<int> IdentifyImageLocations(CancellationToken ct = default);
    public ImageLinks? GetImageLinks(string md5);
}

public sealed class ImageLocator(
    IOptions<StorageOptions> storage,
    ILogger<ImageLocator> logger,
    IImageLocationRepository imageLocationRepository)
    : IImageLocator
{
    private readonly StorageOptions _storage = storage.Value;
    private readonly ConcurrentDictionary<string, string> _imageLocationsMap = new();
    private readonly IImageLocationRepository _repo = imageLocationRepository;
    
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

    public ImageLinks? GetImageLinks(string md5)
    {
        if (string.IsNullOrWhiteSpace(md5)) return null;
        if (!_imageLocationsMap.TryGetValue(md5, out var path)) return null;

        var p16 = path.GetPreview16Path();
        var p32 = path.GetPreview32Path();
        var p64 = path.GetPreview64Path();
        var p128 = path.GetPreview128Path();
        var p512 = path.GetPreview512Path();
        var p2000 = path.GetPreview2000Path();

        // Attempt to read the dimensions of the 2000px preview (used in lightbox href)
        var size = TryReadJpegSize(p2000);

        var links = new ImageLinks
        {
            Md5 = md5,
            Real = path,
            P16 = p16,
            P32 = p32,
            P64 = p64,
            P128 = p128,
            P512 = p512,
            P2000 = p2000,
            P2000Width = size?.width,
            P2000Height = size?.height
        };
        return links;
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
            await _repo.UpsertLocationsAsync(_imageLocationsMap, ct);
            logger.LogInformation("PhotoLocator: uploaded {Count} image locations to DB", _imageLocationsMap.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PhotoLocator: failed to upload image locations to DB");
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
    public required string P16 { get; init; }
    public required string P32 { get; init; }
    public required string P64 { get; init; }
    public required string P128 { get; init; }
    public required string P512 { get; init; }
    public required string P2000 { get; init; }
    // Optional dimensions of the 2000px preview (read from file header if available)
    public int? P2000Width { get; init; }
    public int? P2000Height { get; init; }
}