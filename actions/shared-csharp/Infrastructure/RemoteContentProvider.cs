using System.Collections.Concurrent;
using System.Text;
using Newtonsoft.Json;
using shared_csharp.Abstractions;

namespace shared_csharp.Infrastructure;

public class RemoteContentProvider : IContentProvider
{
    private readonly IHttpClientFactory _factory;
    private readonly HttpClient _client;

    private readonly ConcurrentDictionary<string, Tuple<FileMetadata[], DateTime>> _cache = new();

    public RemoteContentProvider(IHttpClientFactory factory, string baseRemoteUrl)
    {
        _factory = factory;
        _client = _factory.CreateClient("PhysicalContentProvider");
        _client.BaseAddress = new Uri(baseRemoteUrl);
        _client.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<string> GetEngShortConversation(string md5) =>  Decode((await GetMetadataByMd5(md5))?.EngShortConversation);

    public async Task<string[]> GetFilePointers(uint partition)
    {
        var fields = new HashSet<string>
        {
            "md5",
        };
        var requestUri = $"/meta-range/{partition}?{string.Join("&", fields.Select(s => $"fields={s}"))}";
        var response = await _client.GetAsync(requestUri);
        Console.WriteLine($"Executing {requestUri}");
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Got response {responseContent.Length}");
        var metadata = JsonConvert.DeserializeObject<FileMetadata[]>(responseContent) ??
                       throw new InvalidOperationException();
        var filePointers = metadata.Select(s => s.Md5).ToArray();
        Console.WriteLine($"Found {filePointers.Length}");
        
        return filePointers;
    }

    public async Task<FileMetadata?> GetMetadataByMd5(string md5)
    {
        var key = md5; 
        var now = DateTime.UtcNow;
        
        while (true)
        {
            if (_cache.TryGetValue(key, out var existing))
            {
                if (existing.Item2 > now)
                {
                    return existing.Item1.FirstOrDefault(); // cached hit (may still be in-flight)
                }
              

                // expired: try to remove; if remove fails, someone else replaced it—loop and re-check
                _cache.TryRemove(key, out _);
                continue;
            }

            var fields = new HashSet<string>
            {
                "partition",
                "section",
                "group",
                "averageHash",
                "colorHash",
                "description",
                "tags",
                "embAnswer",
                "embConversation",
                "dqQuestion",
                "dqAnswer",
                "dqConversation",
                "commerceMarkQuestion",
                "commerceMarkAnswer",
                "commerceMarkConversation",
                "eng30TagsQuestion",
                "eng30TagsAnswer",
                "eng30TagsConversation",
                "engShortQuestion",
                "engShortAnswer",
                "engShortConversation",
                "ext",
                "md5"
            };

            var response = await _client.GetAsync($"/meta/{md5}?{string.Join("&", fields.Select(s => $"fields={s}"))}");
            var responseContent = await response.Content.ReadAsStringAsync();
            var results = JsonConvert.DeserializeObject<FileMetadata[]>(responseContent);
            now = DateTime.UtcNow;
            var nowPlusTTl = now.AddSeconds(30);
            if (_cache.TryAdd(key, new Tuple<FileMetadata[], DateTime>(results, nowPlusTTl)))
                return results?.FirstOrDefault();
        }
    }

    public async Task<string> GetGroup(string md5) => (await GetMetadataByMd5(md5))?.Group;
    public async Task<string> GetAverageHash(string md5) => (await GetMetadataByMd5(md5))?.AverageHash;

    public async Task<string> GetColorHash(string md5) => (await GetMetadataByMd5(md5))?.ColorHash;

    public async Task<string> GetDescription(string md5) => Decode((await GetMetadataByMd5(md5))?.Description);

    public async Task<string[]> GetTags(string md5) => (await GetMetadataByMd5(md5))?.Tags?.ToArray() ?? Array.Empty<string>();

    public async Task<string> GetEmbAnswer(string md5) => Decode((await GetMetadataByMd5(md5))?.EmbAnswer);
    public async Task<string> GetEmbConversation(string md5) => Decode((await GetMetadataByMd5(md5))?.EmbConversation);

    public async Task<string> GetDqAnswer(string md5) => Decode((await GetMetadataByMd5(md5))?.DqAnswer);
    public async Task<string> GetDqQuestion(string md5) => Decode((await GetMetadataByMd5(md5))?.DqQuestion);
    public async Task<string> GetDqConversation(string md5) => Decode((await GetMetadataByMd5(md5))?.DqConversation);
    public async Task<string> GetCommerceMarkQuestion(string md5) => Decode((await GetMetadataByMd5(md5))?.CommerceMarkQuestion);

    public async Task<string> GetCommerceMarkAnswer(string md5) => Decode((await GetMetadataByMd5(md5))?.CommerceMarkAnswer);
    public async Task<string> GetCommerceMarkConversation(string md5) => Decode((await GetMetadataByMd5(md5))?.CommerceMarkConversation);

    public async Task<string> GetEng30TagsQuestion(string md5) => Decode((await GetMetadataByMd5(md5))?.Eng30TagsQuestion);

    public async Task<string> GetEng30TagsAnswer(string md5) => Decode((await GetMetadataByMd5(md5))?.Eng30TagsAnswer);

    public async Task<string> GetEng30TagsConversation(string md5) => Decode((await GetMetadataByMd5(md5))?.Eng30TagsConversation);

    public async Task<string> GetEngShortQuestion(string md5) => Decode((await GetMetadataByMd5(md5))?.EngShortQuestion);

    public async Task<string> GetEngShortAnswer(string md5) => Decode((await GetMetadataByMd5(md5))?.EngShortAnswer);

    public async Task<string[]> GetEng30Tags(string md5)
    {
        var metadata = await GetMetadataByMd5(md5);
        var tags = Decode(metadata?.Eng30TagsAnswer);
        if (string.IsNullOrEmpty(tags)) return Array.Empty<string>();
        return tags.Split(',').Select(s => s.Trim()).ToArray();
    }

    public async Task<CommerceJson?> GetCommerceMarkAnswerJson(string md5)
    {
        var metadata = await GetMetadataByMd5(md5);
        var json = Decode(metadata?.CommerceMarkAnswer);
        if (string.IsNullOrEmpty(json)) return null;
        return JsonConvert.DeserializeObject<CommerceJson>(json);
    }

    public async Task<string> GetExtension(string md5)
    {
        var metadata = await GetMetadataByMd5(md5);
        return Decode(metadata?.Ext);
    }

    public async Task<FileMetadata?> GetMetadataWithPreviews(string md5)
    {
        var response = await _client.GetAsync($"/previews/{md5}");
        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<FileMetadata[]>(responseContent)?.FirstOrDefault();
    }

    public async Task<byte[]> GetReal(string md5)
    {
        var response = await _client.GetAsync($"/files/{md5}");
        var responseContent = await response.Content.ReadAsByteArrayAsync();

        return responseContent;
    }

    public async Task<string> GetSection(string md5)
    {
        var metadata = await GetMetadataByMd5(md5);
        return Decode(metadata?.Section);
    }

    public async Task<string> GetPartition(string md5)
    {
        var metadata = await GetMetadataByMd5(md5);
        return Decode(metadata?.Partition);
    }

    private string Decode(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return string.Empty;
        try 
        {
            var bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }
        catch 
        {
            return base64; // Fallback if not base64
        }
    }

}