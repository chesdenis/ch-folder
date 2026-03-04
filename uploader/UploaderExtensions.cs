using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace uploader;

public static class UploaderExtensions
{
    private static readonly JsonSerializerOptions JsonPretty = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

    private sealed class ExistsResponse
    {
        public bool Exists { get; set; }
    }
    
    public static async Task<bool?> RemoteExistsAsync(HttpClient http, string baseUrl, string md5)
    {
        try
        {
            baseUrl = baseUrl.TrimEnd('/');
            var url = $"{baseUrl}/exists/{md5}";
            var resp = await http.GetFromJsonAsync<ExistsResponse>(url, JsonWeb).ConfigureAwait(false);
            return resp?.Exists;
        }
        catch
        {
            return null;
        }
    }
    
    public static string ToBase64(this string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    public static string ToBase64(this byte[] bytes) => Convert.ToBase64String(bytes);

    public static async Task AddMetadataIfAvailable(this Dictionary<string, object?> metadata, string key,
        string filePath, Func<string, Task<string>> getter, Func<string, string> pathResolver)
    {
        var path = pathResolver(filePath);
        if (File.Exists(path))
        {
            try
            {
                var content = await getter(filePath);
                metadata[key] = content.ToBase64();
            }
            catch
            {
                // Ignore errors reading individual metadata files
            }
        }
    }

    public static string[] GetNameParts(string filePath)
    {
        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        var averageHash = string.Empty;
        var colorHash = string.Empty;

        if (Path.GetFileNameWithoutExtension(filePath).Split("_").Length > 2)
        {
            if (Path.GetFileNameWithoutExtension(filePath).Split("_")[0].Length != 4)
            {
                averageHash = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
                colorHash = Path.GetFileNameWithoutExtension(filePath).Split("_")[1];
            }
            else
            {
                averageHash = Path.GetFileNameWithoutExtension(filePath).Split("_")[1];
                colorHash = Path.GetFileNameWithoutExtension(filePath).Split("_")[2];
            }
        }
        
        return [groupName, averageHash, colorHash];   
    }
    
    public static string GetPreview16Path(this string filePath) => filePath.GetPreviewPath("16");
    public static string GetPreview32Path(this string filePath) => filePath.GetPreviewPath("32");
    public static string GetPreview64Path(this string filePath) => filePath.GetPreviewPath("64");
    public static string GetPreview128Path(this string filePath) => filePath.GetPreviewPath("128");
    public static string GetPreview512Path(this string filePath) => filePath.GetPreviewPath("512");
    public static string GetPreview2000Path(this string filePath) => filePath.GetPreviewPath("2000");

    public static string GetPreviewPath(this string filePath, string previewKind)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var previewFolder = Path.Combine(directoryName, "preview");

        return Path.Combine(previewFolder, $"{Path.GetFileNameWithoutExtension(filePath)}_p{previewKind}.jpg");
    }
    
    public static async Task<string> GetEmbAnswer(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEmbAnswer(filePath));
    public static async Task<string> GetEmbConversation(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEmbConversation(filePath));
    public static async Task<string> GetDqQuestion(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveDqQuestionPath(filePath));
    public static async Task<string> GetCommerceMarkQuestion(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveCommerceMarkQuestionPath(filePath));
    public static async Task<string> GetEng30TagsQuestion(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEng30TagsQuestionPath(filePath));
    public static async Task<string> GetEngShortQuestion(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEngShortQuestionPath(filePath));
    public static async Task<string> GetDqAnswer(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveDqAnswerPath(filePath));
    public static async Task<string> GetCommerceMarkAnswer(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveCommerceMarkAnswerPath(filePath));
    public static async Task<string> GetEng30TagsAnswer(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEng30TagsAnswerPath(filePath));
    public static async Task<string> GetEngShortAnswer(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEngShortAnswerPath(filePath));
    public static async Task<string> GetDqConversation(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveDqConversationPath(filePath));
    public static async Task<string> GetCommerceMarkConversation(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveCommerceMarkConversationPath(filePath));
    public static async Task<string> GetEng30TagsConversation(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEng30TagsConversationPath(filePath));
    public static async Task<string> GetEngShortConversation(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEngShortConversationPath(filePath));

    public static async Task<Dictionary<string, object?>> CollectMetadataAsync(string filePath, string sourcePath)
    {
        var relativePath = Path.GetRelativePath(sourcePath, filePath);
        var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string partition = pathParts.Length > 1 ? pathParts[0] : "";
        string section = pathParts.Length > 2 ? pathParts[1] : "";

        var metadata = new Dictionary<string, object?>();
        metadata["partition"] = partition.ToBase64();
        metadata["section"] = section.ToBase64();
        
        metadata["group"] = GetNameParts(filePath)[0];
        metadata["averageHash"] = GetNameParts(filePath)[1];
        metadata["colorHash"] = GetNameParts(filePath)[2];

        // Description and Tags (as used in ImageEmbeddingUploader)
        if (File.Exists(PathExtensions.ResolveDqAnswerPath(filePath)))
        {
            metadata["description"] = (await GetDqAnswer(filePath)).ToBase64();
        }

        if (File.Exists(PathExtensions.ResolveEng30TagsAnswerPath(filePath)))
        {
            var tags =(await GetEng30TagsAnswer(filePath)).Split(',').Select(s => s.Trim()).ToArray();
        
            if (tags != null && tags.Length > 0)
            {
                metadata["tags"] = tags.Select(t => t.ToBase64()).ToArray();
            }
        }
        
        // Previews
        var previews = new Dictionary<string, string>();
        foreach (var size in ImageProcessingExtensions.AllowedSizes)
        {
            var previewPath = filePath.GetPreviewPath(size.ToString());
            if (File.Exists(previewPath))
            {
                previews[size.ToString()] = (await File.ReadAllBytesAsync(previewPath)).ToBase64();
            }
        }
        metadata["previews"] = previews;

        // Other components from PhysicalFileSystem
        await metadata.AddMetadataIfAvailable("embAnswer", filePath, GetEmbAnswer, PathExtensions.ResolveEmbAnswer);
        await metadata.AddMetadataIfAvailable("embConversation", filePath, GetEmbConversation, PathExtensions.ResolveEmbConversation);
        
        await metadata.AddMetadataIfAvailable("dqQuestion", filePath, GetDqQuestion, PathExtensions.ResolveDqQuestionPath);
        await metadata.AddMetadataIfAvailable("dqAnswer", filePath, GetDqAnswer, PathExtensions.ResolveDqAnswerPath);
        await metadata.AddMetadataIfAvailable("dqConversation", filePath, GetDqConversation, PathExtensions.ResolveDqConversationPath);
        
        await metadata.AddMetadataIfAvailable("commerceMarkQuestion", filePath, GetCommerceMarkQuestion, PathExtensions.ResolveCommerceMarkQuestionPath);
        await metadata.AddMetadataIfAvailable("commerceMarkAnswer", filePath, GetCommerceMarkAnswer, PathExtensions.ResolveCommerceMarkAnswerPath);
        await metadata.AddMetadataIfAvailable("commerceMarkConversation", filePath, GetCommerceMarkConversation, PathExtensions.ResolveCommerceMarkConversationPath);
        
        await metadata.AddMetadataIfAvailable("eng30TagsQuestion", filePath, GetEng30TagsQuestion, PathExtensions.ResolveEng30TagsQuestionPath);
        await metadata.AddMetadataIfAvailable("eng30TagsAnswer", filePath, GetEng30TagsAnswer, PathExtensions.ResolveEng30TagsAnswerPath);
        await metadata.AddMetadataIfAvailable("eng30TagsConversation", filePath, GetEng30TagsConversation, PathExtensions.ResolveEng30TagsConversationPath);
        
        await metadata.AddMetadataIfAvailable("engShortQuestion", filePath, GetEngShortQuestion, PathExtensions.ResolveEngShortQuestionPath);
        await metadata.AddMetadataIfAvailable("engShortAnswer", filePath, GetEngShortAnswer, PathExtensions.ResolveEngShortAnswerPath);
        await metadata.AddMetadataIfAvailable("engShortConversation", filePath, GetEngShortConversation, PathExtensions.ResolveEngShortConversationPath);

        return metadata;
    }

    public static string GetTargetPartitionDir(this string targetPath, string md5)
    {
        string ab = md5.Substring(0, 2);
        string cd = md5.Substring(2, 2);
        return Path.Combine(targetPath, ab, cd);
    }
}