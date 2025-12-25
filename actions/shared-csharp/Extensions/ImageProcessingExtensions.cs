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
        
        var commerceRawContent = await File.ReadAllTextAsync(Path.Combine(commerceFolder, $"{groupName}.commerceMark.md.answer.md"));
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
        return File.ReadAllText(dqAnswerPath).Split(',').Select(s => s.Trim()).ToArray();
    }

    public static string[] GetFacesOnPhotos(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var fvFolder = Path.Combine(directoryName, "fv");

        var groupName = filePath.GetGroupName();
        var fvAnswerPath = Path.Combine(fvFolder, groupName + ".fv.md.answer.md");

        var rawText = File.ReadAllText(fvAnswerPath);
        var d = JsonSerializer.Deserialize<FaceEncoding>(rawText);
        return d?.detected_faces ?? [];
    }
    
    public static readonly HashSet<string> IgnoredExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".ds_store",
            ".mov",
            ".mp4",
            "._",
        };
    
    public static bool AllowImageToProcess(this string filePath)
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