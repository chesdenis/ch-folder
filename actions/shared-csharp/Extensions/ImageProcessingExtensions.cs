namespace shared_csharp.Extensions;

public static class ImageProcessingExtensions
{
    public static readonly int[] AllowedSizes = [16, 32, 64, 128, 256, 512, 2000];

    public static int SnapToAllowed(this int value)
    {
        var closest = AllowedSizes[0];
        foreach (var s in AllowedSizes)
        {
            if (Math.Abs(s - value) < Math.Abs(closest - value)) closest = s;
        }
        return closest;
    }
    
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