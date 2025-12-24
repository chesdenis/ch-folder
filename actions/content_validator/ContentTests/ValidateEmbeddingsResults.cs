using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class ValidateEmbeddingsResults(IFileSystem fs) : ContentValidationTest(fs)
{
    public override string Key => "EMB_OK";

    protected override async Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        try
        {
            var embAnswerExists = fs.FileExists(PathExtensions.ResolveEmbAnswer(filePath));
            var embConversationExists = fs.FileExists(PathExtensions.ResolveEmbConversation(filePath));

            if (!embAnswerExists)
            {
                var s = $"Embeddings file does not exist: {Path.GetFileName(filePath)}";
                await log(new { message = s });
                failures.Add(new { file = filePath, reason = s });
                return false;
            }

            if (!embConversationExists)
            {
                var s = $"Embeddings conversation does not exist: {Path.GetFileName(filePath)}";
                await log(new { message = s });
                failures.Add(new { file = filePath, reason = s });
                return false;
            }
        }
        catch (Exception e)
        {
            await log(new { message = $"Fatal error for '{filePath}': {e.Message}" });
            failures.Add(new { file = filePath, reason = $"Fatal error for '{filePath}': {e.Message}" });
            return false;
        }
        
        return true;
    }
}