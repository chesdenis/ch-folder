using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class EmbeddingAnswersMustBeInsideConversation(IFileSystem fs) : ContentValidationTest(fs)
{
    public override string Key => "EMB_ANS_IN_CONV";

    protected override async Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        try
        {
            var embeddingAnswer = await fs.GetEmbAnswer(filePath);
            var embeddingConversation = await fs.GetEmbConversation(filePath);
            var descriptionAnswer = await fs.GetDqAnswer(filePath);
            var isOk = true;
            
            if (!embeddingConversation.Contains(embeddingAnswer))
            {
                var s =
                    $"Embedding answer '{PathExtensions.ResolveEmbAnswer(filePath)}' does not have in conversation '{filePath}'";
                failures.Add(new { file = filePath, reason = s });
                isOk = false;
            }

            if (!embeddingConversation.Contains(descriptionAnswer))
            {
                var s =
                    $"'DescriptionAnswer {PathExtensions.ResolveDqAnswerPath(filePath)}' does not have in conversation";
                failures.Add(new { file = filePath, reason = s });
                isOk = false;
            }
            
            return isOk;
        }
        catch (Exception e)
        {
            failures.Add(new { file = filePath, reason = $"Fatal error for '{filePath}': {e.Message}" });
            return false;
        }
    }
}