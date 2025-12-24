using shared_csharp.Abstractions;

namespace content_validator.ContentTests;

internal sealed class EmbeddingAnswersMustBeInsideConversation(IFileSystem fs) : ContentValidationTest(fs)
{
    // var embeddingAnswer = await _fileSystem.GetEmbAnswer(filePath);
// var embeddingConversation = await _fileSystem.GetEmbConversation(filePath);
//         
// var descriptionAnswer = await _fileSystem.GetDqAnswer(filePath);
//         
// Assert.True(embeddingConversation.Contains(embeddingAnswer), 
//     $"'embeddingAnswer {PathExtensions.ResolveEmbAnswer(filePath)}' does not have in conversation");
//         
// Assert.True(embeddingConversation.Contains(descriptionAnswer), 
//     $"'descriptionAnswer {PathExtensions.ResolveDqAnswerPath(filePath)}' does not have in conversation");


    public override string Key { get; }

    protected override Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        throw new NotImplementedException();
    }
}