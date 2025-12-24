using shared_csharp.Abstractions;

namespace content_validator.ContentTests;

internal sealed class ValidateEmbeddingsExistance(IFileSystem fs) : ContentValidationTest(fs)
{
    // Assert.True(File.Exists(PathExtensions.ResolveEmbAnswer(filePath)),
//     $"Embeddings file answer '{PathExtensions.ResolveEmbAnswer(filePath)}' does not exist.");
//         
// Assert.True(File.Exists(PathExtensions.ResolveEmbAnswer(filePath)),
//     $"Embeddings file conversation '{PathExtensions.ResolveEmbConversation(filePath)}' does not exist.");


// ValidatePreviews
// var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
// var previewFolder = Path.Combine(directoryName, "preview");
//
// var previewFileName = Path.GetFileNameWithoutExtension(filePath) + "_p" + previewKind + ".jpg";
// var previewPath = Path.Combine(previewFolder, previewFileName);
//
// Assert.True(File.Exists(previewPath), $"Preview file '{previewPath}' does not exist.");


    public override string Key { get; }

    protected override Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        throw new NotImplementedException();
    }
}