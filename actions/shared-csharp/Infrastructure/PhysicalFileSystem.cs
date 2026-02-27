using System.Text;
using Newtonsoft.Json;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace shared_csharp.Infrastructure;

public class PhysicalFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateFiles(path, searchPattern, searchOption);
    
    public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateDirectories(path, searchPattern, searchOption);
    
    public async Task<FileMetadata?> GetMetadata(string filePath)
    {
        var metadataPath = filePath.GetMetadataPath();
        if (!File.Exists(metadataPath)) return null;
        
        var content = await File.ReadAllTextAsync(metadataPath);
        return JsonConvert.DeserializeObject<FileMetadata>(content);
    }

    public async Task<string> GetGroup(string filePath) => (await GetMetadata(filePath))?.Group;

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

    public async Task<string> GetEmbAnswer(string filePath) => Decode((await GetMetadata(filePath))?.EmbAnswer);
    public async Task<string> GetDqAnswer(string filePath) => Decode((await GetMetadata(filePath))?.DqAnswer);
    public async Task<string> GetCommerceMarkAnswer(string filePath) => Decode((await GetMetadata(filePath))?.CommerceMarkAnswer);
    public async Task<string> GetEngShortAnswer(string filePath) => Decode((await GetMetadata(filePath))?.EngShortAnswer);

    public async Task<string[]> GetEng30Tags(string filePath)
    {
        var metadata = await GetMetadata(filePath);
        var tags = Decode(metadata?.Eng30TagsAnswer);
        if (string.IsNullOrEmpty(tags)) return Array.Empty<string>();
        return tags.Split(',').Select(s => s.Trim()).ToArray();
    }

    public async Task<CommerceJson?> GetCommerceMarkAnswerJson(string filePath)
    {
        var metadata = await GetMetadata(filePath);
        var json = Decode(metadata?.CommerceMarkAnswer);
        if (string.IsNullOrEmpty(json)) return null;
        return JsonConvert.DeserializeObject<CommerceJson>(json);
    }
}