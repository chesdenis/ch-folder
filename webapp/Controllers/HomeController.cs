using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using webapp.Models;
using Microsoft.Extensions.Options;
using webapp.Services;

namespace webapp.Controllers;

public class HomeController(
    ILogger<HomeController> logger,
    IJobRunner jobRunner,
    IOptions<StorageOptions> storageOptions,
    IDockerSearchRunner dockerSearchRunner,
    ISearchResultsRepository searchResultsRepo) : Controller
{
    private readonly StorageOptions _storage = storageOptions.Value;
    private readonly IDockerSearchRunner _dockerSearchRunner = dockerSearchRunner;
    private readonly ISearchResultsRepository _searchResultsRepo = searchResultsRepo;
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
    public async Task<IActionResult> Search()
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

        // Execute ImageSearcher container and wait for completion
        var queryText = Request.Query["query"].ToString();
        if (string.IsNullOrWhiteSpace(queryText))
        {
            // No query provided, just redirect with normalized route
            return RedirectToAction("Index", route);
        }

        var actionsPath = _storage.ActionsPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actionsPath))
        {
            logger.LogWarning("[Search] Storage.ActionsPath is not configured. Falling back to redirect.");
            return RedirectToAction("Index", route);
        }

        int exitCode = await _dockerSearchRunner.RunImageSearcherAsync(actionsPath, queryText,
            onStdout: s => logger.LogInformation("[image_searcher][stdout] {Line}", s),
            onStderr: s => logger.LogWarning("[image_searcher][stderr] {Line}", s));

        if (exitCode != 0)
        {
            logger.LogError("[Search] image_searcher exited with code {Code}", exitCode);
            TempData["SearchError"] = $"Search failed with code {exitCode}";
            return RedirectToAction("Index", route);
        }

        // Read the latest results from metastore
        var latest = await _searchResultsRepo.GetLatestResultsAsync(HttpContext.RequestAborted);
        if (latest is null || latest.Results.Count == 0)
        {
            TempData["SearchInfo"] = "No results found";
            return RedirectToAction("Index", route);
        }

        // Pass results to the Index view directly for immediate display
        ViewBag.SelectedTags = route.TryGetValue("tags", out var t) ? t : Array.Empty<string>();
        ViewBag.SearchResults = latest.Results;
        ViewBag.Total = latest.Results.Count;
        ViewBag.PageSize = route["pageSize"]; // preserve page size for the view
        ViewBag.Size = route["size"];
        ViewBag.Query = queryText;
        return View("Index");
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

    public IActionResult Meta()
    {
        ViewBag.StoragePath = _storage.RootPath ?? string.Empty;
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    [HttpPost]
    public IActionResult IndexJob([FromForm] string jobId, [FromForm] JobType type, [FromForm] int? dop)
    {
        if (string.IsNullOrWhiteSpace(jobId)) return BadRequest("jobId is required");
        var id = jobRunner.StartJob(jobId, type, dop);
        return Ok(new { jobId = id });
    }
}