using System.Text.Json.Serialization;

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
    
    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mov",
        ".mp4",
        ".avi",
        ".mkv"
    };
    
    public static bool IsVideo(this string extension)
    {
        if (VideoExtensions.Contains(extension.ToLowerInvariant()))
        {
            return true;
        }
        
        return false;
    }
    
    public record RateExplanation(
        [property: JsonPropertyName("rate")] int rate,
        [property: JsonPropertyName("rate-explanation")] string rateExplanation
    );
}