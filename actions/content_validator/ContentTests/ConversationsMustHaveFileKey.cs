using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class ConversationsMustHaveFileKey(IFileSystem fs) : ContentValidationTest(fs)
{
    public override string Key => "KEY_IN_CONVS";

    protected override async Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        try
        {
            var fileKey = PathExtensions.ResolveFileKey(filePath);

            var commerceMarkConversation = await fs.GetCommerceMarkConversation(filePath);
            var engShortConversation = await fs.GetEngShortConversation(filePath);
            var eng30TagsConversation = await fs.GetEng30TagsConversation(filePath);
            var dqConversation = await fs.GetDqConversation(filePath);

            var isOk = true;

            if (!commerceMarkConversation.Contains(fileKey))
            {
                var s = $"Commerce mark file '{filePath}' does not contain '{fileKey}'";
                await log(new { message = s });
                failures.Add(new { file = filePath, reason = s });
                isOk = false;
            }

            if (!engShortConversation.Contains(fileKey))
            {
                var s = $"Eng short file '{filePath}' does not contain '{fileKey}'";
                await log(new { message = s });
                failures.Add(new { file = filePath, reason = s });
                isOk = false;
            }

            if (!eng30TagsConversation.Contains(fileKey))
            {
                var s = $"Eng 30 tags file '{filePath}' does not contain '{fileKey}'";
                await log(new { message = s });
                failures.Add(new { file = filePath, reason = s });
                isOk = false;
            }

            if (!dqConversation.Contains(fileKey))
            {
                var s = $"DQ file '{filePath}' does not contain '{fileKey}'";
                await log(new { message = s });
                failures.Add(new { file = filePath, reason = s });
                isOk = false;
            }
            
            return isOk;
        }
        catch (Exception e)
        {
            await log(new { message = $"Fatal error for '{filePath}': {e.Message}" });
            failures.Add(new { file = filePath, reason = $"Fatal error for '{filePath}': {e.Message}" });
            return false;
        }
    }
}