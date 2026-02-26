using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class ValidateDescriptionQuery(IFileSystem fs) : QuestionAndAnswerValidation(fs)
{
    public override string Key => "DQ";
    
    protected override Func<string, string> QuestionPathFunc => PathExtensions.ResolveDqQuestionPath;
    protected override Func<string, string> AnswerPathFunc => PathExtensions.ResolveDqAnswerPath;
    protected override Func<string, Task<string>> QuestionTextFunc => fs.GetDqQuestion;
    protected override Func<string, Task<string>> AnswerTextFunc => fs.GetDqAnswer;
}