using Microsoft.AspNetCore.SignalR;

namespace webapp.Hubs;

public class JobStatusHub : Hub
{
    public async Task JoinJobGroup(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(jobId));
    }

    public static string GroupName(string jobId) => $"job-{jobId}";
}
