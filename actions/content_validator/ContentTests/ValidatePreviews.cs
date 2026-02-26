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
            var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
            var previewFolder = Path.Combine(directoryName, "preview");

            foreach (var previewKind in GetPreviewKinds())
            {
                var previewFileName = Path.GetFileNameWithoutExtension(filePath) + "_p" + previewKind + ".jpg";
                var previewPath = Path.Combine(previewFolder, previewFileName);

                var fileExist = fs.FileExists(previewPath);
                if (!fileExist)
                {
                    var s = $"Preview file '{previewPath}' does not exist.";
                    failures.Add(new { file = filePath, reason = s });
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