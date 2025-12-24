using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class ValidateFileNameConvention(IFileSystem fs) : ContentValidationTest(fs)
{
    public override string Key => "FNC";

    protected override async Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        try
        {
            var fileIdParts = Path.GetFileNameWithoutExtension(filePath).Split("_");
            if (fileIdParts.Length == 4)
            {
                return true;
            }

            if (fileIdParts.Length == 3)
            {
                return true;
            }

            if (fileIdParts.Length == 1)
            {
                var md5Hash = fileIdParts[0];

                string actualHash = await filePath.CalculateMd5Async();

                var result = md5Hash.Equals(actualHash, StringComparison.InvariantCultureIgnoreCase);

                if (!result)
                {
                    await log(new { message = $"File format is incorrect: {Path.GetFileName(filePath)}" });
                    failures.Add(
                        new { file = filePath, reason = $"File format is incorrect: {Path.GetFileName(filePath)}" });
                }

                return result;
            }


            failures.Add(new { file = filePath, reason = $"File format is incorrect: {Path.GetFileName(filePath)}" });
            return false;
        }
        catch (Exception e)
        {
            await log(new { message = $"Fatal error for '{filePath}': {e.Message}" });
            failures.Add(new { file = filePath, reason = $"Fatal error for '{filePath}': {e.Message}" });
            return false;
        }
    }
}