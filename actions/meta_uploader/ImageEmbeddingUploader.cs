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
        if (!filePath.AllowImageToProcess())
        {
            return;
        }
        
        var groupName = filePath.GetGroupName();
        var md5 = await fileHasher.ComputeMd5Async(filePath);
        
        var fileParentFolder = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        
        var embeddingFolder = Path.Combine(fileParentFolder, "emb");
        var descriptionFolder = Path.Combine(fileParentFolder, "dq");
        
        var embeddingPath = Path.Combine(embeddingFolder, $"{groupName}.emb.md.answer.md");
        var descriptionPath = Path.Combine(descriptionFolder, $"{groupName}.dq.md.answer.md");
        
        var commerceFolder = Path.Combine(fileParentFolder, "commerceMark");
        var eng30TagsFolder = Path.Combine(fileParentFolder, "eng30tags");
        
       
        //'/commerceMark/*.commerceMark.md.answer.md' - to get commerce mark with this format {rate, rate-explanation}
        //'/eng30tags/*.eng30tags.md.answer.md' - to get tags with this format tag1, tag2, tag3, ..
        // file location - first 2 folders, ... 

        var embeddingContent = await File.ReadAllTextAsync(embeddingPath);
        var descriptionContent = await File.ReadAllTextAsync(descriptionPath);
        
        var commerceRawContent = await File.ReadAllTextAsync(Path.Combine(commerceFolder, $"{groupName}.commerceMark.md.answer.md"));
        var eng30TagsRawContent = await File.ReadAllTextAsync(Path.Combine(eng30TagsFolder, $"{groupName}.eng30tags.md.answer.md"));
        var commerceData = JsonSerializer.Deserialize<ImageProcessingExtensions.RateExplanation>(commerceRawContent);
        var eng30TagsData = eng30TagsRawContent
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        var eventName = Path.GetFileName(fileParentFolder);
        var yearName = Path.GetFileName(Directory.GetParent(fileParentFolder)?.FullName);

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
                ["rate"] = commerceData?.rate ?? 0,
                ["rate-explanation"] = commerceData?.rateExplanation ?? string.Empty
            },
            ["commerceRate"] = commerceData?.rate ?? 0,
            ["commerceRateExplanation"] = commerceData?.rateExplanation ?? string.Empty,
            ["tags"] = eng30TagsData,
            ["eventName"] = eventName ?? string.Empty,
            ["yearName"] = yearName ?? string.Empty
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