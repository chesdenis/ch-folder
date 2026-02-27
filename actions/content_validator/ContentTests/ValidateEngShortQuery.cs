using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class ValidateEngShortQuery(IFileSystem fs) : QuestionAndAnswerValidation(fs)
{
    public override string Key => "ESQ";
    
    protected override Func<string, Task<string>> QuestionTextFunc => fs.GetEngShortQuestion;
    protected override Func<string, Task<string>> AnswerTextFunc => fs.GetEngShortAnswer;
}