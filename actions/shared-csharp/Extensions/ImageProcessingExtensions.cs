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

    public static async Task<RateExplanation?> GetRateExplanation(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var commerceFolder = Path.Combine(directoryName, "commerceMark");
        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }


        var commerceMarkPath = Path.Combine(commerceFolder, $"{groupName}.commerceMark.md.answer.md");
        if(!File.Exists(commerceMarkPath)) return null;
        
        var commerceRawContent = await File.ReadAllTextAsync(commerceMarkPath);
        var commerceData = JsonSerializer.Deserialize<RateExplanation>(commerceRawContent);

        return commerceData;
    }
    
    public static string GetEngShortText(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var dqFolder = Path.Combine(directoryName, "engShort");

        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        var dqQuestionPath = Path.Combine(dqFolder, groupName + ".engShort.md");
        var dqAnswerPath = Path.Combine(dqFolder, groupName + ".engShort.md.answer.md");
        
        if (!File.Exists(dqAnswerPath)) return string.Empty;

        return File.ReadAllText(dqAnswerPath);
    } 
    
    public static string[] GetEng30TagsText(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var dqFolder = Path.Combine(directoryName, "eng30tags");

        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }
        
        var dqAnswerPath = Path.Combine(dqFolder, groupName + ".eng30tags.md.answer.md");
        if(!File.Exists(dqAnswerPath)) return Array.Empty<string>();
        
        return File.ReadAllText(dqAnswerPath).Split(',').Select(s => s.Trim()).ToArray();
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