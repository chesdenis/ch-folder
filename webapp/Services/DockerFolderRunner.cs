using System.Diagnostics;
using Microsoft.Extensions.Options;
using shared_csharp.Extensions;
using webapp.Models;

namespace webapp.Services;

public interface IDockerFolderRunner
{
    Task<int> RunMetaUploaderAsync(string hostFolderAbs, Action<string>? onStdout = null,
        Action<string>? onStderr = null, CancellationToken ct = default);

    Task<int> RunAiContentQueryBuilderAsync(string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default); 
    
    Task<int> RunAiContentAnswerBuilderAsync(string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default);
    
    Task<int> RunMd5ImageMarkerAsync(string hostFolderAbs,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null, CancellationToken ct = default);

    Task<int> RunDuplicateMarkerAsync(string hostFolderAbs,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null, CancellationToken ct = default);

    Task<int> RunFaceHashBuilderAsync(string hostFolderAbs,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null, CancellationToken ct = default); 
    
    Task<int> RunGroupFolderExtractorAsync(string hostFolderAbs,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null, CancellationToken ct = default); 
    
    Task<int> RunEmbeddingDownloaderAsync(string hostFolderAbs,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null, CancellationToken ct = default);

    Task<int> RunAverageImageMarkerAsync(string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default);

    Task<int> RunContentValidatorAsync(string hostFolderAbs, string testKind, string folderName,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default);
}

public class DockerFolderRunner(IOptions<StorageOptions> storage) : IDockerFolderRunner
{
    private readonly StorageOptions _storageOptions = storage.Value;

    public Task<int> RunMetaUploaderAsync(string hostFolderAbs, Action<string>? onStdout = null,
        Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync("meta_uploader", hostFolderAbs, onStdout, onStderr, ct);

    public Task<int> RunAiContentQueryBuilderAsync(string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync("ai_content_query_builder", hostFolderAbs, onStdout, onStderr, ct); 
    
    public Task<int> RunAiContentAnswerBuilderAsync(string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync("ai_content_answer_builder", hostFolderAbs, onStdout, onStderr, ct);

    public Task<int> RunMd5ImageMarkerAsync(string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync("md5_image_marker", hostFolderAbs, onStdout, onStderr, ct);

    public Task<int> RunDuplicateMarkerAsync(string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync("duplicate_marker", hostFolderAbs, onStdout, onStderr, ct);

    public Task<int> RunFaceHashBuilderAsync(string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync("face_hash_builder", hostFolderAbs, onStdout, onStderr, ct);
     public Task<int> RunGroupFolderExtractorAsync(string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync("group_folder_extractor", hostFolderAbs, onStdout, onStderr, ct);
    
    public Task<int> RunEmbeddingDownloaderAsync(string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync("embedding_downloader", hostFolderAbs, onStdout, onStderr, ct);

    public Task<int> RunAverageImageMarkerAsync(string hostFolderAbs,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync("average_image_marker", hostFolderAbs, onStdout, onStderr, ct);

    public Task<int> RunContentValidatorAsync(string hostFolderAbs, string testKind, string folderName,
        Action<string>? onStdout = null, Action<string>? onStderr = null, CancellationToken ct = default)
        => RunDockerAsync("content_validator", hostFolderAbs, onStdout, onStderr, ct,
            extraArgs: $"--test-kind {testKind.AsBase64String()} --folder-name {folderName.AsBase64String()}");


    private Task<int> RunDockerAsync(
        string image,
        string hostFolderAbs,
        Action<string>? onStdout,
        Action<string>? onStderr,
        CancellationToken ct,
        string? extraArgs = null)
    {
        var containerFolder = "/in";
        var actionsPath = this._storageOptions.ActionsPath ?? throw new InvalidOperationException();
        var host = Path.GetFullPath(hostFolderAbs);
        var envFile = Path.Combine(actionsPath, image, ".env");
        var argsTail = string.IsNullOrWhiteSpace(extraArgs) ? string.Empty : $" {extraArgs}";
        var arguments =
            $"run --env-file {envFile} --rm -v \"{host}\":{containerFolder}:rw {image} {containerFolder}{argsTail}";

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