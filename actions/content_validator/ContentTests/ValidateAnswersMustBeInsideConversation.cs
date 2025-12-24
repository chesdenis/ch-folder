using shared_csharp.Abstractions;

namespace content_validator.ContentTests;

internal sealed class ValidateAnswersMustBeInsideConversation(IFileSystem fs, string key) : ContentValidationTest(fs)
{
    public override string Key { get; } = key;
    protected override Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        throw new NotImplementedException();
    }

    // var commerceMarkConversation = await _fileSystem.GetCommerceMarkConversation(filePath);
    // var engShortConversation = await _fileSystem.GetEngShortConversation(filePath);
    // var eng30TagsConversation = await _fileSystem.GetEng30TagsConversation(filePath);
    // var dqConversation = await _fileSystem.GetDqConversation(filePath);
    //
    // var commerceMarkAnswer = await _fileSystem.GetCommerceMarkAnswer(filePath);
    // var engShortAnswer = await _fileSystem.GetEngShortAnswer(filePath);
    // var eng30TagsAnswer = await _fileSystem.GetEng30TagsAnswer(filePath);
    // var dqAnswer = await _fileSystem.GetDqAnswer(filePath);
    //
    // // each answer must be inside conversation:
    // Assert.True(commerceMarkConversation.Contains(commerceMarkAnswer), 
    // $"'commerceMarkAnswer {PathExtensions.ResolveCommerceMarkAnswerPath(filePath)}' does not have in conversation");
    // Assert.True(engShortConversation.Contains(engShortAnswer),
    // $"'engShortAnswer {PathExtensions.ResolveEngShortConversationPath(filePath)}' does not exist in conversation");
    // Assert.True(eng30TagsConversation.Contains(eng30TagsAnswer),
    // $"'eng30TagsAnswer {PathExtensions.ResolveEng30TagsConversationPath(filePath)}' does not exist in conversation");
    // Assert.True(dqConversation.Contains(dqAnswer),
    // $"'dqAnswer {PathExtensions.ResolveDqConversationPath(filePath)}' does not exist in conversation");

}