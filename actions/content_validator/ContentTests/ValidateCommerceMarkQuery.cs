using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class ValidateCommerceMarkQuery(IFileSystem fs) : QuestionAndAnswerValidation(fs)
{
    public override string Key => "CMQ";
    
    protected override Func<string, Task<string>> QuestionTextFunc => fs.GetCommerceMarkQuestion;
    protected override Func<string, Task<string>> AnswerTextFunc => fs.GetCommerceMarkAnswer;
}
 