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

        var photoContent = imageLocator.GetImageLinks(md5);

        // choose preview path based on requested width (w). fall back to 512 if not specified.
        var previewPath = photoContent == null ? null : SelectPreviewPath(photoContent, w);

        if (string.IsNullOrWhiteSpace(previewPath) || !System.IO.File.Exists(previewPath))
        {
            return NotFound();
        }

        var contentType = GetContentType(previewPath!);
        var stream = await System.IO.File.ReadAllBytesAsync(previewPath!, ct);
        return File(stream, contentType);
    }

    private static string SelectPreviewPath(Services.ImageLinks links, int? w)
    {
        // default to 512 if width is not specified
        if (w is null) return links.P512;

        var width = Math.Max(1, w.Value);

        // pick the smallest preview that is >= requested width, otherwise the largest available
        if (width <= 16) return links.P16;
        if (width <= 32) return links.P32;
        if (width <= 64) return links.P64;
        if (width <= 128) return links.P128;
        if (width <= 512) return links.P512;
        return links.P2000;
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