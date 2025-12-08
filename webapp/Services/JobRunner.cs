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
    private readonly IDockerRunner _docker;

    public JobRunner(IHubContext<JobStatusHub> hub, ILogger<JobRunner> logger, IOptions<StorageOptions> storage, IDockerRunner docker)
    {
        _hub = hub;
        _logger = logger;
        _storage = storage.Value;
        _docker = docker;
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
                var rootPath = _storage.RootPath ?? throw new InvalidOperationException("Storage root not specified in settings");
                var actionsPath =_storage.ActionsPath ?? throw new InvalidOperationException("Actions root not specified in settings");
                    
                await _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                {
                    jobId = id,
                    percent = 0,
                    message = $"Rebuild started. Root: '{rootPath}'"
                });

                if (!Directory.Exists(rootPath))
                {
                    throw new DirectoryNotFoundException($"Storage root '{rootPath}' does not exist");
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

                foreach (var row in storageFolders)
                {
                    var rel = row.Length > 0 ? row[0] as string : null;
                    if (string.IsNullOrWhiteSpace(rel))
                        continue;

                    var folderAbs = Path.GetFullPath(Path.Combine(rootPath, rel));
                    if (!Directory.Exists(folderAbs))
                        continue;

                    await _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                    {
                        jobId = id,
                        percent = completed * 100 / Math.Max(1, total),
                        message = $"Starting: {Path.GetRelativePath(rootPath, folderAbs)}"
                    });

                    var exit = await _docker.RunMetaUploaderAsync(
                        actionsPath,
                        folderAbs,
                        onStdout: line =>
                        {
                            try
                            {
                                _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                                {
                                    jobId = id,
                                    percent = completed * 100 / Math.Max(1, total),
                                    message = line
                                }).GetAwaiter().GetResult();
                            }
                            catch { /* ignore hub send errors per-line */ }
                        },
                        onStderr: line =>
                        {
                            try
                            {
                                _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                                {
                                    jobId = id,
                                    percent = completed * 100 / Math.Max(1, total),
                                    message = $"[stderr] {line}"
                                }).GetAwaiter().GetResult();
                            }
                            catch { }
                        });

                    completed++;

                    if (exit != 0)
                    {
                        throw new Exception($"meta_uploader failed for '{rel}' with exit code {exit}");
                    }

                    await _hub.Clients.Group(group).SendAsync("ReceiveProgress", new
                    {
                        jobId = id,
                        percent = completed * 100 / Math.Max(1, total),
                        message = $"Processed {completed}/{total} -> {Path.GetRelativePath(rootPath, folderAbs)}"
                    });
                }
  
                await _hub.Clients.Group(group).SendAsync("ReceiveCompleted", new
                {
                    jobId = id,
                    percent = 100,
                    message = $"Rebuild completed. Processed {total} folders."
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
