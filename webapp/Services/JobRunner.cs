using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using webapp.Hubs;
using webapp.Models;
using Microsoft.Extensions.Options;
using shared_csharp.Extensions;

namespace webapp.Services;

public interface IJobRunner
{
    string StartJob(string? folder);
}

public class JobRunner : IJobRunner
{
    private readonly IHubContext<JobStatusHub> _hub;
    private readonly ILogger<JobRunner> _logger;
    private readonly StorageOptions _storage;

    public JobRunner(IHubContext<JobStatusHub> hub, ILogger<JobRunner> logger, IOptions<StorageOptions> storage)
    {
        _hub = hub;
        _logger = logger;
        _storage = storage.Value;
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
                var root = _storage.RootPath ?? throw new InvalidOperationException("Storage root not specified in settings");
                await _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                {
                    jobId,
                    percent = 0,
                    message = $"Rebuild started. Root: '{root}'"
                });

                if (!Directory.Exists(root))
                {
                    throw new DirectoryNotFoundException($"Storage root '{root}' does not exist");
                }
                
                var storageFolders = PathExtensions.CollectStorageFolders(_storage.RootPath);
                var total = storageFolders.Length;
                var completed = 0;

                await _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                {
                    jobId,
                    percent = 0,
                    message = $"Discovered {total} image folders(s). Starting processing..."
                });

                foreach (var pathContainer in PathExtensions.GetFilesInFolder(root, storageFolders,
                             s =>
                             {
                                 completed++;
                                 
                                 _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                                 {
                                     jobId,
                                     percent = completed * 100 / total,
                                     message = $"Processed {completed} of {total} image folders(s)..."
                                 }).GetAwaiter().GetResult();
                             }))
                {
                    var filePath = pathContainer[0] as string;
                    if (filePath != null)
                    {
                        var _ = new FileInfo(filePath).Length;
                    }
                }
  
                await _hub.Clients.Group(group).SendAsync("ReceiveCompleted", new
                {
                    jobId,
                    percent = 100,
                    message = $"Rebuild completed. Processed {total} file(s)."
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
}
