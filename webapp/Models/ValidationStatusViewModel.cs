namespace webapp.Models;

public sealed class ValidationStatusViewModel
{
    public required string TestKind { get; set; }
    public IReadOnlyList<FolderStatus> Items { get; init; } = Array.Empty<FolderStatus>();
}

public sealed class FolderStatus
{
    public required string Folder { get; init; }
    public required string Status { get; init; }
}
