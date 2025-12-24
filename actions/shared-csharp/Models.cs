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

public static class ModelConstants
{
    public static readonly Tuple<string, string>[] ContentValidationKeys =
    [
        new("MD5_PFX", "Correct Md5 Prefix"),
        new("FNC", "Correct File Name Convention"),
        new("DQ", "Correct Description Query"),
        new("ESQ", "Correct Eng Short Query"),
        new("CMQ", "Correct Commerce Mark Query"),
        new("TQ", "Correct Tags Query")
    ];
}