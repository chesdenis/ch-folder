using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class QuestionsMustHaveFileKey(IFileSystem fs) : ContentValidationTest(fs)
{
    public override string Key => "KEY_IN_QUESTIONS";

    protected override async Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        try
        {
            var fileKey = PathExtensions.ResolveFileKey(filePath);

            var commerceMarkQuestion = await fs.GetCommerceMarkQuestion(filePath);
            var engShortQuestion = await fs.GetEngShortQuestion(filePath);
            var eng30TagsQuestion = await fs.GetEng30TagsQuestion(filePath);
            var dqQuestion = await fs.GetDqQuestion(filePath);

            if (!commerceMarkQuestion.Contains(fileKey))
            {
                var s = $"Commerce mark question '{filePath}' does not contain '{fileKey}'";
                await log(new { message = s });
                failures.Add(new { file = filePath, reason = s });
                return false;
            }

            if (!engShortQuestion.Contains(fileKey))
            {
                var s = $"Eng short question '{filePath}' does not contain '{fileKey}'";
                await log(new { message = s });
                failures.Add(new { file = filePath, reason = s });
                return false;
            }

            if (!eng30TagsQuestion.Contains(fileKey))
            {
                var s = $"Eng 30 tags question '{filePath}' does not contain '{fileKey}'";
                await log(new { message = s });
                failures.Add(new { file = filePath, reason = s });
                return false;
            }

            if (!dqQuestion.Contains(fileKey))
            {
                var s = $"Description question '{filePath}' does not contain '{fileKey}'";
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