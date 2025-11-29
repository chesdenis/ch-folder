using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using shared_csharp;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace meta_uploader;

public class EmbeddingUploader
{
    private readonly IFileSystem _fileSystem;
    private readonly IFileHasher _fileHasher;
    private readonly string _connectionString;
    private const string Collection = "photos";
    private const int VectorSize = 1536;

    public EmbeddingUploader(IFileSystem fileSystem, IFileHasher fileHasher)
    {
        _fileSystem = fileSystem;
        _fileHasher = fileHasher;
        _connectionString = Environment.GetEnvironmentVariable("QDRANT_CS")
                            ?? throw new ArgumentNullException("QDRANT_CS");
    }

    public async Task RunAsync(string[] args)
    {
        using var http = new HttpClient { BaseAddress = new Uri(_connectionString) };
        await EnsureCollection(http, Collection, VectorSize);
        
        args = args.ValidateArgs();
        await _fileSystem.WalkThrough(args, (p)=> ProcessSingleFile(p, http));
    }

    private async Task ProcessSingleFile(string filePath, HttpClient http)
    {
        if (!filePath.AllowImageToProcess())
        {
            return;
        }
        
        var groupName = filePath.GetGroupName();
        var md5 = await _fileHasher.ComputeMd5Async(filePath);
        
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var descriptionFolder = Path.Combine(directoryName, "dq");
        var embeddingPath = Path.Combine(descriptionFolder, $"{groupName}.dq.emb.json");
        var descriptionPath = Path.Combine(descriptionFolder, $"{groupName}.dq.md.answer.md");

        var embeddingContent = await File.ReadAllTextAsync(embeddingPath);
        var descriptionContent = await File.ReadAllTextAsync(descriptionPath);
        
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var item = JsonSerializer.Deserialize<EmbeddingFile>(embeddingContent, options);
        if (item?.data == null || item.data.Count == 0)
        {
            Console.WriteLine($"Skipping (no embedding): {filePath}");
            return;
        }
        
        if (item?.data[0].embedding == null || item.data[0].embedding.Count == 0)
        {
            Console.WriteLine($"Skipping (no embedding) data: {filePath}");
            return;
        }
        
        var payload = new Dictionary<string, object>
        {
            ["path"] = md5,
            ["text"] = descriptionContent,
        };
        
        var req = new UpsertPointsRequest([
            new(md5, item.data[0].embedding.ToArray(), payload)
        ]);
        
        var json = JsonSerializer.Serialize(req);
        var resp = await http.PutAsync($"/collections/{Collection}/points?wait=true",
            new StringContent(json, Encoding.UTF8, "application/json"));
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"Upsert error [{resp.Content}]: {body}");
        }
        else
        {
            Console.WriteLine($"Upserted for {filePath}");
        }

        Console.WriteLine($"File {filePath} processed successfully.");
    }
    
    static async Task EnsureCollection(HttpClient http, string collection, int dim)
    {
        var get = await http.GetAsync($"/collections/{collection}");
        if (get.IsSuccessStatusCode) return;

        var create = new
        {
            vectors = new { size = dim, distance = "Cosine" }
        };
        var json = JsonSerializer.Serialize(create);
        var resp = await http.PutAsync($"/collections/{collection}",
            new StringContent(json, Encoding.UTF8, "application/json"));
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new Exception($"Create collection failed: {resp.StatusCode} {body}");
        }

        Console.WriteLine($"Created collection '{collection}'");
    }
    
    record UpsertPointsRequest(PointStruct[] points);

    record PointStruct(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("vector")] float[] Vector,
        [property: JsonPropertyName("payload")]
        Dictionary<string, object> Payload
    );
}