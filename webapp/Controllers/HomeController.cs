using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using webapp.Models;
using Microsoft.Extensions.Options;
using shared_csharp.Extensions;
using webapp.Services;

namespace webapp.Controllers;

public class HomeController(
    ILogger<HomeController> logger,
    IJobRunner jobRunner,
    IOptions<StorageOptions> storageOptions,
    IDockerSearchRunner dockerSearchRunner,
    ISearchResultsRepository searchResultsRepo,
    ISearchSessionRepository sessionsRepo,
    ISearchSessionSelectionRepository selectionRepo,
    IImageLocator imageLocator) : Controller
{
    private readonly StorageOptions _storage = storageOptions.Value;
    private readonly IImageLocator _imageLocator = imageLocator;

    public async Task<IActionResult> Index([FromQuery] string[]? tags)
    {
        // If only sessionId is provided (no query), redirect to Search to restore query by sessionId
        var sessionIdStr = Request.Query["sessionId"].ToString();
        var hasSessionInQuery = Guid.TryParse(sessionIdStr, out var sessionIdVal);
        var hasQueryInQuery = Request.Query.ContainsKey("query") && !string.IsNullOrWhiteSpace(Request.Query["query"].ToString());
        if (hasSessionInQuery && !hasQueryInQuery)
        {
            // Preserve all query parameters (page/pageSize/size/tags/etc.) and let Search restore query
            var route = new RouteValueDictionary();
            foreach (var (key, value) in Request.Query)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                route[key] = value.Count > 1 ? value.ToArray() : value.ToString();
            }
            route["sessionId"] = sessionIdVal; // ensure normalized
            return RedirectToAction("Search", route);
        }

        // Expose selected tags (from model binding) to the view
        ViewBag.SelectedTags = tags ?? Array.Empty<string>();

        // Pull paging and size from query to load real data for the gallery
        var page = int.TryParse(Request.Query["page"], out var p) ? Math.Max(1, p) : 1;
        var pageSize = int.TryParse(Request.Query["pageSize"], out var ps) ? Math.Max(1, ps) : 12;
        var thumbSize = int.TryParse(Request.Query["size"], out var sz) ? sz : 256;
        thumbSize = thumbSize.SnapToAllowed();

        // Load recent photos as the default gallery content (no sample data)
        var total = await searchResultsRepo.GetPhotosCountAsync(HttpContext.RequestAborted);
        var offset = (page - 1) * pageSize;
        var md5s = await searchResultsRepo.GetRecentPhotoMd5Async(offset, pageSize, HttpContext.RequestAborted);

        var items = md5s
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m =>
            {
                var links = _imageLocator.GetImageLinks(m);
                // Prefer actual preview-2000 dimensions if available, otherwise use a safe fallback
                int pw = Math.Max(1, links?.P2000Width ?? 2000);
                int ph = Math.Max(1, links?.P2000Height ?? 1500);
                return new webapp.Components.GalleryItem
                {
                    FullUrl = Url.Action("ByMd5", "Images", new { md5 = m })!,
                    FullWidth = pw,
                    FullHeight = ph,
                    Alt = m!,
                    Md5 = m!
                };
            })
            .ToList();

        ViewBag.GalleryItems = items;
        ViewBag.Total = total;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Size = thumbSize;

        return View();
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

        route["size"] = sizeVal.SnapToAllowed();

        // Determine requested page from query; default to 1
        var pageFromQuery = 1;
        if (int.TryParse(Request.Query["page"], out var pageVal) && pageVal > 0)
        {
            pageFromQuery = pageVal;
        }
        route["page"] = pageFromQuery;

        var normalizedSize = route["size"];
        // Score limiter (minScore) normalization with default 0.78
        const double defaultMinScore = 0.78;
        if (!route.ContainsKey("minScore") ||
            !double.TryParse(route["minScore"]?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var minScoreVal))
        {
            route["minScore"] = defaultMinScore.ToString(System.Globalization.CultureInfo.InvariantCulture);
            minScoreVal = defaultMinScore;
        }
        // Clamp [0,1] and round to 2 decimals for display/urls
        minScoreVal = Math.Max(0, Math.Min(1, minScoreVal));
        minScoreVal = Math.Round(minScoreVal, 2, MidpointRounding.AwayFromZero);
        route["minScore"] = minScoreVal.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var normalizedPageSize = route["pageSize"];
        logger.LogInformation(
            "[Search] normalized state -> pageSize: {PageSize}, size: {Size}, page reset to 1",
            normalizedPageSize, normalizedSize);

        // Determine if a specific session is requested via query string
        var sessionIdStr = Request.Query["sessionId"].ToString();
        Guid requestedSessionId;
        var hasSessionInQuery = Guid.TryParse(sessionIdStr, out requestedSessionId);

        // Execute ImageSearcher container and wait for completion based on session/query state
        var queryText = Request.Query["query"].ToString();
        if (string.IsNullOrWhiteSpace(queryText))
        {
            if (hasSessionInQuery)
            {
                // Try to restore query text by session id
                // Do NOT apply minScore here; we just need to restore the query for the session
                var byId = await searchResultsRepo.GetResultsBySessionIdAsync(requestedSessionId, null, HttpContext.RequestAborted);
                if (byId != null)
                {
                    route["query"] = byId.QueryText;
                    route["sessionId"] = byId.SessionId;
                    return RedirectToAction("Search", route);
                }
            }

            // No query and no valid session to restore from -> go to Index with normalized route
            return RedirectToAction("Index", route);
        }
        
        var tagsValues = Request.Query["tags"].ToString();
        var tags = tagsValues.Split(',', StringSplitOptions.RemoveEmptyEntries);

        var actionsPath = _storage.ActionsPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actionsPath))
        {
            logger.LogWarning("[Search] Storage.ActionsPath is not configured. Falling back to redirect.");
            return RedirectToAction("Index", route);
        }

        // requestedSessionId/hasSessionInQuery are already computed above

        var isPagingRequest = Request.Query.ContainsKey("page");

        SearchSessionResults? sessionToUse = null;

        if (hasSessionInQuery)
        {
            // Try to stick with provided session id
            // IMPORTANT: don't use minScore for this validation; otherwise zero filtered results causes endless reruns
            var byId = await searchResultsRepo.GetResultsBySessionIdAsync(requestedSessionId, null, HttpContext.RequestAborted);
            if (byId != null && string.Equals(byId.QueryText, queryText, StringComparison.Ordinal))
            {
                sessionToUse = byId;
            }
            else
            {
                // Provided session is missing or belongs to a different query -> run a new search and stick to new latest
                int exitCode = await dockerSearchRunner.RunImageSearcherAsync(actionsPath, queryText, tags,
                    onStdout: s => logger.LogInformation("[image_searcher][stdout] {Line}", s),
                    onStderr: s => logger.LogWarning("[image_searcher][stderr] {Line}", s));
                if (exitCode != 0)
                {
                    logger.LogError("[Search] image_searcher exited with code {Code}", exitCode);
                    TempData["SearchError"] = $"Search failed with code {exitCode}";
                    return RedirectToAction("Index", route);
                }

                var latestAfterRun = await searchResultsRepo.GetLatestResultsAsync(HttpContext.RequestAborted);
                if (latestAfterRun is null || latestAfterRun.Results.Count == 0)
                {
                    TempData["SearchInfo"] = "No results found";
                    return RedirectToAction("Index", route);
                }

                // Redirect to same action but with new sessionId to stick
                route["sessionId"] = latestAfterRun.SessionId;
                return RedirectToAction("Search", route);
            }
        }

        if (sessionToUse is null)
        {
            // No specific session requested; optionally run search on non-paging request
            if (!isPagingRequest)
            {
                int exitCode = await dockerSearchRunner.RunImageSearcherAsync(actionsPath, queryText, tags,
                    onStdout: s => logger.LogInformation("[image_searcher][stdout] {Line}", s),
                    onStderr: s => logger.LogWarning("[image_searcher][stderr] {Line}", s));
                if (exitCode != 0)
                {
                    logger.LogError("[Search] image_searcher exited with code {Code}", exitCode);
                    TempData["SearchError"] = $"Search failed with code {exitCode}";
                    return RedirectToAction("Index", route);
                }
            }

            // Use the latest session and then stick to it by adding sessionId
            var latest = await searchResultsRepo.GetLatestResultsAsync(HttpContext.RequestAborted);
            if (latest is null || latest.Results.Count == 0)
            {
                TempData["SearchInfo"] = "No results found";
                return RedirectToAction("Index", route);
            }

            // If the current query already has the same session id, render; otherwise redirect to stick
            if (!hasSessionInQuery || latest.SessionId != requestedSessionId)
            {
                route["sessionId"] = latest.SessionId;
                return RedirectToAction("Search", route);
            }

            sessionToUse = latest;
        }

        // Normalize typed values for the view to avoid dynamic cast issues
        var pageSizeInt = int.TryParse(route["pageSize"]?.ToString(), out var psVal) ? psVal : 12;
        var sizeInt = int.TryParse(route["size"]?.ToString(), out var szVal) ? szVal : 256;
        sizeInt = sizeInt.SnapToAllowed();

        // Apply score limiter to results
        var minScoreForFilter = (float)minScoreVal;
        var filteredResults = sessionToUse!.Results
            .Where(r => r.Score >= minScoreForFilter)
            .ToList();

        // Pass results to the Index view directly for immediate display
        ViewBag.SelectedTags = route.TryGetValue("tags", out var t) ? t : Array.Empty<string>();
        ViewBag.SearchResults = filteredResults;
        // Build gallery items with real preview dimensions for PhotoSwipe
        var galleryItems = filteredResults
            .Where(r => !string.IsNullOrWhiteSpace(r.Md5))
            .Select(r => r.Md5!)
            .Select(m =>
            {
                var links = _imageLocator.GetImageLinks(m);
                int pw = Math.Max(1, links?.P2000Width ?? 2000);
                int ph = Math.Max(1, links?.P2000Height ?? 1500);
                return new webapp.Components.GalleryItem
                {
                    FullUrl = Url.Action("ByMd5", "Images", new { md5 = m })!,
                    FullWidth = pw,
                    FullHeight = ph,
                    Alt = m,
                    Md5 = m
                };
            })
            .ToList();
        ViewBag.GalleryItems = galleryItems;
        // Load distinct tags for this session to populate tags selector
        try
        {
            var availableTags = await searchResultsRepo.GetDistinctTagsForSessionAsync(sessionToUse.SessionId, HttpContext.RequestAborted);
            ViewBag.AvailableTags = availableTags;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Search] Failed to load available tags for session {SessionId}", sessionToUse.SessionId);
            ViewBag.AvailableTags = Array.Empty<string>();
        }
        ViewBag.Total = filteredResults.Count;
        ViewBag.Page = pageFromQuery; // reflect requested page
        ViewBag.PageSize = pageSizeInt; // strongly-typed int
        ViewBag.Size = sizeInt; // strongly-typed int
        ViewBag.Query = queryText;
        ViewBag.SessionId = sessionToUse.SessionId;
        ViewBag.MinScore = minScoreVal;
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
        var current = (size ?? 256).SnapToAllowed();
        var idx = Array.IndexOf(ImageProcessingExtensions.AllowedSizes, current);
        var nextIdx = Math.Min(ImageProcessingExtensions.AllowedSizes.Length - 1, Math.Max(0, idx + 1));
        var nextSize = ImageProcessingExtensions.AllowedSizes[nextIdx];

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
        var current = (size ?? 256).SnapToAllowed();
        var idx = Array.IndexOf(ImageProcessingExtensions.AllowedSizes, current);
        var prevIdx = Math.Max(0, Math.Max(0, idx - 1));
        var prevSize = ImageProcessingExtensions.AllowedSizes[prevIdx];

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

    [HttpGet]
    public IActionResult RankUp(
        [FromQuery] string? query,
        [FromQuery] string[]? tags,
        [FromQuery] string[]? filters,
        [FromQuery] string[]? sorting,
        [FromQuery] int? pageSize,
        [FromQuery] int? size,
        [FromQuery] string? minScore)
    {
        if (!double.TryParse(minScore, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ms))
            ms = 0.78;
        ms = Math.Min(1.0, Math.Round(ms + 0.01, 2, MidpointRounding.AwayFromZero));
        var msStr = ms.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return RedirectToAction("Index", new
        {
            query,
            tags,
            filters,
            sorting,
            pageSize = pageSize ?? 12,
            size = (size ?? 256).SnapToAllowed(),
            minScore = msStr,
            page = 1
        });
    }

    [HttpGet]
    public IActionResult RankDown(
        [FromQuery] string? query,
        [FromQuery] string[]? tags,
        [FromQuery] string[]? filters,
        [FromQuery] string[]? sorting,
        [FromQuery] int? pageSize,
        [FromQuery] int? size,
        [FromQuery] string? minScore)
    {
        if (!double.TryParse(minScore, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ms))
            ms = 0.78;
        ms = Math.Max(0.0, Math.Round(ms - 0.01, 2, MidpointRounding.AwayFromZero));
        var msStr = ms.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return RedirectToAction("Index", new
        {
            query,
            tags,
            filters,
            sorting,
            pageSize = pageSize ?? 12,
            size = (size ?? 256).SnapToAllowed(),
            minScore = msStr,
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

    public IActionResult Images()
    {
        ViewBag.StoragePath = _storage.RootPath ?? string.Empty;
        ViewBag.InputPath = _storage.InputPath ?? string.Empty;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Selected([FromQuery] Guid sessionId)
    {
        if (sessionId == Guid.Empty)
            return BadRequest("sessionId is required");

        var items = await selectionRepo.GetSelectedMd5Async(sessionId, HttpContext.RequestAborted);

        var vm = new SelectedViewModel
        {
            SessionId = sessionId,
            Items = items.Select(i => new SelectedItemViewModel
            {
                Md5 = i.Md5,
                ShortDetails = i.ShortDetails,
                Tags = i.Tags ?? Array.Empty<string>(),
                ImageUrl = Url.Action("ByMd5", "Images", new { md5 = i.Md5, w = 128 })!
            }).ToList()
        };

        return View(vm);
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

    [HttpGet]
    public async Task<IActionResult> Sessions([FromQuery] int? page, [FromQuery] int? pageSize)
    {
        var p = Math.Max(1, page ?? 1);
        var ps = Math.Max(1, pageSize ?? 20);
        var offset = (p - 1) * ps;

        var (items, total) = await sessionsRepo.GetRecentSessionsAsync(offset, ps, HttpContext.RequestAborted);

        ViewBag.Total = total;
        ViewBag.Page = p;
        ViewBag.PageSize = ps;

        return View(items);
    }
}