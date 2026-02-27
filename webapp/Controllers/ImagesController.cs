using Microsoft.AspNetCore.Mvc;
using webapp.Services;

namespace webapp.Controllers;

[Route("images")]
public sealed class ImagesController(
    IImageLocator imageLocator)
    : Controller
{
    [HttpGet("by-md5/{md5}")]
    public async Task<IActionResult> ByMd5(string md5, [FromQuery] int? w, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(md5)) return BadRequest("md5 is required");

        var links = await imageLocator.GetImageLinksAsync(md5);
        if (links?.Previews == null) return NotFound();

        var previewBase64 = SelectPreviewBase64(links, w);
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

        var links = await imageLocator.GetImageLinksAsync(md5);
        if (links == null || string.IsNullOrWhiteSpace(links.Real) || !System.IO.File.Exists(links.Real))
        {
            return NotFound();
        }

        var path = links.Real;
        var contentType = GetContentType(path);
        var fileName = Path.GetFileName(path);
        
        var stream = await System.IO.File.ReadAllBytesAsync(path, ct);
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

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}