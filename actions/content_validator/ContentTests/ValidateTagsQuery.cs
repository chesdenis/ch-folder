using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class ValidateTagsQuery(IFileSystem fs) : QuestionAndAnswerValidation(fs)
{
    public override string Key => "TQ";
    
    protected override Func<string, Task<string>> QuestionTextFunc => fs.GetEng30TagsQuestion;
    protected override Func<string, Task<string>> AnswerTextFunc => fs.GetEng30TagsAnswer;
}