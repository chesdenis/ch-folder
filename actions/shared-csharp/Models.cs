using Newtonsoft.Json;

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

public record CommerceJson(
    [property: JsonProperty("rate")] int Rate,
    [property: JsonProperty("rate-explanation")]
    string RateExplanation
);

public record FileMetadata(
    [property: JsonProperty("partition")] string? Partition,
    [property: JsonProperty("section")] string? Section,
    [property: JsonProperty("group")] string? Group,
    [property: JsonProperty("averageHash")] string? AverageHash,
    [property: JsonProperty("colorHash")] string? ColorHash,
    [property: JsonProperty("description")] string? Description,
    [property: JsonProperty("tags")] IReadOnlyList<string>? Tags,
    [property: JsonProperty("previews")] IReadOnlyDictionary<string, string>? Previews,
    [property: JsonProperty("embAnswer")] string? EmbAnswer,
    [property: JsonProperty("embConversation")] string? EmbConversation,
    [property: JsonProperty("dqQuestion")] string? DqQuestion,
    [property: JsonProperty("dqAnswer")] string? DqAnswer,
    [property: JsonProperty("dqConversation")] string? DqConversation,
    [property: JsonProperty("commerceMarkQuestion")] string? CommerceMarkQuestion,
    [property: JsonProperty("commerceMarkAnswer")] string? CommerceMarkAnswer,
    [property: JsonProperty("commerceMarkConversation")] string? CommerceMarkConversation,
    [property: JsonProperty("eng30TagsQuestion")] string? Eng30TagsQuestion,
    [property: JsonProperty("eng30TagsAnswer")] string? Eng30TagsAnswer,
    [property: JsonProperty("eng30TagsConversation")] string? Eng30TagsConversation,
    [property: JsonProperty("engShortQuestion")] string? EngShortQuestion,
    [property: JsonProperty("engShortAnswer")] string? EngShortAnswer,
    [property: JsonProperty("engShortConversation")] string? EngShortConversation,
    [property: JsonProperty("ext")] string? Ext,
    [property: JsonProperty("md5")] string? Md5
);