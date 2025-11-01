namespace shared_csharp;

public sealed record EmbeddingFile(
    string @object,
    IReadOnlyList<EmbeddingData> data,
    string model,
    Usage usage
);

public sealed record EmbeddingData(
    string @object,
    int index,
    IReadOnlyList<float> embedding
);

public sealed record Usage(
    int prompt_tokens,
    int total_tokens
);