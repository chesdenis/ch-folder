using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using webapp.Models;
using Microsoft.Extensions.Options;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;
using webapp.Services;
using System.Text.Json;

namespace webapp.Controllers;

public class HomeController(
    ILogger<HomeController> logger,
    IJobRunner jobRunner,
    IOptions<StorageOptions> storageOptions,
    IDockerSearchRunner dockerSearchRunner,
    ISearchResultsRepository searchResultsRepo,
    ISearchSessionRepository sessionsRepo,
    ISearchSessionSelectionRepository selectionRepo,
    IImageLocator imageLocator,
    IImageLocationRepository imageLocationRepository,
    IFileSystem fileSystem,
    IContentValidationRepository contentValidationRepository,
    IPublishTrackerRepository publishTrackerRepo) : Controller
{
    private readonly StorageOptions _storage = storageOptions.Value;

    [HttpPost]
    public async Task<IActionResult> TogglePublish(string md5, string platform)
    {
        if (string.IsNullOrWhiteSpace(md5) || string.IsNullOrWhiteSpace(platform))
            return BadRequest();

        await publishTrackerRepo.TogglePublishStatusAsync(md5, platform, HttpContext.RequestAborted);
        return Ok();
    }

    public async Task<IActionResult> Index([FromQuery] string[]? tags, [FromQuery] string[]? persons, [FromQuery] int[]? commerceRatings, [FromQuery] string[]? folders, [FromQuery] string[]? extensions)
    {
        // Default values for Navigation page when not explicitly provided
        var effectiveCommerceRatings = (commerceRatings == null || commerceRatings.Length == 0) ? [4, 5] : commerceRatings;
        
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

        // Expose selected filters to the view
        ViewBag.SelectedTags = tags ?? Array.Empty<string>();
        ViewBag.SelectedPersons = persons ?? Array.Empty<string>();
        ViewBag.SelectedFolders = folders ?? Array.Empty<string>();
        ViewBag.SelectedExtensions = extensions ?? Array.Empty<string>();
        ViewBag.CommerceRatings = effectiveCommerceRatings;
        ViewBag.SessionId = Guid.Empty; // Static empty guid for navigation search result id

        // Fetch available tags and persons for filtering
        ViewBag.AvailableTags = await searchResultsRepo.GetAllDistinctTagsAsync(HttpContext.RequestAborted);
        ViewBag.AvailablePersons = await searchResultsRepo.GetAllDistinctPersonsAsync(HttpContext.RequestAborted);
        
        // Use ImageLocator for folders as it has in-memory map which is faster/more accurate for current session
        ViewBag.AvailableFolders = imageLocator.GetAvailableFolders();
        ViewBag.AvailableExtensions = await searchResultsRepo.GetAllDistinctExtensionsAsync(HttpContext.RequestAborted);

        // Pull paging and size from query to load real data for the gallery
        var page = int.TryParse(Request.Query["page"], out var p) ? Math.Max(1, p) : 1;
        var pageSize = int.TryParse(Request.Query["pageSize"], out var ps) ? Math.Max(1, ps) : 12;
        var thumbSize = int.TryParse(Request.Query["size"], out var sz) ? sz : 256;
        thumbSize = thumbSize.SnapToAllowed();

        // Load photos as the gallery content with hard filters
        var total = await searchResultsRepo.GetPhotosCountAsync(tags, persons, effectiveCommerceRatings, folders, extensions, groupByGroup: true, HttpContext.RequestAborted);
        var offset = (page - 1) * pageSize;
        var md5s = await searchResultsRepo.GetRecentPhotoMd5Async(offset, pageSize, tags, persons, effectiveCommerceRatings, folders, extensions, groupByGroup: true, HttpContext.RequestAborted);

        // Fetch full photo info to get ShortDetails for "Jump to Search"
        var photos = await searchResultsRepo.GetPhotosByMd5sAsync(md5s, HttpContext.RequestAborted);
        var publishStatuses = await publishTrackerRepo.GetPublishStatusesAsync(md5s, HttpContext.RequestAborted);
        
        // Map to components and store ShortDetails
        var items = md5s
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m =>
            {
                var photo = photos.FirstOrDefault(p => p.Md5Hash == m);
                var links = imageLocator.GetImageLinks(m);
                // Prefer actual preview-2000 dimensions if available, otherwise use a safe fallback
                int pw = Math.Max(1, links?.P2000Width ?? 2000);
                int ph = Math.Max(1, links?.P2000Height ?? 1500);
                return new webapp.Components.GalleryItem
                {
                    FullUrl = Url.Action("ByMd5", "Images", new { md5 = m })!,
                    FullWidth = pw,
                    FullHeight = ph,
                    Alt = m!,
                    Md5 = m!,
                    ShortDetails = photo?.ShortDetails,
                    PublishPlatforms = publishStatuses.TryGetValue(m, out var platforms) ? platforms : Array.Empty<string>()
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
    public async Task<IActionResult> Search([FromQuery] string[]? extensions)
    {
        // Log invocation to verify this endpoint is being triggered
        logger.LogInformation(
            "[Search] endpoint invoked at {Timestamp} from {RemoteIp} with query {QueryString} and {KeyCount} keys",
            DateTimeOffset.Now,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty,
            Request.Query.Count);

        var effExtensions = extensions ?? Array.Empty<string>();
        ViewBag.AvailableExtensions = await searchResultsRepo.GetAllDistinctExtensionsAsync(HttpContext.RequestAborted);
        ViewBag.SelectedExtensions = effExtensions;

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

        // Commerce rating filter normalization with default 4,5
        int[] commerceRatings;
        if (Request.Query.ContainsKey("commerceRatings"))
        {
            commerceRatings = Request.Query["commerceRatings"]
                .Select(s => int.TryParse(s, out var v) ? (int?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToArray();
        }
        else
        {
            commerceRatings = [4, 5];
        }
        route["commerceRatings"] = commerceRatings;

        // Ordering normalization: score (default) or commerce
        var orderByRaw = Request.Query["orderBy"].ToString();
        var orderBy = string.Equals(orderByRaw, "commerce", StringComparison.OrdinalIgnoreCase)
            ? ResultsOrderBy.CommerceDesc
            : ResultsOrderBy.ScoreDesc;
        route["orderBy"] = orderBy == ResultsOrderBy.CommerceDesc ? "commerce" : "score";
        var normalizedPageSize = route["pageSize"];
        logger.LogInformation(
            "[Search] normalized state -> pageSize: {PageSize}, size: {Size}, orderBy: {OrderBy}, page reset to 1",
            normalizedPageSize, normalizedSize, route["orderBy"]);

        // Normalize typed values for the view to avoid dynamic cast issues
        var pageSizeInt = int.TryParse(route["pageSize"]?.ToString(), out var psVal) ? psVal : 12;
        var sizeInt = int.TryParse(route["size"]?.ToString(), out var szVal) ? szVal : 256;
        sizeInt = sizeInt.SnapToAllowed();

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
                var byId = await searchResultsRepo.GetResultsBySessionIdAsync(requestedSessionId, null, orderBy, HttpContext.RequestAborted);
                if (byId != null)
                {
                    route["query"] = byId.QueryText;
                    route["sessionId"] = byId.SessionId;
                    return RedirectToAction("Search", route);
                }
            }

            // If no query and no session to restore, just show the search page with empty results/form
            ViewBag.AvailableTags = Array.Empty<string>();
            ViewBag.AvailablePersons = Array.Empty<string>();
            ViewBag.GalleryItems = new List<webapp.Components.GalleryItem>();
            ViewBag.SearchResults = new List<webapp.Services.SearchResultRow>();
            ViewBag.Total = 0;
            ViewBag.Page = pageFromQuery;
            ViewBag.PageSize = pageSizeInt;
            ViewBag.Size = sizeInt;
            ViewBag.Query = string.Empty;
            ViewBag.MinScore = minScoreVal;
            ViewBag.CommerceRatings = commerceRatings;
            ViewBag.OrderBy = route["orderBy"];

            return View();
        }
        
        var tagsValues = Request.Query["tags"].ToString();
        var tags = tagsValues.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var personsValues = Request.Query["persons"].ToString();
        var persons = personsValues.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var searchExtensions = Request.Query["extensions"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);

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
            var byId = await searchResultsRepo.GetResultsBySessionIdAsync(requestedSessionId, null, orderBy, HttpContext.RequestAborted);
            if (byId != null && string.Equals(byId.QueryText, queryText, StringComparison.Ordinal))
            {
                sessionToUse = byId;
            }
            else
            {
                // Provided session is missing or belongs to a different query -> run a new search and stick to new latest
                int exitCode = await dockerSearchRunner.RunImageSearcherAsync(actionsPath, queryText, tags, persons, searchExtensions,
                    onStdout: s => logger.LogInformation("[image_searcher][stdout] {Line}", s),
                    onStderr: s => logger.LogWarning("[image_searcher][stderr] {Line}", s));
                if (exitCode != 0)
                {
                    logger.LogError("[Search] image_searcher exited with code {Code}", exitCode);
                    TempData["SearchError"] = $"Search failed with code {exitCode}";
                    return RedirectToAction("Index", route);
                }

                var latest = await searchResultsRepo.GetLatestResultsAsync(orderBy, HttpContext.RequestAborted);
                if (latest is null || latest.Results.Count == 0)
                {
                    TempData["SearchInfo"] = "No results found";
                    return RedirectToAction("Index", route);
                }

                // Redirect to same action but with new sessionId to stick
                route["sessionId"] = latest.SessionId;
                return RedirectToAction("Search", route);
            }
        }

        if (sessionToUse is null)
        {
            // No specific session requested; optionally run search on non-paging request
            if (!isPagingRequest)
            {
                int exitCode = await dockerSearchRunner.RunImageSearcherAsync(actionsPath, queryText, tags, persons, searchExtensions,
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
            var latest = await searchResultsRepo.GetLatestResultsAsync(orderBy, HttpContext.RequestAborted);
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
        pageSizeInt = int.TryParse(route["pageSize"]?.ToString(), out psVal) ? psVal : 12;
        sizeInt = int.TryParse(route["size"]?.ToString(), out szVal) ? szVal : 256;
        sizeInt = sizeInt.SnapToAllowed();
        var minScoreForFilter = (float)minScoreVal;
        var filteredResults = sessionToUse!.Results
            .GroupBy(g=>g.Group).Select(s=>s.First())
            .Where(r => commerceRatings.Contains(r.CommerceRating))
            .Where(r => r.Score >= minScoreForFilter)
            .ToList();

        // Pass results to the Index view directly for immediate display
        ViewBag.SelectedTags = route.TryGetValue("tags", out var t) ? t : Array.Empty<string>();
        ViewBag.SearchResults = filteredResults;
        // Build gallery items with real preview dimensions for PhotoSwipe
        var searchMd5s = filteredResults
            .Where(r => !string.IsNullOrWhiteSpace(r.Md5))
            .Select(r => r.Md5!)
            .ToList();
        var searchPublishStatuses = await publishTrackerRepo.GetPublishStatusesAsync(searchMd5s, HttpContext.RequestAborted);

        var galleryItems = searchMd5s
            .Select(m =>
            {
                var links = imageLocator.GetImageLinks(m);
                int pw = Math.Max(1, links?.P2000Width ?? 2000);
                int ph = Math.Max(1, links?.P2000Height ?? 1500);
                return new webapp.Components.GalleryItem
                {
                    FullUrl = Url.Action("ByMd5", "Images", new { md5 = m })!,
                    FullWidth = pw,
                    FullHeight = ph,
                    Alt = m,
                    Md5 = m,
                    PublishPlatforms = searchPublishStatuses.TryGetValue(m, out var platforms) ? platforms : Array.Empty<string>()
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
        // Load distinct persons for this session to populate persons selector
        try
        {
            var availablePersons = await searchResultsRepo.GetDistinctPersonsForSessionAsync(sessionToUse.SessionId, HttpContext.RequestAborted);
            ViewBag.AvailablePersons = availablePersons;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Search] Failed to load available persons for session {SessionId}", sessionToUse.SessionId);
            ViewBag.AvailablePersons = Array.Empty<string>();
        }
        ViewBag.Total = filteredResults.Count;
        ViewBag.Page = pageFromQuery; // reflect requested page
        ViewBag.PageSize = pageSizeInt; // strongly-typed int
        ViewBag.Size = sizeInt; // strongly-typed int
        ViewBag.Query = queryText;
        ViewBag.SessionId = sessionToUse.SessionId;
        ViewBag.MinScore = minScoreVal;
        ViewBag.CommerceRatings = commerceRatings;
        ViewBag.OrderBy = route["orderBy"];
        return View();
    }

    [HttpGet]
    public IActionResult SizeUp(
        [FromQuery] string? query,
        [FromQuery] string[]? tags,
        [FromQuery] int[]? commerceRatings,
        [FromQuery] string[]? filters,
        [FromQuery] string[]? sorting,
        [FromQuery] int? pageSize,
        [FromQuery] int? size)
    {
        var targetAction = string.IsNullOrWhiteSpace(query) ? "Index" : "Search";
        return RedirectToAction(targetAction, new
        {
            query,
            tags,
            commerceRatings,
            filters,
            sorting,
            pageSize = pageSize ?? 12,
            size = size ?? 256,
            page = 1
        });
    }

    [HttpGet]
    public IActionResult SizeDown(
        [FromQuery] string? query,
        [FromQuery] string[]? tags,
        [FromQuery] int[]? commerceRatings,
        [FromQuery] string[]? filters,
        [FromQuery] string[]? sorting,
        [FromQuery] int? pageSize,
        [FromQuery] int? size)
    {
        var targetAction = string.IsNullOrWhiteSpace(query) ? "Index" : "Search";
        return RedirectToAction(targetAction, new
        {
            query,
            tags,
            commerceRatings,
            filters,
            sorting,
            pageSize = pageSize ?? 12,
            size = size ?? 256,
            page = 1
        });
    }

    [HttpGet]
    public IActionResult RankUp(
        [FromQuery] string? query,
        [FromQuery] string[]? tags,
        [FromQuery] int[]? commerceRatings,
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
        var targetAction = string.IsNullOrWhiteSpace(query) ? "Index" : "Search";
        return RedirectToAction(targetAction, new
        {
            query,
            tags,
            commerceRatings,
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
        [FromQuery] int[]? commerceRatings,
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
        var targetAction = string.IsNullOrWhiteSpace(query) ? "Index" : "Search";
        return RedirectToAction(targetAction, new
        {
            query,
            tags,
            commerceRatings,
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

    public IActionResult ContentQualityValidator()
    {
        ViewBag.StoragePath = _storage.RootPath ?? string.Empty;
        return View();
    }

    public async Task<IActionResult> ContentQualityStatus()
    {
        var root = _storage.RootPath;
        var folders = (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            ? PathExtensions.GetStorageFolders(root).ToArray()
            : Array.Empty<string>();

        var latest = await contentValidationRepository.GetLatestAsync(HttpContext.RequestAborted);
        
        ViewBag.StoragePath = root ?? string.Empty;

        var vm = new ValidationStatusViewModel()
        {
            Items = latest.Select(s => new FolderStatus
            {
                Folder = s.Folder,
                TestKind = s.TestKind,
                Status = s.Status,
                TotalFailures = s.TotalFailures
            }).ToArray()
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> TruncateAnalysisResults()
    {
        await contentValidationRepository.TruncateAsync(HttpContext.RequestAborted);
        return RedirectToAction(nameof(ContentQualityStatus));
    }

    [HttpGet]
    public async Task<IActionResult> ContentQualityDetails([FromQuery] string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return BadRequest("folder is required");

        ViewBag.StoragePath = _storage.RootPath ?? string.Empty;

        var rows = await contentValidationRepository.GetLatestDetailsByFolderAsync(folder, HttpContext.RequestAborted);

        // Prepare pretty JSON once on server side
        static string? Pretty(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return json; // fallback to raw
            }
        }

        static ValidationDetailPayload? Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var opts = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var payload = JsonSerializer.Deserialize<ValidationDetailPayload>(json, opts);
                if (payload == null) return null;
                // Normalize null arrays to empty for easier rendering
                payload = new ValidationDetailPayload
                {
                    Total = payload.Total,
                    Mismatches = payload.Mismatches,
                    Failures = payload.Failures ?? Array.Empty<FailureItem>()
                };
                return payload;
            }
            catch
            {
                return null;
            }
        }

        var vm = new ValidationDetailsViewModel
        {
            Folder = folder,
            Items = rows.Select(r => new ValidationDetailItem
            {
                TestKind = r.TestKind,
                Status = r.Status,
                Details = Pretty(r.DetailsJson),
                Parsed = Parse(r.DetailsJson)
            }).OrderBy(i => i.TestKind).ToList()
        };

        return View(vm);
    }

    [HttpGet("/api/storage/folders")]
    public IActionResult GetStorageFolders()
    {
        var root = _storage.RootPath;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return Ok(Array.Empty<string>());

        var folders = PathExtensions.GetStorageFolders(root).ToArray();
        return Ok(folders);
    }

    public IActionResult Images()
    {
        ViewBag.StoragePath = _storage.RootPath ?? string.Empty;
        ViewBag.InputPath = _storage.InputPath ?? string.Empty;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> SinglePhoto([FromQuery] string md5)
    {
        if (string.IsNullOrWhiteSpace(md5)) return BadRequest("MD5 is required");

        var photo = await searchResultsRepo.GetPhotoInfoByMd5Async(md5, HttpContext.RequestAborted);
        if (photo == null) return NotFound();

        var realPath = await imageLocationRepository.GetPathByMd5Async(md5, HttpContext.RequestAborted);
        var largeDetails = realPath != null ? await fileSystem.GetDqAnswer(realPath) : string.Empty;
        var commerceMarkJson = realPath != null ? await fileSystem.GetCommerceMarkAnswer(realPath) : "{}";

        var links = imageLocator.GetImageLinks(md5);
        var publishStatuses = await publishTrackerRepo.GetPublishStatusesAsync([md5], HttpContext.RequestAborted);
        var item = new SelectedItemViewModel
        {
            Md5 = md5,
            ShortDetails = photo.ShortDetails,
            LargeDetails = largeDetails,
            Tags = photo.Tags ?? Array.Empty<string>(),
            ImageUrl = Url.Action("ByMd5", "Images", new { md5 = md5, w = 128 })!,
            RealUrl = links?.Real ?? string.Empty,
            CommerceMark = commerceMarkJson.ThisJsonAs<ImageProcessingExtensions.RateExplanation>().rate.ToString(),
            ImprovementWays = commerceMarkJson.ThisJsonAs<ImageProcessingExtensions.RateExplanation>().rateExplanation,
            Width = links?.P2000Width,
            Height = links?.P2000Height,
            PublishPlatforms = publishStatuses.TryGetValue(md5, out var platforms) ? platforms : Array.Empty<string>()
        };

        var vm = new SinglePhotoViewModel
        {
            Photo = item
        };

        if (!string.IsNullOrEmpty(photo.GroupName))
        {
            var similar = await searchResultsRepo.GetPhotosByGroupAsync(photo.GroupName, HttpContext.RequestAborted);
            foreach (var s in similar)
            {
                if (s.Md5Hash == md5) continue;

                var sLinks = imageLocator.GetImageLinks(s.Md5Hash);
                vm.SimilarPhotos.Add(new SelectedItemViewModel
                {
                    Md5 = s.Md5Hash,
                    ShortDetails = s.ShortDetails,
                    Tags = s.Tags ?? Array.Empty<string>(),
                    ImageUrl = Url.Action("ByMd5", "Images", new { md5 = s.Md5Hash, w = 128 })!,
                    RealUrl = sLinks?.Real ?? string.Empty,
                    Width = sLinks?.P2000Width,
                    Height = sLinks?.P2000Height
                });
            }
        }

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Selected([FromQuery] Guid sessionId)
    {
        var items = await selectionRepo.GetSelectedMd5Async(sessionId, HttpContext.RequestAborted);
        var md5s = items.Select(i => i.Md5).ToArray();
        var publishStatuses = await publishTrackerRepo.GetPublishStatusesAsync(md5s, HttpContext.RequestAborted);

        var vm = new SelectedViewModel
        {
            SessionId = sessionId,
            Items = items.Select(i => {
                var links = imageLocator.GetImageLinks(i.Md5);
                return new SelectedItemViewModel
                {
                    Md5 = i.Md5,
                    ShortDetails = i.ShortDetails,
                    LargeDetails = i.LargeDetails,
                    Tags = i.Tags ?? Array.Empty<string>(),
                    ImageUrl = Url.Action("ByMd5", "Images", new { md5 = i.Md5, w = 128 })!,
                    RealUrl = links?.Real ?? string.Empty,
                    CommerceMark = i.CommerceMark.ThisJsonAs<ImageProcessingExtensions.RateExplanation>().rate.ToString(),
                    ImprovementWays = i.CommerceMark.ThisJsonAs<ImageProcessingExtensions.RateExplanation>().rateExplanation,
                    Width = links?.P2000Width,
                    Height = links?.P2000Height,
                    PublishPlatforms = publishStatuses.TryGetValue(i.Md5, out var platforms) ? platforms : Array.Empty<string>()
                };
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> ClearSelected([FromQuery] Guid sessionId)
    {
        await selectionRepo.ClearSelectionAsync(sessionId, HttpContext.RequestAborted);
        return RedirectToAction("Selected", new { sessionId });
    }

    public IActionResult About()
    {
        return View();
    }

    [HttpPost]
    public IActionResult IndexJob(
        [FromForm] string jobId,
        [FromForm] JobType type,
        [FromForm] int? dop,
        [FromForm] string? testKind,
        [FromForm] string? level1,
        [FromForm] string? level2)
    {
        if (string.IsNullOrWhiteSpace(jobId)) return BadRequest("jobId is required");
        var rootPath = _storage.RootPath;
        if (string.IsNullOrWhiteSpace(rootPath)) return BadRequest("Storage root path is not configured");
        var id = jobRunner.StartJob(jobId, type, rootPath, dop, testKind, level1, level2);
        return Ok(new { jobId = id });
    }

    [HttpPost]
    public IActionResult AddImagesJob([FromForm] string jobId, [FromForm] JobType type, [FromForm] int? dop)
    {
        if (string.IsNullOrWhiteSpace(jobId)) return BadRequest("jobId is required");
        var inputPath = _storage.InputPath;
        if (string.IsNullOrWhiteSpace(inputPath)) return BadRequest("Input path is not configured");
        var id = jobRunner.StartJob(jobId, type, inputPath, dop);
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

    [HttpGet]
    public async Task<IActionResult> Selections([FromQuery] int? page, [FromQuery] int? pageSize)
    {
        var p = Math.Max(1, page ?? 1);
        var ps = Math.Max(1, pageSize ?? 20);
        var offset = (p - 1) * ps;

        var (items, total) = await selectionRepo.GetRecentSelectionSessionsAsync(offset, ps, HttpContext.RequestAborted);

        ViewBag.Total = total;
        ViewBag.Page = p;
        ViewBag.PageSize = ps;

        return View(items);
    }

    [HttpPost]
    public async Task<IActionResult> SaveSelection([FromQuery] Guid sessionId, [FromForm] string name)
    {
        if (string.IsNullOrWhiteSpace(name)) name = $"Selection {DateTime.Now:yyyy-MM-dd HH:mm}";
        var newSessionId = await selectionRepo.CreateSelectionSessionAsync(sessionId, name, HttpContext.RequestAborted);
        return RedirectToAction("Selected", new { sessionId = newSessionId });
    }
}