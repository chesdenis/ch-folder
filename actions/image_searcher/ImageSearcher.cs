using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using shared_csharp;
using shared_csharp.Abstractions;

namespace image_searcher;

public class ImageSearcher
{
    private const string Collection = "photos";
    
    private readonly IFileSystem _fileSystem;
    private readonly IFileHasher _fileHasher;
    
    private readonly string _qdrantConnectionString;
    private readonly string _openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
                                      ?? throw new ArgumentNullException(nameof(_openAiKey));

    private const string Url = "https://api.openai.com/v1/embeddings";
    private const string Model = "text-embedding-ada-002";

    public ImageSearcher(IFileSystem fileSystem, IFileHasher fileHasher)
    {
        _fileSystem = fileSystem;
        _fileHasher = fileHasher;
        
        _qdrantConnectionString = $"http://{Environment.GetEnvironmentVariable("QD_HOST")}:{Environment.GetEnvironmentVariable("QD_PORT")}";
    }

    public async Task RunAsync(string[] args)
    {
        string queryText = args[0];
        
        var response = await GetOpenAiEmbedding(queryText);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var queryEmbedding = JsonSerializer.Deserialize<EmbeddingFile>(response, options);
        if (queryEmbedding?.data == null || queryEmbedding.data.Count == 0)
        {
            return;
        }
        
        var queryVector = queryEmbedding.data[0].embedding.ToArray();

        using var http = new HttpClient { BaseAddress = new Uri(_qdrantConnectionString) };

        // Build optional pre-filter from args
        var filter = BuildFilter(args.Skip(1).ToArray());

        var req = new SearchRequest(
            Vector: queryVector,
            Limit: 100,
            WithPayload: true,
            ScoreThreshold: null,
            Filter: filter
        );

        var body = JsonSerializer.Serialize(req);
        var resp = await http.PostAsync($"/collections/{Collection}/points/search",
            new StringContent(body, Encoding.UTF8, "application/json"));
        
        if (!resp.IsSuccessStatusCode)
        {
            Console.WriteLine($"Search failed: {resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
            return;
        }
        
        var resultJson = await resp.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<SearchResultWrapper>(resultJson);
        if (result?.Result == null || result.Result.Length == 0)
        {
            Console.WriteLine("No matches.");
            return;
        }
        
        foreach (var r in result.Result)
        {
            var path = r.Payload != null && r.Payload.TryGetValue("path", out var pEl) &&
                       pEl.ValueKind == JsonValueKind.String
                ? pEl.GetString()
                : "(no path)";

            if (r.Payload != null)
            {
                Console.WriteLine($"{r.Score:F4}  {path}, {string.Join("-->", r.Payload.Values.ToArray())}");
            }
        }
    }
    
    private static Filter? BuildFilter(string[] args)
    {
        if (args.Length == 0) return null;

        var must = new List<Condition>();

        string? GetArg(string key)
            => args.FirstOrDefault(a => a.StartsWith($"--{key}=", StringComparison.OrdinalIgnoreCase))?
                    .Split('=', 2)[1];

        // Note: tenant/visibility/album/nsfw filters were removed as they are not defined in payload

        // New specific fields requested
        var eventName = GetArg("eventName");
        if (!string.IsNullOrWhiteSpace(eventName))
            must.Add(new Condition("eventName", new Match(Value: eventName, Any: null, Text: null), null));

        var yearName = GetArg("yearName");
        if (!string.IsNullOrWhiteSpace(yearName))
            must.Add(new Condition("yearName", new Match(Value: yearName, Any: null, Text: null), null));

        var eng30TagsParam = GetArg("eng30TagsData");
        if (!string.IsNullOrWhiteSpace(eng30TagsParam))
        {
            var tags = eng30TagsParam.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tags.Length > 0)
                must.Add(new Condition("eng30TagsData", new Match(Value: null, Any: tags.Cast<object>().ToArray(), Text: null), null));
        }

        // Commerce rate (flattened from commerceData.rate)
        var commerceRateEq = GetArg("commerceRate");
        if (!string.IsNullOrWhiteSpace(commerceRateEq) && int.TryParse(commerceRateEq, out var crEq))
            must.Add(new Condition("commerceRate", new Match(Value: crEq, Any: null, Text: null), null));

        var commerceRateGte = GetArg("commerceRateGte");
        var commerceRateLte = GetArg("commerceRateLte");
        double? crGte = null, crLte = null;
        if (!string.IsNullOrWhiteSpace(commerceRateGte) && double.TryParse(commerceRateGte, out var g)) crGte = g;
        if (!string.IsNullOrWhiteSpace(commerceRateLte) && double.TryParse(commerceRateLte, out var l)) crLte = l;
        if (crGte.HasValue || crLte.HasValue)
            must.Add(new Condition("commerceRate", null, new Range(Gt: null, Gte: crGte, Lt: null, Lte: crLte)));

        // Tags: comma-separated, uses match.any against array payload
        var tagsStr = GetArg("tags");
        if (!string.IsNullOrWhiteSpace(tagsStr))
        {
            var tags = tagsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tags.Length > 0)
                must.Add(new Condition("tags", new Match(Value: null, Any: tags.Cast<object>().ToArray(), Text: null), null));
        }

        // Date range args (date_from/date_to) were intentionally removed — year-based filtering via yearName is sufficient

        // Full-text filter on "text" field (requires text index in Qdrant)
        var textQuery = GetArg("text");
        if (!string.IsNullOrWhiteSpace(textQuery))
            must.Add(new Condition("text", new Match(Value: null, Any: null, Text: textQuery), null));

        if (must.Count == 0) return null;
        return new Filter(Must: must.ToArray(), Should: null, MustNot: null);
    }

    private async Task<string> GetOpenAiEmbedding(string input)
    {
        using var client = new HttpClient();
        
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAiKey}");

        var requestBody = new
        {
            input,
            model = Model,
            encoding_format = "float"
        };

        var jsonBody = JsonSerializer.Serialize(requestBody);
        using var data = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(Url, data);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync();
        }

        var errorResponse = await response.Content.ReadAsStringAsync();
        throw new Exception($"Error: {response.StatusCode}, Response: {errorResponse}");
    }
}

record SearchRequest(
    [property: JsonPropertyName("vector")] float[] Vector,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("with_payload")] bool WithPayload,
    [property: JsonPropertyName("score_threshold")] float? ScoreThreshold,
    [property: JsonPropertyName("filter")] Filter? Filter
);

public record Filter(
    [property: JsonPropertyName("must")] Condition[]? Must,
    [property: JsonPropertyName("should")] Condition[]? Should,
    [property: JsonPropertyName("must_not")] Condition[]? MustNot
);

public record Condition(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("match")] Match? Match,
    [property: JsonPropertyName("range")] Range? Range
);

public record Match(
    [property: JsonPropertyName("value")] object? Value,
    [property: JsonPropertyName("any")] object[]? Any,
    [property: JsonPropertyName("text")] string? Text
);

public record Range(
    [property: JsonPropertyName("gt")] double? Gt,
    [property: JsonPropertyName("gte")] double? Gte,
    [property: JsonPropertyName("lt")] double? Lt,
    [property: JsonPropertyName("lte")] double? Lte
);

record SearchResultWrapper(
    [property: JsonPropertyName("result")] SearchResultItem[] Result
);

record SearchResultItem(
    [property: JsonPropertyName("id")] JsonElement Id,
    [property: JsonPropertyName("score")] float Score,
    [property: JsonPropertyName("payload")]
    Dictionary<string, JsonElement>? Payload
);