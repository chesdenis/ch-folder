namespace webapp.Models;

public sealed class ValidationDetailsViewModel
{
    public required string Folder { get; init; }
    public IReadOnlyList<ValidationDetailItem> Items { get; init; } = Array.Empty<ValidationDetailItem>();
}

public sealed class ValidationDetailItem
{
    public required string TestKind { get; init; }
    public required string Status { get; init; }
    public string? Details { get; init; }
    public ValidationDetailPayload? Parsed { get; init; }
}

public sealed class ValidationDetailPayload
{
    public int? Total { get; init; }
    public int? Mismatches { get; init; }
    public IReadOnlyList<FailureItem> Failures { get; init; } = Array.Empty<FailureItem>();
}

public sealed class FailureItem
{
    public string? File { get; init; }
    public string? Reason { get; init; }
}
