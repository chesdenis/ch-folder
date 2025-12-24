using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class ValidateTagsQuery(IFileSystem fs) : QuestionAndAnswerValidation(fs)
{
    public override string Key => "TQ";

    protected override Func<string, string> QuestionPathFunc => PathExtensions.ResolveEng30TagsQuestionPath;
    protected override Func<string, string> AnswerPathFunc => PathExtensions.ResolveEng30TagsAnswerPath;
    protected override Func<string, Task<string>> QuestionTextFunc => fs.GetEng30TagsQuestion;
    protected override Func<string, Task<string>> AnswerTextFunc => fs.GetEng30TagsAnswer;
}