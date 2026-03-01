using System.Diagnostics;
using Microsoft.Extensions.Options;
using shared_csharp.Extensions;
using webapp.Models;

namespace webapp.Services;

public interface IDockerPartitionRunner
{
    Task<int> RunMetaUploaderAsync(Action<string>? onStdout = null,
        Action<string>? onStderr = null, CancellationToken ct = default, string? extraArgs = "");
}

public class DockerPartitionRunner(IOptions<WebAppOptions> storage) : IDockerPartitionRunner
{
    private readonly WebAppOptions _webAppOptions = storage.Value;

    public Task<int> RunMetaUploaderAsync(Action<string>? onStdout = null,
        Action<string>? onStderr = null, CancellationToken ct = default, string? extraArgs = "")
        => RunDockerAsync("meta_uploader", onStdout, onStderr, ct, extraArgs);

    private Task<int> RunDockerAsync(
        string image,
        Action<string>? onStdout,
        Action<string>? onStderr,
        CancellationToken ct,
        string? extraArgs = null)
    {
        var actionsPath = this._webAppOptions.ActionsPath ?? throw new InvalidOperationException();
        var envFile = Path.Combine(actionsPath, image, ".env");
        var argsTail = string.IsNullOrWhiteSpace(extraArgs) ? string.Empty : $" {extraArgs}";
        var arguments =
            $"run --env-file {envFile} --rm {image} {argsTail}";

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