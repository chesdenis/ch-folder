using System.Diagnostics;

namespace webapp.Services;

public interface IDockerRunner
{
    Task<int> RunMetaUploaderAsync(
        string hostFolderAbs,
        string? envFilePath = null,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        CancellationToken ct = default);

    // Placeholders for future use on a different page
    Task<int> RunAiContentQueryBuilderAsync(string hostFolderAbs, string? envFilePath = null, Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default);
    Task<int> RunMd5ImageMarkerAsync(string hostFolderAbs, string? envFilePath = null, Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default);
    Task<int> RunFaceHashBuilderAsync(string hostFolderAbs, string? envFilePath = null, Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default);
    Task<int> RunAverageImageMarkerAsync(string hostFolderAbs, string? envFilePath = null, Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default);
}

public class DockerRunner : IDockerRunner
{
    private static Task<int> RunDockerAsync(
        string image,
        string hostFolderAbs,
        string containerTarget,
        IEnumerable<string>? extraArgs,
        Action<string>? onStdout,
        Action<string>? onStderr,
        CancellationToken ct)
    {
        var host = Path.GetFullPath(hostFolderAbs);
        var extra = extraArgs != null ? string.Join(" ", extraArgs) : string.Empty;
        var arguments = $"run --rm -v \"{host}\":{containerTarget}:rw {extra} {image} {containerTarget}";

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

        proc.OutputDataReceived += (_, e) => { if (e.Data != null) onStdout?.Invoke(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) onStderr?.Invoke(e.Data); };

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

    public Task<int> RunMetaUploaderAsync(string hostFolderAbs, string? envFilePath = null, Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync(
            "meta_uploader",
            hostFolderAbs,
            "/in",
            BuildEnvArgs(envFilePath),
            onStdout,
            onStderr,
            ct);

    public Task<int> RunAiContentQueryBuilderAsync(string hostFolderAbs, string? envFilePath = null, Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync("ai_content_query_builder", hostFolderAbs, "/in", BuildEnvArgs(envFilePath), onStdout, onStderr, ct);

    public Task<int> RunMd5ImageMarkerAsync(string hostFolderAbs, string? envFilePath = null, Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync("md5_image_marker", hostFolderAbs, "/in", BuildEnvArgs(envFilePath), onStdout, onStderr, ct);

    public Task<int> RunFaceHashBuilderAsync(string hostFolderAbs, string? envFilePath = null, Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync("face_hash_builder", hostFolderAbs, "/in", BuildEnvArgs(envFilePath), onStdout, onStderr, ct);

    public Task<int> RunAverageImageMarkerAsync(string hostFolderAbs, string? envFilePath = null, Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync("average_image_marker", hostFolderAbs, "/in", BuildEnvArgs(envFilePath), onStdout, onStderr, ct);

    private static IEnumerable<string>? BuildEnvArgs(string? envFilePath)
    {
        if (string.IsNullOrWhiteSpace(envFilePath)) return null;
        var abs = Path.GetFullPath(envFilePath);
        // Quote the path to handle spaces
        return [$"--env-file \"{abs}\""];
    }
}
