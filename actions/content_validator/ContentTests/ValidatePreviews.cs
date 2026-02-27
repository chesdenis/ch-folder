using shared_csharp.Abstractions;

namespace content_validator.ContentTests;

internal sealed class ValidatePreviews(IFileSystem fs) : ContentValidationTest(fs)
{
    public override string Key => "PREVIEWS";
    
    private static string[] GetPreviewKinds() => ["16", "32", "64", "128", "512", "2000"];

    protected override async Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        try
        {
            var metadata = await fs.GetMetadata(filePath);
            if (metadata == null) 
            {
                failures.Add(new { file = filePath, reason = "Metadata file is missing." });
                return false;
            }

            if (metadata.Previews == null) 
            {
                failures.Add(new { file = filePath, reason = "Previews are missing in metadata." });
                return false;
            }

            foreach (var previewKind in GetPreviewKinds())
            {
                if (!metadata.Previews.ContainsKey(previewKind))
                {
                    failures.Add(new { file = filePath, reason = $"Preview '{previewKind}' is missing in metadata." });
                    return false;
                }
            }
            
            return true;
        }
        catch (Exception e)
        {
            failures.Add(new { file = filePath, reason = $"Fatal error for '{filePath}': {e.Message}" });
            return false;
        }
    }
}