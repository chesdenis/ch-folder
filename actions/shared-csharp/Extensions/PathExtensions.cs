using System.Text.RegularExpressions;

namespace shared_csharp.Extensions;

public static class PathExtensions
{
    private static readonly Regex Md5Regex = new Regex("^[a-fA-F0-9]{32}$", RegexOptions.Compiled);
    
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
    
    public static string ResolveFvAnswer(string filePath) => ResolveAiPath(filePath, "fv", "answer");

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