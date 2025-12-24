using System.Text.RegularExpressions;

namespace shared_csharp.Extensions;

public static class PathExtensions
{
    private static readonly Regex Md5Regex = new Regex("^[a-fA-F0-9]{32}$", RegexOptions.Compiled);

    public static string GetGroupName(this string filePath)
    {
        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        return groupName;
    }
    
    public static string GetPreview16Path(this string filePath) => GetPreviewPath(filePath, "16");
    public static string GetPreview32Path(this string filePath) => GetPreviewPath(filePath, "32");
    public static string GetPreview64Path(this string filePath) => GetPreviewPath(filePath, "64");
    public static string GetPreview128Path(this string filePath) => GetPreviewPath(filePath, "128");
    public static string GetPreview512Path(this string filePath) => GetPreviewPath(filePath, "512");
    public static string GetPreview2000Path(this string filePath) => GetPreviewPath(filePath, "2000");

    public static string GetPreviewPath(this string filePath, string previewKind)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var previewFolder = Path.Combine(directoryName, "preview");

        return Path.Combine(previewFolder, $"{Path.GetFileNameWithoutExtension(filePath)}_p{previewKind}.jpg");
    }

    /// <summary>
    /// Storage folders are 2 level deep. First level is yearly based partition, second level is event partition
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<string> GetStorageFolders(string rootStoragePath)
    {
        if (!Directory.Exists(rootStoragePath))
            yield break;
        
        // Level 1: immediate subfolders of contextPath
        var firstLevelDirs = Directory.GetDirectories(rootStoragePath, "*", SearchOption.TopDirectoryOnly);
        foreach (var dr in firstLevelDirs)
        {
            var directoryName = Path.GetFileName(dr);
            if (string.IsNullOrWhiteSpace(directoryName) || directoryName.StartsWith("."))
                continue;

            // include the level-1 folder itself
            yield return directoryName;

            // Level 2: subfolders of each level-1 folder
            var level2Dirs = Directory.GetDirectories(dr, "*", SearchOption.TopDirectoryOnly);
            foreach (var dir2 in level2Dirs)
            {
                var name2 = Path.GetFileName(dir2);
                if (string.IsNullOrWhiteSpace(name2) || name2.StartsWith("."))
                    continue;

                var relative = Path.Combine(directoryName, name2);
                
                yield return relative;
            }
        }
    }
    
    public static IEnumerable<string> GetFilesInFolder(string contextPath, IEnumerable<string> storageFolders, Action<string> onFolderProcessed = null)
    {
        foreach (var storageFolder in storageFolders)
        {
            var folderPath = Path.Combine(contextPath, storageFolder);
            // Ensure the folder exists
            if (!Directory.Exists(folderPath))
                continue;

            // Get all files in the folder and add their names to the data
            // use top directory only because other folders are system, preview, etc.
            var files = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                .Select(s => new
            {
                fileName = Path.GetFileName(s),
                filePath = s
            });
            // excluding preview and system files and unsupported file types
            files = files.Where(f =>
                !ImageProcessingExtensions.IgnoredExtensions
                    .Contains(Path.GetExtension(f.fileName)));

            files = files.Where(w => !w.fileName.StartsWith("._"));
            
            foreach (var file in files)
            {
                yield return file.filePath;
            }

            onFolderProcessed?.Invoke(folderPath);
        }
    }

    public static bool IsMd5InFileName(this string fileName)
    {
        // Get the file name without the extension
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        // Split by underscores
        var parts = fileNameWithoutExtension.Split('_');

        // Check if the last part is a valid MD5 hash (32 hexadecimal characters)
        return parts.Length > 0 && Md5Regex.IsMatch(parts[^1]); // Check the last part
    }

    public static string GetMd5FromFileName(this string fileName)
    {
        // Get the file name without the extension and split by underscores
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var parts = fileNameWithoutExtension.Split('_');

        return parts[^1];
    }
    
    public static string ResolveFileKey(string filePath)
    {
        // optional group name 
        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        return groupName;
    }

    public static string ResolveAiPath(string filePath, string sectionKey, string kindKey)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var sectionFolder = Path.Combine(directoryName, sectionKey);
        var groupName = ResolveFileKey(filePath);

        if (kindKey == "question")
        {
            return Path.Combine(sectionFolder, groupName + $".{sectionKey}.md");
        }

        return Path.Combine(sectionFolder, groupName + $".{sectionKey}.md.{kindKey}.md");
    }
    
    public static string ResolveEmbAnswer(string filePath) => ResolveAiPath(filePath, "emb", "answer");
    public static string ResolveEmbConversation(string filePath) => ResolveAiPath(filePath, "emb", "conversation");

    public static string ResolveDqQuestionPath(string filePath) => ResolveAiPath(filePath, "dq", "question");
    public static string ResolveCommerceMarkQuestionPath(string filePath) => ResolveAiPath(filePath, "commerceMark", "question");
    public static string ResolveEng30TagsQuestionPath(string filePath) => ResolveAiPath(filePath, "eng30Tags", "question");
    public static string ResolveEngShortQuestionPath(string filePath) => ResolveAiPath(filePath, "engShort", "question");

    public static string ResolveDqConversationPath(string filePath) => ResolveAiPath(filePath, "dq", "conversation");
    public static string ResolveCommerceMarkConversationPath(string filePath) => ResolveAiPath(filePath, "commerceMark", "conversation");
    public static string ResolveEng30TagsConversationPath(string filePath) => ResolveAiPath(filePath, "eng30Tags", "conversation");
    public static string ResolveEngShortConversationPath(string filePath) => ResolveAiPath(filePath, "engShort", "conversation");

    public static string ResolveDqAnswerPath(string filePath) => ResolveAiPath(filePath, "dq", "answer");
    public static string ResolveCommerceMarkAnswerPath(string filePath) => ResolveAiPath(filePath, "commerceMark", "answer");
    public static string ResolveEng30TagsAnswerPath(string filePath) => ResolveAiPath(filePath, "eng30Tags", "answer");
    public static string ResolveEngShortAnswerPath(string filePath) => ResolveAiPath(filePath, "engShort", "answer");

    
    
}