using System.Text;
using System.Diagnostics;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace ai_content_answer_builder;

public class AiContentAnswerBuilder(IFileSystem fileSystem)
{
    private static readonly HashSet<string> queriesToProcess = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> filesToProcess = new List<string>();

    public async Task RunAsync(string[] args)
    {
        args = args.ValidateArgs();
        await fileSystem.WalkThrough(args, async s =>
        {
            filesToProcess.Add(s);
            await Task.CompletedTask;
        });
        
        await fileSystem.WalkThrough(args, CollectQueryFiles);
        
        Console.WriteLine($"Found {filesToProcess.Count} files to process.");
        Console.WriteLine($"Found {queriesToProcess.Count} queries to process.");
        
        await Parallel.ForEachAsync(queriesToProcess,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            async (s, ct) =>
            {
                var results = await RunExternalAsync($"\"{s}\" gpt-5");
                if (results.ExitCode != 0)
                {
                    Console.WriteLine($"Error building description for {s}: {results.Stderr}");
                }
                else
                {
                    Console.WriteLine($"Built AI analysis for {s}");
                }
            });
    }

    private static async Task CollectQueryFiles(string filePath)
    {
        if (!filePath.AllowImageToProcess())
        {
            return;
        }

        try
        {
            var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
            if (groupName.Length != 4)
            {
                groupName = Path.GetFileNameWithoutExtension(filePath);
            }

            // Extract file information
            var directory = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");

            await CollectDescriptionQueries(directory, groupName);
            await CollectEnglish10WordsQueries(directory, groupName);
            await CollectTagsQueries(directory, groupName);
            await CollectCommerceMarkQueries(directory, groupName);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file '{filePath}': {ex.Message}");
        }
    }

    private static async Task CollectDescriptionQueries(string directory, string groupName)
    {
        var expectedPath = Path.Combine(directory, "dq", $"{groupName}.dq.md");
        queriesToProcess.Add(expectedPath);
    }

    private static async Task CollectEnglish10WordsQueries(string directory, string groupName)
    {
        var expectedPath = Path.Combine(directory, "engShort", $"{groupName}.engShort.md");
        queriesToProcess.Add(expectedPath);
    }

    private static async Task CollectTagsQueries(string directory, string groupName)
    {
        var expectedPath = Path.Combine(directory, "eng30tags", $"{groupName}.eng30tags.md");
        queriesToProcess.Add(expectedPath);
    }

    private static async Task CollectCommerceMarkQueries(string directory, string groupName)
    {
        var expectedPath = Path.Combine(directory, "commerceMark", $"{groupName}.commerceMark.md");
        queriesToProcess.Add(expectedPath);
    }

    /// <summary>
    /// Resolves the path to the external tool built and copied by the Dockerfile.
    /// Priority:
    /// 1) ENV `EXT_BIN_PATH` — absolute path to the executable.
    /// 2) ENV `EXT_BIN_NAME` (default: "external_app") placed by Dockerfile into `/usr/local/bin/<name>`.
    /// </summary>
    private static string ResolveExternalToolPath()
    {
        var explicitPath = Environment.GetEnvironmentVariable("EXT_BIN_PATH") ??
                           throw new InvalidOperationException("EXT_BIN_PATH is not configured");

        return explicitPath;
    }

    /// <summary>
    /// Convenience wrapper to run the external tool configured via Dockerfile/ENV.
    /// </summary>
    /// <param name="arguments">Arguments string passed to the external tool.</param>
    /// <param name="workingDirectory">Optional working directory for the process.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    public static Task<(int ExitCode, string Stdout, string Stderr)> RunExternalAsync(
        string arguments,
        string? workingDirectory = null,
        int? timeoutMs = null)
    {
        var exePath = ResolveExternalToolPath();
        return RunProcessAsync(exePath, arguments, workingDirectory, timeoutMs);
    }

    /// <summary>
    /// Runs an external process located at a custom path synchronously and returns its exit code and captured output.
    /// Output (stdout/stderr) is read after the process finishes to satisfy the requirement
    /// "make run sync with output reading once it will be finished".
    /// </summary>
    /// <param name="exePath">Full path to the executable to run.</param>
    /// <param name="arguments">Arguments string to pass to the executable.</param>
    /// <param name="workingDirectory">Optional working directory. If null, current directory is used.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds. If exceeded, the process is killed and -1 is returned.</param>
    /// <returns>Tuple containing ExitCode, Stdout, Stderr.</returns>
    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(
        string exePath,
        string arguments,
        string? workingDirectory = null,
        int? timeoutMs = null)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new ArgumentException("Executable path must be provided", nameof(exePath));
        }

        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"Executable not found: {exePath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Environment.CurrentDirectory
                : workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = false };

        // Start the process
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: {exePath}");
        }

        // Read output ONLY after the process finishes. We still initiate reads now but await them after exit.
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        Task waitTask = process.WaitForExitAsync();

        if (timeoutMs is > 0)
        {
            var completed = await Task.WhenAny(waitTask, Task.Delay(timeoutMs.Value));
            if (completed != waitTask)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                }
                catch
                {
                    /* ignore */
                }

                var outAfterKill = await SafeGetOutput(stdOutTask);
                var errAfterKill = await SafeGetOutput(stdErrTask);
                return (-1, outAfterKill, errAfterKill);
            }
        }
        else
        {
            await waitTask;
        }

        // Ensure process fully exited and streams are complete
        var stdout = await stdOutTask;
        var stderr = await stdErrTask;
        var exitCode = process.ExitCode;
        return (exitCode, stdout, stderr);

        static async Task<string> SafeGetOutput(Task<string> t)
        {
            try
            {
                return await t;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}