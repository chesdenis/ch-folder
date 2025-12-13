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

    public async Task<IActionResult> Index([FromQuery] string[]? tags)
    {
        // Expose selected tags (from model binding) to the view
        ViewBag.SelectedTags = tags ?? Array.Empty<string>();

        // Pull paging and size from query to load real data for the gallery
        var page = int.TryParse(Request.Query["page"], out var p) ? Math.Max(1, p) : 1;
        var pageSize = int.TryParse(Request.Query["pageSize"], out var ps) ? Math.Max(1, ps) : 12;
        var thumbSize = int.TryParse(Request.Query["size"], out var sz) ? sz : 256;
        thumbSize = SnapToAllowed(thumbSize);

        // Load recent photos as the default gallery content (no sample data)
        var total = await searchResultsRepo.GetPhotosCountAsync(HttpContext.RequestAborted);
        var offset = (page - 1) * pageSize;
        var md5s = await searchResultsRepo.GetRecentPhotoMd5Async(offset, pageSize, HttpContext.RequestAborted);

        var items = md5s
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => new webapp.Components.GalleryItem
            {
                FullUrl = Url.Action("ByMd5", "Images", new { md5 = m })!,
                // Fallback dimensions (real dimensions can be added later)
                FullWidth = 512,
                FullHeight = 512,
                Alt = m!
            })
            .ToList();

        ViewBag.GalleryItems = items;
        ViewBag.Total = total;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Size = thumbSize;

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

        // Determine requested page from query; default to 1
        var pageFromQuery = 1;
        if (int.TryParse(Request.Query["page"], out var pageVal) && pageVal > 0)
        {
            pageFromQuery = pageVal;
        }
        route["page"] = pageFromQuery;

        var normalizedSize = route["size"];
        var normalizedPageSize = route["pageSize"];
        logger.LogInformation(
            "[Search] normalized state -> pageSize: {PageSize}, size: {Size}, page reset to 1",
            normalizedPageSize, normalizedSize);

        // Execute ImageSearcher container and wait for completion only for fresh searches (no page param)
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

        var isPagingRequest = Request.Query.ContainsKey("page");
        if (!isPagingRequest)
        {
            int exitCode = await dockerSearchRunner.RunImageSearcherAsync(actionsPath, queryText,
                onStdout: s => logger.LogInformation("[image_searcher][stdout] {Line}", s),
                onStderr: s => logger.LogWarning("[image_searcher][stderr] {Line}", s));

            if (exitCode != 0)
            {
                logger.LogError("[Search] image_searcher exited with code {Code}", exitCode);
                TempData["SearchError"] = $"Search failed with code {exitCode}";
                return RedirectToAction("Index", route);
            }
        }

        // Read the latest results from metastore
        var latest = await searchResultsRepo.GetLatestResultsAsync(HttpContext.RequestAborted);
        if (latest is null || latest.Results.Count == 0)
        {
            TempData["SearchInfo"] = "No results found";
            return RedirectToAction("Index", route);
        }

        // Normalize typed values for the view to avoid dynamic cast issues
        var pageSizeInt = int.TryParse(route["pageSize"]?.ToString(), out var psVal) ? psVal : 12;
        var sizeInt = int.TryParse(route["size"]?.ToString(), out var szVal) ? szVal : 256;
        sizeInt = SnapToAllowed(sizeInt);

        // Pass results to the Index view directly for immediate display
        ViewBag.SelectedTags = route.TryGetValue("tags", out var t) ? t : Array.Empty<string>();
        ViewBag.SearchResults = latest.Results;
        ViewBag.Total = latest.Results.Count;
        ViewBag.Page = pageFromQuery; // reflect requested page
        ViewBag.PageSize = pageSizeInt; // strongly-typed int
        ViewBag.Size = sizeInt; // strongly-typed int
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