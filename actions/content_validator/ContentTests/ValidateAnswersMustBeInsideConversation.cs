using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class ValidateAnswersMustBeInsideConversation(IFileSystem fs) : ContentValidationTest(fs)
{
    public override string Key => "ANS_INSIDE_CONV";

    protected override async Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        try
        {
            var commerceMarkConversation = await fs.GetCommerceMarkConversation(filePath);
            var engShortConversation = await fs.GetEngShortConversation(filePath);
            var eng30TagsConversation = await fs.GetEng30TagsConversation(filePath);
            var dqConversation = await fs.GetDqConversation(filePath);

            var commerceMarkAnswer = await fs.GetCommerceMarkAnswer(filePath);
            var engShortAnswer = await fs.GetEngShortAnswer(filePath);
            var eng30TagsAnswer = await fs.GetEng30TagsAnswer(filePath);
            var dqAnswer = await fs.GetDqAnswer(filePath);

            if (!commerceMarkConversation.Contains(commerceMarkAnswer))
            {
                var s =
                    $"CommerceMark answer '{PathExtensions.ResolveCommerceMarkAnswerPath(filePath)}' does not have in conversation.";
                await log(new { message = s });
                failures.Add(new { file = filePath, reason = s });
                return false;
            }

            if (!engShortConversation.Contains(engShortAnswer))
            {
                var s =
                    $"EngShort answer '{PathExtensions.ResolveEngShortConversationPath(filePath)}' does not exist in conversation.";
                await log(new { message = s });
                failures.Add(new { file = filePath, reason = s });
                return false;
            }

            if (!eng30TagsConversation.Contains(eng30TagsAnswer))
            {
                var s =
                    $"Eng30Tags answer '{PathExtensions.ResolveEng30TagsConversationPath(filePath)}' does not exist in conversation.";
                await log(new { message = s });
                failures.Add(new { file = filePath, reason = s });
                return false;
            }

            if (!dqConversation.Contains(dqAnswer))
            {
                var s =
                    $"DQ answer '{PathExtensions.ResolveDqConversationPath(filePath)}' does not exist in conversation.";
                await log(new { message = s });
                failures.Add(new { file = filePath, reason = s });
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            await log(new { message = $"Fatal error for '{filePath}': {e.Message}" });
            failures.Add(new { file = filePath, reason = $"Fatal error for '{filePath}': {e.Message}" });
            return false;
        }
    }
}