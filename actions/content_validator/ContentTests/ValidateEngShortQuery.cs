using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class ValidateEngShortQuery(IFileSystem fs) : QuestionAndAnswerValidation(fs)
{
    public override string Key => "ESQ";

    protected override Func<string, string> QuestionPathFunc => PathExtensions.ResolveEngShortQuestionPath;
    protected override Func<string, string> AnswerPathFunc => PathExtensions.ResolveEngShortAnswerPath;
    protected override Func<string, Task<string>> QuestionTextFunc => fs.GetEngShortQuestion;
    protected override Func<string, Task<string>> AnswerTextFunc => fs.GetEngShortAnswer;
}