using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using shared_csharp;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace meta_uploader;

public class ImageEmbeddingUploader(IFileSystem fileSystem, IFileHasher fileHasher)
{
    private readonly string _connectionString = $"http://{Environment.GetEnvironmentVariable("QD_HOST")}:{Environment.GetEnvironmentVariable("QD_PORT")}";
    private const string Collection = "photos";
    private const int BatchSize = 200;
    private readonly List<PointStruct> _buffer = new();

    public async Task RunAsync(string[] args)
    {
        using var http = new HttpClient { BaseAddress = new Uri(_connectionString) };
        
        args = args.ValidateArgs();
        await fileSystem.WalkThrough(args, (p)=> ProcessSingleFile(p, http));

        // flush remaining buffer
        if (_buffer.Count > 0)
        {
            await UpsertBatchAsync(http, _buffer);
            _buffer.Clear();
        }
    }

    private async Task ProcessSingleFile(string filePath, HttpClient http)
    {
        if (!filePath.AllowToProcess())
        {
            return;
        }
        
        if (filePath.IsVideo())
        {
            return;
        }
        
        var md5 = await fileHasher.ComputeMd5Async(filePath);
        
        var metadata = await fileSystem.GetMetadata(filePath);
        if (metadata == null) return;
        
        if (string.IsNullOrEmpty(metadata.EmbAnswer)) return;
        if (string.IsNullOrEmpty(metadata.EngShortAnswer)) return;
        if (string.IsNullOrEmpty(metadata.DqAnswer)) return;
        if (string.IsNullOrEmpty(metadata.CommerceMarkAnswer)) return;
        if (string.IsNullOrEmpty(metadata.Eng30TagsAnswer)) return;

        var embeddingContent = await fileSystem.GetEmbAnswer(filePath);
        var descriptionContent = await fileSystem.GetDqAnswer(filePath);
        
        var commerceData = await fileSystem.GetCommerceMarkAnswerJson(filePath);
        var eng30TagsData = await fileSystem.GetEng30Tags(filePath);
        string[] persons = Array.Empty<string>();
        try
        {
            persons = ImageProcessingExtensions.GetFacesOnPhotos(filePath);
        }
        catch
        {
            // ignore faces extraction errors
        }

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
            // New filterable fields
            ["commerceData"] = new Dictionary<string, object>
            {
                ["rate"] = commerceData?.Rate ?? 0,
                ["rate-explanation"] = commerceData?.RateExplanation ?? string.Empty
            },
            ["commerceRate"] = commerceData?.Rate ?? 0,
            ["commerceRateExplanation"] = commerceData?.RateExplanation ?? string.Empty,
            ["tags"] = eng30TagsData,
            ["persons"] = persons,
            ["eventName"] = metadata.Section ?? string.Empty,
            ["yearName"] = metadata.Partition ?? string.Empty
        };

        // add to buffer for batch upsert
        _buffer.Add(new PointStruct(md5, item.data[0].embedding.ToArray(), payload));
        if (_buffer.Count >= BatchSize)
        {
            await UpsertBatchAsync(http, _buffer);
            _buffer.Clear();
        }
    }
    
    private static async Task UpsertBatchAsync(HttpClient http, List<PointStruct> batch)
    {
        if (batch.Count == 0) return;
        Console.WriteLine($"Upserting {batch.Count} vectors to Qdrant...");
        var req = new UpsertPointsRequest(batch.ToArray());
        var json = JsonSerializer.Serialize(req);
        var resp = await http.PutAsync($"/collections/{Collection}/points?wait=true",
            new StringContent(json, Encoding.UTF8, "application/json"));
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"Upsert error: {resp.StatusCode} {body}");
        }
    }
    
    record UpsertPointsRequest(PointStruct[] points);

    record PointStruct(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("vector")] float[] Vector,
        [property: JsonPropertyName("payload")] Dictionary<string, object> Payload
    );
}