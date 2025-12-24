using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class ValidateDescriptionAnswerMustHaveMultipleSentences(IFileSystem fs) : ContentValidationTest(fs)
{
    public override string Key => "DQ_ANS_MULT";

    protected override async Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        try
        {
            var path = PathExtensions.ResolveDqAnswerPath(filePath);
            var pathExist = fs.FileExists(path);
            if (!pathExist)
            {
                var s = $"Description does not exist: {Path.GetFileName(filePath)}";
                await log(new { message = s });
                failures.Add(new { file = filePath, reason = s });

                return false;
            }

            var answerText = await fs.GetDqAnswer(filePath);
            var sentences = answerText.Split(".");

            if (sentences.Length < 3)
            {
                var s = $"Answer file '{path}' must have multiple sentences.";
                await log(new { message = s });
                failures.Add(new { file = filePath, reason = s });
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