using Microsoft.AspNetCore.Mvc;
using webapp.Services;

namespace webapp.Controllers;

[Route("images")]
public sealed class ImagesController(
    IImageLocator imageLocator)
    : Controller
{
    [HttpGet("by-md5/{md5}")]
    public async Task<IActionResult> ByMd5(string md5, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(md5)) return BadRequest("md5 is required");

        var photoContent = imageLocator.GetImageLinks(md5);

        if (photoContent == null || !System.IO.File.Exists(photoContent.P512))
        {
            return NotFound();
        }

        var contentType = GetContentType(photoContent.P512);
        var stream = await System.IO.File.ReadAllBytesAsync(photoContent.P512, ct);
        return File(stream, contentType);
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