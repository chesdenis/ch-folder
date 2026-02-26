namespace webapp.Models;

public sealed class BackupStatusViewModel
{
    public IReadOnlyList<BackupFolderStatus> Folders { get; init; } = Array.Empty<BackupFolderStatus>();
}

public sealed class BackupFolderStatus
{
    public required string FolderName { get; init; }
    public int TotalFiles { get; init; }
    public int BackedUpFiles { get; init; }
    public string Status { get; init; } = "Pending";
}
