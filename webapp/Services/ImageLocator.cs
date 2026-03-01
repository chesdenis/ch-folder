using System.Collections.Concurrent;
using shared_csharp.Abstractions;

namespace webapp.Services;

public interface IImageLocator
{
    public Task<ImageLinks?> GetImageLinksAsync(string md5);
}

public sealed class ImageLocator(
    ILogger<ImageLocator> logger,
    IContentProvider contentProvider)
    : IImageLocator
{
    private readonly ConcurrentDictionary<string, string> _imageLocationsMap = new();
    
    public async Task<ImageLinks?> GetImageLinksAsync(string md5)
    {
        if (string.IsNullOrWhiteSpace(md5)) return null;
        if (!_imageLocationsMap.TryGetValue(md5, out var path)) return null;

        var metadata = await contentProvider.GetMetadataByMd5(path);

        var links = new ImageLinks
        {
            Md5 = md5,
            Previews = metadata?.Previews,
            P2000Width = 2000, // Fallback or could be parsed from metadata if available
            P2000Height = 1500
        };
        return links;
    }
}

public record ImageLinks
{
    public required string Md5 { get; init; }
    public IReadOnlyDictionary<string, string>? Previews { get; init; }
    // Optional dimensions of the 2000px preview (read from file header if available)
    public int? P2000Width { get; init; }
    public int? P2000Height { get; init; }
}