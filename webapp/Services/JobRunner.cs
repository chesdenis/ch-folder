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
    Md5ImageMarker,
    DuplicateMarker,
    FaceHashBuilder,
    AverageImageMarker
}

public interface IJobRunner
{
    string StartJob(string jobId, JobType jobType, string workingFolder, int? degreeOfParallelism = null);
}

public class JobRunner : IJobRunner
{
    private readonly IHubContext<JobStatusHub> _hub;
    private readonly ILogger<JobRunner> _logger;
    private readonly StorageOptions _storage;
    private readonly IDockerFolderRunner _dockerFolder;

    public JobRunner(
        IHubContext<JobStatusHub> hub, ILogger<JobRunner> logger, IOptions<StorageOptions> storage, IDockerFolderRunner dockerFolder, IImageLocator imageLocator)
    {
        _hub = hub;
        _logger = logger;
        _storage = storage.Value;
        _dockerFolder = dockerFolder;
    }

    public string StartJob(string jobId, JobType jobType, string workingFolder, int? degreeOfParallelism = null)
    {
        var id = string.IsNullOrWhiteSpace(jobId) ? Guid.NewGuid().ToString("N") : jobId;
        var group = JobStatusHub.GroupName(id);

        // Fire-and-forget background task
        _ = Task.Run((Func<Task>)(async () =>
        {
            try
            {
                var rootPath = workingFolder ?? throw new InvalidOperationException("Working folder was not provided");
                var actionsPath =_storage.ActionsPath ?? throw new InvalidOperationException("Actions root not specified in settings");
                    
                await _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                {
                    jobId = id,
                    percent = 0,
                    message = $"Job '{jobType}' started. Root: '{rootPath}'"
                });

                if (!Directory.Exists(rootPath))
                {
                    throw new DirectoryNotFoundException($"Working folder '{rootPath}' does not exist");
                }
                
                var storageFolders = PathExtensions.GetStorageFolders(rootPath).ToArray();
                var total = storageFolders.Length;
                var completed = 0;
                var dop = Math.Max(1, degreeOfParallelism ?? Math.Min(Environment.ProcessorCount, 4));

                await _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                {
                    jobId = id,
                    percent = ComputeCompleted(total, ref completed),
                    message = $"Discovered {total} folder(s). Starting '{jobType}' with DOP={dop}..."
                });

                // Map job to appropriate docker runner function (unify signatures via wrappers)
                var jobFunc = BuildJobFunc(jobType);

                var errors = new ConcurrentBag<string>();

                await Parallel.ForEachAsync(storageFolders, new ParallelOptions { MaxDegreeOfParallelism = dop }, async (row, ct) =>
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(row)) return;

                        var folderAbs = Path.GetFullPath(Path.Combine(rootPath, row));
                        if (!Directory.Exists(folderAbs)) return;

                        await _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                        {
                            jobId = id,
                            percent = ComputeCompleted(total, ref completed),
                            message = $"Starting: {Path.GetRelativePath(rootPath, folderAbs)}"
                        }, cancellationToken: ct);

                        var exit = await jobFunc(
                            actionsPath,
                            folderAbs,
                            line =>
                            {
                                try
                                {
                                    _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                                    {
                                        jobId = id,
                                        percent = ComputeCompleted(total, ref completed),
                                        message = line
                                    }, cancellationToken: ct).GetAwaiter().GetResult();
                                }
                                catch { /* ignore hub send errors per-line */ }
                            },
                            line =>
                            {
                                try
                                {
                                    _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                                    {
                                        jobId = id,
                                        percent = ComputeCompleted(total, ref completed),
                                        message = $"[stderr] {line}"
                                    }, cancellationToken: ct).GetAwaiter().GetResult();
                                }
                                catch { }
                            },
                            ct);

                        Interlocked.Increment(ref completed);
                        if (exit != 0)
                        {
                            errors.Add($"{jobType} failed for '{row}' with exit code {exit}");
                        }

                        await _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                        {
                            jobId = id,
                            percent = ComputeCompleted(total, ref completed),
                            message = $"Processed {completed}/{total} -> {Path.GetRelativePath(rootPath, folderAbs)}"
                        }, cancellationToken: ct);
                    }
                    catch (Exception e)
                    {
                        errors.Add(e.Message);
                        Interlocked.Increment(ref completed);
                        await _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                        {
                            jobId = id,
                            percent = ComputeCompleted(total, ref completed),
                            message = $"Error: {e.Message}"
                        }, cancellationToken: ct);
                    }
                });

                if (!errors.IsEmpty)
                {
                    throw new AggregateException(errors.Select(e => new Exception(e)));
                }
  
                await _hub.Clients.Group(group).SendAsync("ReceiveCompleted", new
                {
                    jobId = id,
                    percent = ComputeCompleted(total, ref completed),
                    message = $"Job '{jobType}' completed. Processed {total} folders."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during job {JobId}", id);
                await _hub.Clients.Group(group).SendAsync("ReceiveError", new
                {
                    jobId = id,
                    message = ex.Message
                });
            }
        }));

        return id;
    }

    private static int ComputeCompleted(int total, ref int completed)
    {
        return Volatile.Read(ref completed) * 100 / Math.Max(1, total);
    }

    private Func<string, string, Action<string>?, Action<string>?, CancellationToken, Task<int>> BuildJobFunc(JobType jobType)
    {
        Func<string, string, Action<string>?, Action<string>?, CancellationToken, Task<int>> jobFunc = jobType switch
        {
            JobType.MetaUploader => (ap, hf, o, e, ct) => _dockerFolder.RunMetaUploaderAsync(ap, hf, o, e, ct),
            JobType.AiContentQueryBuilder => (ap, hf, o, e, ct) => _dockerFolder.RunAiContentQueryBuilderAsync(ap, hf, o, e, ct),
            JobType.AiContentAnswerBuilder => (ap, hf, o, e, ct) => _dockerFolder.RunAiContentAnswerBuilderAsync(ap, hf, o, e, ct),
            JobType.Md5ImageMarker => (ap, hf, o, e, ct) => _dockerFolder.RunMd5ImageMarkerAsync(ap, hf, o, e, ct),
            JobType.DuplicateMarker => (ap, hf, o, e, ct) => _dockerFolder.RunDuplicateMarkerAsync(ap, hf, o, e, ct),
            JobType.FaceHashBuilder => (ap, hf, o, e, ct) => _dockerFolder.RunFaceHashBuilderAsync(ap, hf, o, e, ct),
            JobType.AverageImageMarker => (ap, hf, o, e, ct) => _dockerFolder.RunAverageImageMarkerAsync(ap, hf, o, e, ct),
            _ => (ap, hf, o, e, ct) => _dockerFolder.RunMetaUploaderAsync(ap, hf, o, e, ct)
        };
        return jobFunc;
    }
}
