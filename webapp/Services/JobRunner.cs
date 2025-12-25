using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using webapp.Hubs;
using webapp.Models;
using Microsoft.Extensions.Options;
using shared_csharp.Extensions;

namespace webapp.Services;

public enum JobType
{
    MetaUploader,
    AiContentQueryBuilder,
    AiContentAnswerBuilder,
    EmbeddingDownloader,
    Md5ImageMarker,
    DuplicateMarker,
    FaceHashBuilder,
    GroupFolderExtractor,
    AverageImageMarker,
    ContentValidator
}

public interface IJobRunner
{
    string StartJob(
        string jobId,
        JobType jobType,
        string workingFolder,
        int? degreeOfParallelism = null,
        string? testKind = null,
        string? level1 = null,
        string? level2 = null);
}



public class JobRunner : IJobRunner
{
    private readonly IHubContext<JobStatusHub> _hub;
    private readonly ILogger<JobRunner> _logger;
    private readonly StorageOptions _storageOptions;
    private readonly IDockerFolderRunner _dockerFolderRunner;

    public JobRunner(
        IHubContext<JobStatusHub> hub, ILogger<JobRunner> logger, IOptions<StorageOptions> storage, IDockerFolderRunner dockerFolderRunner, IImageLocator imageLocator)
    {
        _hub = hub;
        _logger = logger;
        _storageOptions = storage.Value;
        _dockerFolderRunner = dockerFolderRunner;
    }

    public string StartJob(
        string jobId,
        JobType jobType,
        string workingFolder,
        int? degreeOfParallelism = null,
        string? testKind = null,
        string? level1 = null,
        string? level2 = null)
    {
        var group = JobStatusHub.GroupName(jobId);

        // Fire-and-forget background task
        _ = Task.Run((Func<Task>)(async () =>
        {
            try
            {
                var rootPath = workingFolder ?? throw new InvalidOperationException("Working folder was not provided");
                if (!Directory.Exists(rootPath))
                {
                    throw new DirectoryNotFoundException($"Working folder '{rootPath}' does not exist");
                }
                
                var actionsPath =_storageOptions.ActionsPath ?? throw new InvalidOperationException("Actions root not specified in settings");
                    
                IEnumerable<string> storageFoldersEnum = PathExtensions.GetStorageFolders(rootPath);

                // Optional filtering by level1/level2 folder names
                var l1 = string.IsNullOrWhiteSpace(level1) ? null : level1.Trim();
                var l2 = string.IsNullOrWhiteSpace(level2) ? null : level2.Trim();
                if (l1 != null || l2 != null)
                {
                    static string[] SplitParts(string s)
                    {
                        var norm = s.Replace('\\', '/');
                        return norm.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    }

                    storageFoldersEnum = storageFoldersEnum.Where(s =>
                    {
                        var parts = SplitParts(s);
                        if (l1 == null && l2 == null) return true;
                        if (l1 != null && l2 == null)
                        {
                            // include first-level folder itself and its second-levels
                            return parts.Length >= 1 && string.Equals(parts[0], l1, StringComparison.OrdinalIgnoreCase);
                        }
                        if (l1 == null && l2 != null)
                        {
                            // any second-level with matching name
                            return parts.Length >= 2 && string.Equals(parts[1], l2, StringComparison.OrdinalIgnoreCase);
                        }

                        // both provided
                        return parts.Length >= 2
                               && string.Equals(parts[0], l1, StringComparison.OrdinalIgnoreCase)
                               && string.Equals(parts[1], l2, StringComparison.OrdinalIgnoreCase);
                    });
                }

                var storageFolders = storageFoldersEnum.ToArray();
                var total = storageFolders.Length;
                var completed = 0;
                
                var dop = Math.Max(1, degreeOfParallelism ?? Math.Min(Environment.ProcessorCount, 4));

                await ReportProgress(jobId, group, total, completed, $"Job '{jobType}' started. Root: '{rootPath}'");
                
                await ReportProgress(jobId, group, total, completed,
                    $"Discovered {total} folder(s). Starting '{jobType}' with DOP={dop}...");

                var errors = new ConcurrentBag<string>();

                await Parallel.ForEachAsync(storageFolders, new ParallelOptions { MaxDegreeOfParallelism = dop }, async (row, ct) =>
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(row)) return;

                        var folderAbs = Path.GetFullPath(Path.Combine(rootPath, row));
                        if (!Directory.Exists(folderAbs)) return;

                        await ReportProgress(jobId, group, total, completed,
                            $"Starting: {Path.GetRelativePath(rootPath, folderAbs)}", ct);

                        int exit;
                        switch (jobType)
                        {
                            case JobType.MetaUploader:
                            case JobType.AiContentQueryBuilder:
                            case JobType.AiContentAnswerBuilder:
                            case JobType.EmbeddingDownloader:
                            case JobType.Md5ImageMarker:
                            case JobType.DuplicateMarker:
                            case JobType.FaceHashBuilder:
                            case JobType.GroupFolderExtractor:
                            case JobType.AverageImageMarker:
                            {
                                // Map job to appropriate docker runner function (unify signatures via wrappers)
                                var jobFunc = BuildJobFunc(jobType, jobId);
                                
                                exit = await jobFunc(
                                    actionsPath,
                                    folderAbs,
                                    line => ReportProgress(jobId, group, total, completed,
                                        line, ct).GetAwaiter().GetResult(),
                                    line => ReportProgress(jobId, group, total, completed,
                                        $"[stderr] {line}", ct).GetAwaiter().GetResult(), ct);
                            }
                                break;
                            case JobType.ContentValidator:
                            {
                                // Pass the real folder name (relative segment) to the container
                                var folderName = row;
                                var tk = testKind;
                                exit = await _dockerFolderRunner.RunContentValidatorAsync(
                                    actionsPath,
                                    folderAbs,
                                    tk!,
                                    folderName,
                                    line => ReportProgress(jobId, group, total, completed,
                                        line, ct).GetAwaiter().GetResult(),
                                    line => ReportProgress(jobId, group, total, completed,
                                        $"[stderr] {line}", ct).GetAwaiter().GetResult(), ct);
                            }
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(jobType), jobType, null);
                        }

                        
                        Interlocked.Increment(ref completed);
                        if (exit != 0)
                        {
                            errors.Add($"{jobType} failed for '{row}' with exit code {exit}");
                        }

                        ReportProgress(jobId, group, total, completed,
                            $"Processed {completed}/{total} -> {Path.GetRelativePath(rootPath, folderAbs)}", ct).GetAwaiter().GetResult();
                    }
                    catch (Exception e)
                    {
                        errors.Add(e.Message);
                        Interlocked.Increment(ref completed);
                        
                        ReportProgress(jobId, group, total, completed,
                            $"Error: {e.Message}", ct).GetAwaiter().GetResult();
                        
                    }
                });

                if (!errors.IsEmpty)
                {
                    throw new AggregateException(errors.Select(e => new Exception(e)));
                }
  
                await _hub.Clients.Group(group).SendAsync("ReceiveCompleted", new
                {
                    jobId = jobId,
                    percent = ComputeCompleted(total, completed),
                    message = $"Job '{jobType}' completed. Processed {total} folders."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during job {JobId}", jobId);
                await _hub.Clients.Group(group).SendAsync("ReceiveError", new
                {
                    jobId = jobId,
                    message = ex.Message
                });
            }
        }));

        return jobId;
    }

    private async Task ReportProgress(string jobId, string group, int total, int completed, string message, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
            {
                jobId,
                percent = ComputeCompleted(total, completed),
                message = message
            }, ct);
        }
        catch
        {
            // ignored
        }
    }

    private static int ComputeCompleted(int total, int completed)
    {
        return completed * 100 / Math.Max(1, total);
    }

    private Func<string, string, Action<string>?, Action<string>?, CancellationToken, Task<int>> BuildJobFunc(JobType jobType, string jobId)
    {
        Func<string, string, Action<string>?, Action<string>?, CancellationToken, Task<int>> jobFunc = jobType switch
        {
            JobType.MetaUploader => (ap, hf, o, e, ct) => _dockerFolderRunner.RunMetaUploaderAsync(ap, hf, o, e, ct),
            JobType.AiContentQueryBuilder => (ap, hf, o, e, ct) => _dockerFolderRunner.RunAiContentQueryBuilderAsync(ap, hf, o, e, ct),
            JobType.AiContentAnswerBuilder => (ap, hf, o, e, ct) => _dockerFolderRunner.RunAiContentAnswerBuilderAsync(ap, hf, o, e, ct),
            JobType.EmbeddingDownloader => (ap, hf, o, e, ct) => _dockerFolderRunner.RunEmbeddingDownloaderAsync(ap, hf, o, e, ct),
            JobType.Md5ImageMarker => (ap, hf, o, e, ct) => _dockerFolderRunner.RunMd5ImageMarkerAsync(ap, hf, o, e, ct),
            JobType.DuplicateMarker => (ap, hf, o, e, ct) => _dockerFolderRunner.RunDuplicateMarkerAsync(ap, hf, o, e, ct),
            JobType.FaceHashBuilder => (ap, hf, o, e, ct) => _dockerFolderRunner.RunFaceHashBuilderAsync(ap, hf, o, e, ct),
            JobType.GroupFolderExtractor => (ap, hf, o, e, ct) => _dockerFolderRunner.RunGroupFolderExtractorAsync(ap, hf, o, e, ct),
            JobType.AverageImageMarker => (ap, hf, o, e, ct) => _dockerFolderRunner.RunAverageImageMarkerAsync(ap, hf, o, e, ct),
            _ => (ap, hf, o, e, ct) => _dockerFolderRunner.RunMetaUploaderAsync(ap, hf, o, e, ct)
        };
        return jobFunc;
    }
}
