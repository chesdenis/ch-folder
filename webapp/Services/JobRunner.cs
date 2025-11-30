using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using webapp.Hubs;

namespace webapp.Services;

public interface IJobRunner
{
    string StartJob(string? folder);
}

public class JobRunner : IJobRunner
{
    private readonly IHubContext<JobStatusHub> _hub;
    private readonly ILogger<JobRunner> _logger;

    public JobRunner(IHubContext<JobStatusHub> hub, ILogger<JobRunner> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public string StartJob(string? folder)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var group = JobStatusHub.GroupName(jobId);

        // Fire-and-forget background task
        _ = Task.Run(async () =>
        {
            try
            {
                var steps = SimulateEnumerableWalk(folder).ToArray();
                var total = steps.Length;
                var completed = 0;

                await _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                {
                    jobId,
                    percent = 0,
                    message = $"Job started for '{folder ?? "(no folder)"}'",
                });

                foreach (var step in steps)
                {
                    // simulate long work per item
                    await Task.Delay(step.delayMs);
                    completed++;
                    var percent = (int)Math.Round(completed * 100.0 / Math.Max(1, total));
                    await _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                    {
                        jobId,
                        percent,
                        message = step.message
                    });
                }

                await _hub.Clients.Group(group).SendAsync("ReceiveCompleted", new
                {
                    jobId,
                    percent = 100,
                    message = "Job completed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during job {JobId}", jobId);
                await _hub.Clients.Group(group).SendAsync("ReceiveError", new
                {
                    jobId,
                    message = ex.Message
                });
            }
        });

        return jobId;
    }

    private static IEnumerable<(string message, int delayMs)> SimulateEnumerableWalk(string? folder)
    {
        var rand = new Random();
        var root = folder ?? "hive:/";
        // Simulate 25 items of work
        for (var i = 1; i <= 25; i++)
        {
            var path = $"{root.TrimEnd('/')}/item-{i:000}";
            var delay = rand.Next(100, 500);
            yield return ($"Processed {path}", delay);
        }
    }
}
