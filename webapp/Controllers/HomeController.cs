using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using webapp.Models;

namespace webapp.Controllers;

public class HomeController(ILogger<HomeController> logger) : Controller
{
    public IActionResult Index([FromQuery] string[]? tags)
    {
        // expose selected tags (from model binding) to the view
        ViewBag.SelectedTags = tags ?? Array.Empty<string>();
        return View();
    }

    private static readonly int[] AllowedSizes = [16, 32, 64, 128, 256, 512, 2000];

    private static int SnapToAllowed(int value)
    {
        var closest = AllowedSizes[0];
        foreach (var s in AllowedSizes)
        {
            if (Math.Abs(s - value) < Math.Abs(closest - value)) closest = s;
        }
        return closest;
    }

    [HttpGet]
    public IActionResult SizeUp(
        [FromQuery] string? query,
        [FromQuery] string[]? tags,
        [FromQuery] string[]? filters,
        [FromQuery] string[]? sorting,
        [FromQuery] int? pageSize,
        [FromQuery] int? size)
    {
        var current = SnapToAllowed(size ?? 256);
        var idx = Array.IndexOf(AllowedSizes, current);
        var nextIdx = Math.Min(AllowedSizes.Length - 1, Math.Max(0, idx + 1));
        var nextSize = AllowedSizes[nextIdx];

        return RedirectToAction("Index", new
        {
            query,
            tags,
            filters,
            sorting,
            pageSize = pageSize ?? 12,
            size = nextSize,
            page = 1
        });
    }

    [HttpGet]
    public IActionResult SizeDown(
        [FromQuery] string? query,
        [FromQuery] string[]? tags,
        [FromQuery] string[]? filters,
        [FromQuery] string[]? sorting,
        [FromQuery] int? pageSize,
        [FromQuery] int? size)
    {
        var current = SnapToAllowed(size ?? 256);
        var idx = Array.IndexOf(AllowedSizes, current);
        var prevIdx = Math.Max(0, Math.Max(0, idx - 1));
        var prevSize = AllowedSizes[prevIdx];

        return RedirectToAction("Index", new
        {
            query,
            tags,
            filters,
            sorting,
            pageSize = pageSize ?? 12,
            size = prevSize,
            page = 1
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}