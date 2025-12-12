using System.Diagnostics;

namespace webapp.Services;

public interface IDockerSearchRunner
{
    Task<int> RunImageSearcherAsync(string actionsPath, string queryText, Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default);
}

public class DockerSearchRunner : IDockerSearchRunner
{
    public Task<int> RunImageSearcherAsync(string actionsPath, string queryText, Action<string>? onStdout = null, Action<string>? onStderr = null,
        CancellationToken ct = default) =>
        RunDockerAsync(actionsPath, "image_searcher", queryText, onStdout, onStderr, ct);

    private static Task<int> RunDockerAsync(
        string actionsPath,
        string image,
        string queryText,
        Action<string>? onStdout,
        Action<string>? onStderr,
        CancellationToken ct)
    {
        var arguments =
            $"run --env-file {Path.Combine(actionsPath,image)}/.env --rm {image} \"{queryText}\"";

        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) onStdout?.Invoke(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) onStderr?.Invoke(e.Data);
        };

        proc.Exited += (_, _) =>
        {
            try
            {
                tcs.TrySetResult(proc.ExitCode);
            }
            finally
            {
                proc.Dispose();
            }
        };

        if (!proc.Start())
        {
            tcs.TrySetResult(-1);
            return tcs.Task;
        }

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        if (ct.CanBeCanceled)
        {
            ct.Register(() =>
            {
                try
                {
                    if (!proc.HasExited)
                        proc.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignore
                }
            });
        }

        return tcs.Task;
    }
}