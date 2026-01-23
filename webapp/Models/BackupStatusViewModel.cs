namespace webapp.Models;

public sealed class BackupStatusViewModel
{
    public IReadOnlyList<BackupItemStatus> Items { get; init; } = Array.Empty<BackupItemStatus>();
}

public sealed class BackupItemStatus
{
    public required string Md5Hash { get; init; }
    public required string Status { get; init; }
    public string? ErrorMessage { get; init; }
}
