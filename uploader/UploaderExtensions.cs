using System.Text;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace uploader;

public static class UploaderExtensions
{
    public static string ToBase64(this string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    public static string ToBase64(this byte[] bytes) => Convert.ToBase64String(bytes);

    public static async Task AddMetadataIfAvailable(this Dictionary<string, object?> metadata, string key, string filePath, Func<string, Task<string>> getter, Func<string, string> pathResolver)
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

    public static bool HasEnoughSpace(this string targetPath, string sourceFilePath, long bufferBytes)
    {
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(targetPath))!);
            var fileInfo = new FileInfo(sourceFilePath);
            return driveInfo.AvailableFreeSpace > (fileInfo.Length + bufferBytes);
        }
        catch
        {
            return true; // If we can't determine, try to continue
        }
    }

    public static async Task<Dictionary<string, object?>> CollectMetadataAsync(this IFileSystem fs, string filePath, string sourcePath)
    {
        var relativePath = Path.GetRelativePath(sourcePath, filePath);
        var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string partition = pathParts.Length > 1 ? pathParts[0] : "";
        string section = pathParts.Length > 2 ? pathParts[1] : "";

        var metadata = new Dictionary<string, object?>();
        metadata["partition"] = partition;
        metadata["section"] = section;

        // Description and Tags (as used in ImageEmbeddingUploader)
        if (fs.FileExists(PathExtensions.ResolveDqAnswerPath(filePath)))
        {
            metadata["description"] = (await fs.GetDqAnswer(filePath)).ToBase64();
        }

        var tags = ImageProcessingExtensions.GetEng30TagsText(filePath);
        if (tags != null && tags.Length > 0)
        {
            metadata["tags"] = tags.Select(t => t.ToBase64()).ToArray();
        }

        // Previews
        var previews = new Dictionary<string, string>();
        foreach (var size in ImageProcessingExtensions.AllowedSizes)
        {
            var previewPath = PathExtensions.GetPreviewPath(filePath, size.ToString());
            if (fs.FileExists(previewPath))
            {
                previews[size.ToString()] = (await File.ReadAllBytesAsync(previewPath)).ToBase64();
            }
        }
        metadata["previews"] = previews;

        // Other components from PhysicalFileSystem
        await metadata.AddMetadataIfAvailable("embAnswer", filePath, fs.GetEmbAnswer, PathExtensions.ResolveEmbAnswer);
        await metadata.AddMetadataIfAvailable("embConversation", filePath, fs.GetEmbConversation, PathExtensions.ResolveEmbConversation);
        await metadata.AddMetadataIfAvailable("dqQuestion", filePath, fs.GetDqQuestion, PathExtensions.ResolveDqQuestionPath);
        await metadata.AddMetadataIfAvailable("commerceMarkQuestion", filePath, fs.GetCommerceMarkQuestion, PathExtensions.ResolveCommerceMarkQuestionPath);
        await metadata.AddMetadataIfAvailable("eng30TagsQuestion", filePath, fs.GetEng30TagsQuestion, PathExtensions.ResolveEng30TagsQuestionPath);
        await metadata.AddMetadataIfAvailable("engShortQuestion", filePath, fs.GetEngShortQuestion, PathExtensions.ResolveEngShortQuestionPath);
        await metadata.AddMetadataIfAvailable("dqAnswer", filePath, fs.GetDqAnswer, PathExtensions.ResolveDqAnswerPath);
        await metadata.AddMetadataIfAvailable("commerceMarkAnswer", filePath, fs.GetCommerceMarkAnswer, PathExtensions.ResolveCommerceMarkAnswerPath);
        await metadata.AddMetadataIfAvailable("eng30TagsAnswer", filePath, fs.GetEng30TagsAnswer, PathExtensions.ResolveEng30TagsAnswerPath);
        await metadata.AddMetadataIfAvailable("engShortAnswer", filePath, fs.GetEngShortAnswer, PathExtensions.ResolveEngShortAnswerPath);
        await metadata.AddMetadataIfAvailable("dqConversation", filePath, fs.GetDqConversation, PathExtensions.ResolveDqConversationPath);
        await metadata.AddMetadataIfAvailable("commerceMarkConversation", filePath, fs.GetCommerceMarkConversation, PathExtensions.ResolveCommerceMarkConversationPath);
        await metadata.AddMetadataIfAvailable("eng30TagsConversation", filePath, fs.GetEng30TagsConversation, PathExtensions.ResolveEng30TagsConversationPath);
        await metadata.AddMetadataIfAvailable("engShortConversation", filePath, fs.GetEngShortConversation, PathExtensions.ResolveEngShortConversationPath);

        return metadata;
    }

    public static string GetTargetPartitionDir(this string targetPath, string md5)
    {
        string ab = md5.Substring(0, 2);
        string cd = md5.Substring(2, 2);
        string ef = md5.Substring(4, 2);
        return Path.Combine(targetPath, ab, cd, ef);
    }
}
