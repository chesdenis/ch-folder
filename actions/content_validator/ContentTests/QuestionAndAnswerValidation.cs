using shared_csharp.Abstractions;

namespace content_validator.ContentTests;

internal abstract class QuestionAndAnswerValidation(IFileSystem fs) : ContentValidationTest(fs)
{
    protected abstract Func<string, string> QuestionPathFunc { get; }
    protected abstract Func<string, string> AnswerPathFunc { get; }
    protected abstract Func<string, Task<string>> QuestionTextFunc { get; }
    protected abstract Func<string, Task<string>> AnswerTextFunc { get; }
    
    protected override async Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        try
        {
            var questionPath = QuestionPathFunc(filePath);
            var questionPathExist = fs.FileExists(questionPath);
            
            if (!await ValidateFileExists(log, filePath, failures, questionPathExist, questionPath)) return false;
            if (!await ValidateQuestionContents(log, filePath, failures, questionPath)) return false;
            
            var answerPath = AnswerPathFunc(filePath);
            var answerPathExist = fs.FileExists(answerPath);
            if (!await ValidateFileExists(log, filePath, failures, answerPathExist, answerPath)) return false;
            if (!await ValidateAnswerContent(log, filePath, failures, answerPath)) return false;

            return true;
        }
        catch (Exception e)
        {
            await log(new { message = $"Fatal error for '{filePath}': {e.Message}" });
            failures.Add(new { file = filePath, reason = $"Fatal error for '{filePath}': {e.Message}" });
            return false;
        }
    }

    private async Task<bool> ValidateQuestionContents(Func<dynamic, Task> log, string filePath, List<object> failures, string path)
    {
        try
        {
            var questionText = await QuestionTextFunc(filePath);
            var isEmpty = string.IsNullOrWhiteSpace(questionText);
            if (isEmpty)
            {
                var m = $"Question file '{path}' is empty.";
                await log(new { message = m });
                failures.Add(new { file = filePath, reason = m });
                return false;
            }
        }
        catch (Exception e)
        {
            var s = $"Error reading question file '{path}': {e.Message}";
                
            await log(new { message = s });
            failures.Add(new { file = filePath, reason = s });
            return false;
        }

        return true;
    } 
    
    private async Task<bool> ValidateAnswerContent(Func<dynamic, Task> log, string filePath, List<object> failures, string path)
    {
        try
        {
            var answerText = await AnswerTextFunc(filePath);
            var isEmpty = string.IsNullOrWhiteSpace(answerText);
            if (isEmpty)
            {
                var m = $"Answer file '{path}' is empty.";
                await log(new { message = m });
                failures.Add(new { file = filePath, reason = m });
                return false;
            }
        }
        catch (Exception e)
        {
            var s = $"Error reading answer file '{path}': {e.Message}";
                
            await log(new { message = s });
            failures.Add(new { file = filePath, reason = s });
            return false;
        }

        return true;
    }

    private static async Task<bool> ValidateFileExists(Func<dynamic, Task> log, string filePath, List<object> failures, bool pathExist, string path)
    {
        if (!pathExist)
        {
            var m = $"File '{path}' does not exist.";
            await log(new { message = m });
            failures.Add(new { file = filePath, reason = m });
            return false;
        }

        return true;
    }
}