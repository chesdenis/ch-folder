using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class ConversationsMustHaveFileKey(IFileSystem fs) : ContentValidationTest(fs)
{
    public override string Key => "KEY_IN_CONVS";

    protected override Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        throw new NotImplementedException();
    }
    // var fileKey = PathExtensions.ResolveFileKey(filePath);
//
// var commerceMarkConversation = await _fileSystem.GetCommerceMarkConversation(filePath);
// var engShortConversation = await _fileSystem.GetEngShortConversation(filePath);
// var eng30TagsConversation = await _fileSystem.GetEng30TagsConversation(filePath);
// var dqConversation = await _fileSystem.GetDqConversation(filePath);
//
// // file key must be inside question and inside conversation. This is because we pass preview for each input. 
// Assert.True(commerceMarkConversation.Contains(fileKey),
//     $"'commerceMarkConversation {filePath}' does not contain '{fileKey}'");
// Assert.True(engShortConversation.Contains(fileKey),
//     $"'engShortConversation {filePath}' does not contain '{fileKey}'");
// Assert.True(eng30TagsConversation.Contains(fileKey),
//     $"'eng30TagsConversation {filePath}' does not contain '{fileKey}'");
// Assert.True(dqConversation.Contains(fileKey),
//     $"'dqConversation {filePath}' does not contain '{fileKey}'");
}