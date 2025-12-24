using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator;

internal abstract class ContentValidationTest(IFileSystem fs) : IContentValidationTest
{
    public abstract string Key { get; }

    public async Task<(int ExitCode, object Details)> ExecuteAsync(string folder, Func<dynamic, Task> log)
    {
        var (exit, total, mismatches, failures) = await RunAsync(folder, log);
        var details = new { total, mismatches, failures };
        return (exit, details);
    }
     
    private async Task<(int ExitCode, int Total, int Mismatches, IReadOnlyList<object> Failures)> RunAsync(
        string folder,
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

            var result = await Validate(log, filePath, failures);
            if(!result) mismatches++;
        }

        await log(new { message = $"Done. Mismatches: {mismatches} of {total}" });
        var code = mismatches == 0 ? 0 : 4;
        return (code, total, mismatches, failures);
    }

    protected abstract Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures);
}