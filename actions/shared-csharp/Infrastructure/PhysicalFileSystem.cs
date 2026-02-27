using System.Text;
using Newtonsoft.Json;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace shared_csharp.Infrastructure;

public class PhysicalFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool DirectoryIsEmpty(string path) => !EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly).Any();
    public bool FileExists(string path) => File.Exists(path);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateFiles(path, searchPattern, searchOption);
    
    public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateDirectories(path, searchPattern, searchOption);

    public void MoveFile(string sourceFileName, string destFileName) => File.Move(sourceFileName, destFileName);
    
    public void CopyFile(string sourceFileName, string destFileName, bool overwrite) => File.Copy(sourceFileName, destFileName, overwrite);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public async Task<FileMetadata?> GetMetadata(string filePath)
    {
        var metadataPath = filePath.GetMetadataPath();
        if (!File.Exists(metadataPath)) return null;
        
        var content = await File.ReadAllTextAsync(metadataPath);
        return JsonConvert.DeserializeObject<FileMetadata>(content);
    }

    public async Task<string> GetPartition(string filePath) => Decode((await GetMetadata(filePath))?.Partition);
    public async Task<string> GetSection(string filePath) => Decode((await GetMetadata(filePath))?.Section);
    public async Task<string> GetGroup(string filePath) => Decode((await GetMetadata(filePath))?.Group);

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
    public async Task<string> GetEmbConversation(string filePath) => Decode((await GetMetadata(filePath))?.EmbConversation);
    public async Task<string> GetDqQuestion(string filePath) => Decode((await GetMetadata(filePath))?.DqQuestion);
    public async Task<string> GetCommerceMarkQuestion(string filePath) => Decode((await GetMetadata(filePath))?.CommerceMarkQuestion);
    public async Task<string> GetEng30TagsQuestion(string filePath) => Decode((await GetMetadata(filePath))?.Eng30TagsQuestion);
    public async Task<string> GetEngShortQuestion(string filePath) => Decode((await GetMetadata(filePath))?.EngShortQuestion);
    public async Task<string> GetDqAnswer(string filePath) => Decode((await GetMetadata(filePath))?.DqAnswer);
    public async Task<string> GetCommerceMarkAnswer(string filePath) => Decode((await GetMetadata(filePath))?.CommerceMarkAnswer);
    public async Task<string> GetEng30TagsAnswer(string filePath) => Decode((await GetMetadata(filePath))?.Eng30TagsAnswer);
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

    public async Task<string> GetDqConversation(string filePath) => Decode((await GetMetadata(filePath))?.DqConversation);
    public async Task<string> GetCommerceMarkConversation(string filePath) => Decode((await GetMetadata(filePath))?.CommerceMarkConversation);
    public async Task<string> GetEng30TagsConversation(string filePath) => Decode((await GetMetadata(filePath))?.Eng30TagsConversation);
    public async Task<string> GetEngShortConversation(string filePath) => Decode((await GetMetadata(filePath))?.EngShortConversation);
}