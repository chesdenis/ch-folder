using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator;

internal sealed class FileHasCorrectMd5PrefixTest(IFileSystem fs) : ContentValidationTest(fs)
{
    public override string Key => "file_has_correct_md5_prefix";


    protected override async Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        var fileIdParts = Path.GetFileNameWithoutExtension(filePath).Split("_");
        string md5Hash = string.Empty;

        if (fileIdParts.Length == 4 && fileIdParts[0].Length == 4)
        {
            // we assume that fileIdParts[0] is a group ID, 4 characters long
            // then we assume that fileIdParts[3] is md5 hash
            md5Hash = fileIdParts[3];
        }

        if (fileIdParts.Length == 3)
        {
            // we assume that fileIdParts[0..1] are preview hashes
            // then we assume that fileIdParts[2] is md5 hash
            md5Hash = fileIdParts[2];
        }

        if (fileIdParts.Length == 1)
        {
            md5Hash = fileIdParts[0];
        }

        string actualHash = await filePath.CalculateMd5Async(force: true);

        var result = md5Hash.Equals(actualHash, StringComparison.InvariantCultureIgnoreCase);

        if (!result)
        {
            await log(new { message = $"MD5 hash is incorrect: {Path.GetFileName(filePath)}" });
            failures.Add(new { file = filePath, reason = "MD5 hash is incorrect" });
        }
        
        return result;
    }
}