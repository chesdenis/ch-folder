using System.Text.Json;
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

    public static readonly HashSet<string> IgnoredExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".ds_store",
            "._",
            ".json",
        };

    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mov",
        ".mp4",
    };
    
    public static bool AllowToProcess(this string filePath)
    {
        if (!IgnoredExtensions.Contains(Path.GetExtension(filePath)))
        {
            // ignore system files
            if (!Path.GetFileName(filePath).StartsWith("._"))
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsVideo(this string filePath)
    {
        if (VideoExtensions.Contains(Path.GetExtension(filePath)))
        {
            return true;
        }
        
        return false;
    }
    
    public record RateExplanation(
        [property: JsonPropertyName("rate")] int rate,
        [property: JsonPropertyName("rate-explanation")] string rateExplanation
    );
    
    public record FaceEncoding(
    string[] detected_faces,
    int rotation,
    int[][] face_locations,
    double[][] face_encodings
);


}