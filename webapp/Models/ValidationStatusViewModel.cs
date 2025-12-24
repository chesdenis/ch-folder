namespace webapp.Models;

public sealed class ValidationStatusViewModel
{
    public IReadOnlyList<FolderStatus> Items { get; init; } = Array.Empty<FolderStatus>();
}

public sealed class FolderStatus
{
    public required string Folder { get; init; }
    public required string TestKind { get; init; }
    public required string Status { get; init; }
}

public record FolderAndTestKind(string Folder, string TestKind);
