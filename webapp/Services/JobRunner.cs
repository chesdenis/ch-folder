using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using webapp.Hubs;
using webapp.Models;
using Microsoft.Extensions.Options;
using shared_csharp.Extensions;

namespace webapp.Services;

public interface IJobRunner
{
    string StartJob(string? folder, string jobId);
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

    public string StartJob(string? folder, string jobId)
    {
        var id = string.IsNullOrWhiteSpace(jobId) ? Guid.NewGuid().ToString("N") : jobId;
        var group = JobStatusHub.GroupName(id);

        // Fire-and-forget background task
        _ = Task.Run(async () =>
        {
            try
            {
                var root = _storage.RootPath ?? throw new InvalidOperationException("Storage root not specified in settings");
                await _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                {
                    jobId = id,
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
                    jobId = id,
                    percent = 0,
                    message = $"Discovered {total} image folders(s). Starting processing..."
                });

                foreach (var pathContainer in PathExtensions.GetFilesInFolder(root, storageFolders,
                             s =>
                             {
                                 completed++;
                                 
                                 _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                                 {
                                     jobId = id,
                                     percent = completed * 100 / total,
                                     message = $"Processed {completed}/{total} -> {Path.GetRelativePath(root, s)}"
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
                    jobId = id,
                    percent = 100,
                    message = $"Rebuild completed. Processed {total} file(s)."
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
        });

        return id;
    }
}
