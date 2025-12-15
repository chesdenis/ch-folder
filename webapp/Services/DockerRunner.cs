using System.Diagnostics;

namespace webapp.Services;

public interface IDockerFolderRunner
{
    Task<int> RunMetaUploaderAsync(string actionsPath, string hostFolderAbs, Action<string>? onStdout = null,
        Action<string>? onStderr = null, CancellationToken ct = default);

    Task<int> RunAiContentQueryBuilderAsync(string actionsPath, string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default); 
    
    Task<int> RunAiContentAnswerBuilderAsync(string actionsPath, string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default);

    Task<int> RunMd5ImageMarkerAsync(string actionsPath, string hostFolderAbs,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null, CancellationToken ct = default);

    Task<int> RunDuplicateMarkerAsync(string actionsPath, string hostFolderAbs,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null, CancellationToken ct = default);

    Task<int> RunFaceHashBuilderAsync(string actionsPath, string hostFolderAbs,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null, CancellationToken ct = default);

    Task<int> RunAverageImageMarkerAsync(string actionsPath, string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default);
}

public class DockerFolderRunner : IDockerFolderRunner
{
    public Task<int> RunMetaUploaderAsync(string actionsPath, string hostFolderAbs, Action<string>? onStdout = null,
        Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync(actionsPath, "meta_uploader", hostFolderAbs, "/in", onStdout, onStderr, ct);

    public Task<int> RunAiContentQueryBuilderAsync(string actionsPath, string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync(actionsPath, "ai_content_query_builder", hostFolderAbs, "/in", onStdout, onStderr, ct); 
    
    public Task<int> RunAiContentAnswerBuilderAsync(string actionsPath, string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync(actionsPath, "ai_content_answer_builder", hostFolderAbs, "/in", onStdout, onStderr, ct);

    public Task<int> RunMd5ImageMarkerAsync(string actionsPath, string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync(actionsPath, "md5_image_marker", hostFolderAbs, "/in", onStdout, onStderr, ct);

    public Task<int> RunDuplicateMarkerAsync(string actionsPath, string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync(actionsPath, "duplicate_marker", hostFolderAbs, "/in", onStdout, onStderr, ct);

    public Task<int> RunFaceHashBuilderAsync(string actionsPath, string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync(actionsPath, "face_hash_builder", hostFolderAbs, "/in", onStdout, onStderr, ct);

    public Task<int> RunAverageImageMarkerAsync(string actionsPath, string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync(actionsPath, "average_image_marker", hostFolderAbs, "/in", onStdout, onStderr, ct);


    private static Task<int> RunDockerAsync(
        string actionsPath,
        string image,
        string hostFolderAbs,
        string containerFolder,
        Action<string>? onStdout,
        Action<string>? onStderr,
        CancellationToken ct)
    {
        var host = Path.GetFullPath(hostFolderAbs);
        var arguments =
            $"run --env-file {Path.Combine(actionsPath, image)}/.env --rm -v \"{host}\":{containerFolder}:rw {image} {containerFolder}";

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