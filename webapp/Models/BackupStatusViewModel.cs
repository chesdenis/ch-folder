namespace webapp.Models;

public sealed class BackupStatusViewModel
{
    public IReadOnlyList<BackupFolderStatus> Items { get; init; } = Array.Empty<BackupFolderStatus>();
    public IReadOnlyList<string> Activities { get; init; } = Array.Empty<string>();
}

public sealed class BackupFolderStatus
{
    public required string Partition { get; init; }
    public required string Folder { get; init; }
    public Dictionary<string, string> ActivityStatuses { get; init; } = new();
}
