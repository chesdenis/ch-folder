using Microsoft.AspNetCore.Mvc;
using shared_csharp.Abstractions;
using webapp.Services;

namespace webapp.Controllers;

[Route("images")]
public sealed class ImagesController(
    IContentProvider contentProvider,
    IImageLocator imageLocator)
    : Controller
{
    [HttpGet("by-md5/{md5}")]
    public async Task<IActionResult> ByMd5(string md5, [FromQuery] int? w, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(md5)) return BadRequest("md5 is required");

        var metadataWithPreviews = await contentProvider.GetMetadataWithPreviews(md5);
        var previews = metadataWithPreviews.Previews;

        if (previews == null) return NotFound();
        
        var previewBase64 = SelectPreviewBase64(new ImageLinks
        {
            Md5 = md5,
            Previews = previews,
            P2000Width = 2000, // Fallback or could be parsed from metadata if available
            P2000Height = 1500
        }, w);
        
        if (string.IsNullOrWhiteSpace(previewBase64))
        {
            return NotFound();
        }

        var bytes = Convert.FromBase64String(previewBase64);
        return File(bytes, "image/jpeg");
    }

    [HttpGet("download/{md5}")]
    public async Task<IActionResult> Download(string md5, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(md5)) return BadRequest("md5 is required");

        var extension = await contentProvider.GetExtension(md5);
        var contentType = GetContentType(extension);
        var fileName = md5 + extension;

        var stream = await contentProvider.GetReal(md5);
        return File(stream, contentType, fileName);
    }

    private static string? SelectPreviewBase64(Services.ImageLinks links, int? w)
    {
        if (links.Previews == null) return null;
        
        string key = "512"; // Default
        if (w.HasValue)
        {
            var width = w.Value;
            if (width <= 16) key = "16";
            else if (width <= 32) key = "32";
            else if (width <= 64) key = "64";
            else if (width <= 128) key = "128";
            else if (width <= 512) key = "512";
            else key = "2000";
        }

        if (links.Previews.TryGetValue(key, out var base64)) return base64;
        
        // Fallback to any available preview if requested one is missing
        return links.Previews.Values.FirstOrDefault();
    }

    private static string GetContentType(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}