using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator;

internal sealed class FileHasCorrectMd5PrefixTest(IFileSystem fs) : IContentValidationTest
{
    public string Key => "file_has_correct_md5_prefix";

    public async Task<(int ExitCode, object Details)> ExecuteAsync(string folder, Func<dynamic, Task> log)
    {
        var (exit, total, mismatches, failures) = await RunAsync(folder, log);
        var details = new { total, mismatches, failures };
        return (exit, details);
    }

    private async Task<(int ExitCode, int Total, int Mismatches, IReadOnlyList<object> Failures)> RunAsync(string folder,
        Func<dynamic, Task> log)
    {
        if (!fs.DirectoryExists(folder))
        {
            await log(new { message = $"Folder does not exist: {folder}" });
            return (2, 0, 0, Array.Empty<object>());
        }

        var files = fs.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly).ToArray();
        int total = files.Length;
        int mismatches = 0;
        var failures = new List<object>();
        await log(new { message = $"Checking {total} files..." });

        foreach (var filePath in files)
        {
            if (!filePath.AllowImageToProcess())
                continue;

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
                mismatches++;

                await log(new { message = $"MD5 hash is incorrect: {Path.GetFileName(filePath)}" });
                failures.Add(new { file = filePath, reason = "MD5 hash is incorrect" });
            }
        }

        await log(new { message = $"Done. Mismatches: {mismatches} of {total}" });
        var code = mismatches == 0 ? 0 : 4;
        return (code, total, mismatches, failures);
    }
}