using shared_csharp.Abstractions;

namespace content_validator.ContentTests;

internal sealed class QuestionsMustHaveFileKey(IFileSystem fs) : ContentValidationTest(fs)
{
    // var fileKey = PathExtensions.ResolveFileKey(filePath);
//
// var commerceMarkQuestion = await _fileSystem.GetCommerceMarkQuestion(filePath);
// var engShortQuestion = await _fileSystem.GetEngShortQuestion(filePath);
// var eng30TagsQuestion = await  _fileSystem.GetEng30TagsQuestion(filePath);
// var dqQuestion = await  _fileSystem.GetDqQuestion(filePath);
//
// // file key must be inside question and inside conversation. This is because we pass preview for each input. 
// Assert.True(commerceMarkQuestion.Contains(fileKey),
//     $"'commerceMarkQuestion {filePath}' does not contain '{fileKey}'");
// Assert.True(engShortQuestion.Contains(fileKey), $"'engShortQuestion {filePath}' does not contain '{fileKey}'");
// Assert.True(eng30TagsQuestion.Contains(fileKey),
//     $"'eng30TagsQuestion {filePath}' does not contain '{fileKey}'");
// Assert.True(dqQuestion.Contains(fileKey), $"'dqQuestion {filePath}' does not contain '{fileKey}'");

    public override string Key { get; }

    protected override Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        throw new NotImplementedException();
    }
}