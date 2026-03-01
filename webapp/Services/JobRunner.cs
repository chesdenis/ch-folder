using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using webapp.Hubs;
using shared_csharp.Extensions;

namespace webapp.Services;

public enum JobType
{
    MetaUploader
}

public interface IJobRunner
{
    string StartJob(
        string jobId,
        JobType jobType,
        int? degreeOfParallelism = null);
}

public class JobRunner : IJobRunner
{
    private readonly IHubContext<JobStatusHub> _hub;
    private readonly ILogger<JobRunner> _logger;
    private readonly IDockerPartitionRunner _dockerPartitionRunner;

    public JobRunner(
        IHubContext<JobStatusHub> hub, ILogger<JobRunner> logger,
        IDockerPartitionRunner dockerPartitionRunner)
    {
        _hub = hub;
        _logger = logger;
        _dockerPartitionRunner = dockerPartitionRunner;
    }

    public string StartJob(
        string jobId,
        JobType jobType,
        int? degreeOfParallelism = null)
    {
        var group = JobStatusHub.GroupName(jobId);

        // Fire-and-forget background task
        _ = Task.Run((Func<Task>)(async () =>
        {
            try
            {
                var total = 256;
                var completed = 0;

                var dop = Math.Max(1, degreeOfParallelism ?? Math.Min(Environment.ProcessorCount, 4));

                await ReportProgress(jobId, group, total, completed, $"Job '{jobType}' started");

                await ReportProgress(jobId, group, total, completed,
                    $"Discovered {total} partitions. Starting '{jobType}' with DOP={dop}...");

                var errors = new ConcurrentBag<string>();

                await Parallel.ForEachAsync(Enumerable.Range(0, 255), new ParallelOptions { MaxDegreeOfParallelism = dop },
                    async (partition, ct) =>
                    {
                        try
                        {
                            await ReportProgress(jobId, group, total, completed,
                                $"Starting: {partition}", ct);

                            int exit = 0;
                            switch (jobType)
                            {
                                case JobType.MetaUploader:
                                {
                                    // Map job to appropriate docker runner function (unify signatures via wrappers)
                                    exit = await _dockerPartitionRunner.RunMetaUploaderAsync(
                                        line => 
                                            ReportProgress(jobId, group, total, completed, line, ct)
                                                .GetAwaiter().GetResult(),
                                        line => ReportProgress(jobId, group, total, completed,
                                            $"[stderr] {line}", ct)
                                            .GetAwaiter().GetResult(), ct, partition.ToString());
                                }
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(nameof(jobType), jobType, null);
                            }


                            Interlocked.Increment(ref completed);
                            if (exit != 0)
                            {
                                errors.Add($"{jobType} failed for '{partition}' with exit code {exit}");
                            }

                            ReportProgress(jobId, group, total, completed,
                                    $"Processed {completed}/{total} -> {partition}", ct)
                                .GetAwaiter().GetResult();
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

    private async Task ReportProgress(string jobId, string group, int total, int completed, string message,
        CancellationToken ct = default)
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
}