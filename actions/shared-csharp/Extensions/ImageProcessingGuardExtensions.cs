namespace shared_csharp.Extensions;

public static class ImageProcessingGuardExtensions
{
    public static readonly HashSet<string> IgnoredExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".ds_store",
            ".mov",
            ".mp4",
            "._",
        };
    
    public static bool AllowImageToProcess(this string filePath) => 
        !IgnoredExtensions.Contains(Path.GetExtension(filePath));
}