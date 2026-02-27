using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

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

    public static async Task<RateExplanation?> GetRateExplanation(string filePath)
    {
        var metadataPath = filePath.GetMetadataPath();
        if (!File.Exists(metadataPath)) return null;
        
        var content = await File.ReadAllTextAsync(metadataPath);
        var metadata = JsonConvert.DeserializeObject<FileMetadata>(content);
        if (metadata == null || string.IsNullOrEmpty(metadata.CommerceMarkAnswer)) return null;
        
        var commerceRawContent = Decode(metadata.CommerceMarkAnswer);
        var commerceData = JsonSerializer.Deserialize<RateExplanation>(commerceRawContent);

        return commerceData;
    }
    
    public static string GetEngShortText(string filePath)
    {
        var metadataPath = filePath.GetMetadataPath();
        if (!File.Exists(metadataPath)) return string.Empty;

        var content = File.ReadAllText(metadataPath);
        var metadata = JsonConvert.DeserializeObject<FileMetadata>(content);
        return Decode(metadata?.EngShortAnswer);
    } 
    
    public static string[] GetEng30TagsText(string filePath)
    {
        var metadataPath = filePath.GetMetadataPath();
        if (!File.Exists(metadataPath)) return Array.Empty<string>();

        var content = File.ReadAllText(metadataPath);
        var metadata = JsonConvert.DeserializeObject<FileMetadata>(content);
        var tags = Decode(metadata?.Eng30TagsAnswer);
        if (string.IsNullOrEmpty(tags)) return Array.Empty<string>();

        return tags.Split(',').Select(s => s.Trim()).ToArray();
    }

    private static string Decode(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return string.Empty;
        try 
        {
            var bytes = Convert.FromBase64String(base64);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch 
        {
            return base64; // Fallback if not base64
        }
    }

    public static string[] GetFacesOnPhotos(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var fvFolder = Path.Combine(directoryName, "fv");

        var groupName = filePath.GetGroupName();
        var fvAnswerPath = Path.Combine(fvFolder, groupName + ".fv.md.answer.md");

        if (!File.Exists(fvAnswerPath))
        {
            // attempting 2nd case, because this is python generated file, can be not aligned with general flow
            fvAnswerPath = Path.Combine(fvFolder, Path.GetFileNameWithoutExtension(filePath) + ".fv.md.answer.md");

            if (!File.Exists(fvAnswerPath))
            {
                return ["NOBODY"];
            }
        }

        var rawText = File.ReadAllText(fvAnswerPath);
        var d = JsonSerializer.Deserialize<FaceEncoding>(rawText);

        if (d?.detected_faces != null && d.detected_faces.Length > 0)
        {
            // here we spotted some known faces -> so returning them
            return d.detected_faces;
        }

        if(d?.face_locations != null && d.face_locations.Length > 0)
        {
            // return specific "placeholder" which will tell that some face on photo
            return ["SOMEONE"];
        }

        if(d?.face_locations != null && d.face_locations.Length == 0)
        {
            // return specific "placeholder" which will tell that photo does not contain any face on it
            return ["NOBODY"];
        }

        // other cases return empty string array
        return ["NOBODY"];
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