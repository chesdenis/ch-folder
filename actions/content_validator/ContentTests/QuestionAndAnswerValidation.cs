using shared_csharp.Abstractions;

namespace content_validator.ContentTests;

internal abstract class QuestionAndAnswerValidation(IFileSystem fs) : ContentValidationTest(fs)
{
    protected abstract Func<string, Task<string>> QuestionTextFunc { get; }
    protected abstract Func<string, Task<string>> AnswerTextFunc { get; }
    
    protected override async Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        try
        {
            var questionText = await QuestionTextFunc(filePath);
            if (string.IsNullOrWhiteSpace(questionText))
            {
                failures.Add(new { file = filePath, reason = $"Question content for '{Key}' is empty or missing." });
                return false;
            }
            
            var answerText = await AnswerTextFunc(filePath);
            if (string.IsNullOrWhiteSpace(answerText))
            {
                failures.Add(new { file = filePath, reason = $"Answer content for '{Key}' is empty or missing." });
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            failures.Add(new { file = filePath, reason = $"Fatal error for '{filePath}': {e.Message}" });
            return false;
        }
    }
}