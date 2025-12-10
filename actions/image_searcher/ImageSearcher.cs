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

        var req = new SearchRequest(
            Vector: queryVector,
            Top: 100,
            WithPayload: true,
            ScoreThreshold: null
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
    [property: JsonPropertyName("top")] int Top,
    [property: JsonPropertyName("with_payload")]
    bool WithPayload,
    [property: JsonPropertyName("score_threshold")]
    float? ScoreThreshold
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