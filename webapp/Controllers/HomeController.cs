using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using webapp.Models;
using Microsoft.Extensions.Options;
using webapp.Services;

namespace webapp.Controllers;

public class HomeController(ILogger<HomeController> logger, IJobRunner jobRunner, IOptions<StorageOptions> storageOptions) : Controller
{
    private readonly IJobRunner _jobRunner = jobRunner;
    private readonly StorageOptions _storage = storageOptions.Value;
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
    public IActionResult Search()
    {
        // Log invocation to verify this endpoint is being triggered
        logger.LogInformation(
            "[Search] endpoint invoked at {Timestamp} from {RemoteIp} with query {QueryString} and {KeyCount} keys",
            DateTimeOffset.Now,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty,
            Request.Query.Count);

        // Build route values from full incoming query/model state, normalize some options
        var route = new RouteValueDictionary();

        // Copy all existing query parameters (supports multi-values)
        foreach (var (key, value) in Request.Query)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            route[key] = value.Count > 1 ? value.ToArray() : value.ToString();
        }

        // Normalize paging and size options
        // Default page size
        if (!route.ContainsKey("pageSize") || !int.TryParse(route["pageSize"]?.ToString(), out var pageSize) || pageSize <= 0)
        {
            route["pageSize"] = 12;
        }

        // Snap thumbnail size to allowed steps
        if (!route.ContainsKey("size") || !int.TryParse(route["size"]?.ToString(), out var sizeVal))
        {
            sizeVal = 256;
        }
        route["size"] = SnapToAllowed(sizeVal);

        // When initiating a search, reset to the first page unless explicitly provided as 1
        route["page"] = 1;

        var normalizedSize = route["size"];
        var normalizedPageSize = route["pageSize"];
        logger.LogInformation(
            "[Search] normalized state -> pageSize: {PageSize}, size: {Size}, page reset to 1",
            normalizedPageSize, normalizedSize);

        return RedirectToAction("Index", route);
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

    public IActionResult Status()
    {
        ViewBag.StoragePath = _storage.RootPath ?? string.Empty;
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    [HttpPost]
    public IActionResult StartJob([FromForm] string? folder, [FromForm] string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId)) return BadRequest("jobId is required");
        var id = _jobRunner.StartJob(folder, jobId);
        return Ok(new { jobId = id });
    }
}