using shared_csharp.Abstractions;

namespace content_validator.ContentTests;

internal sealed class ValidateTagsAnswersStructure(IFileSystem fs) : ContentValidationTest(fs)
{
    // var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
// var dqFolder = Path.Combine(directoryName, "eng30tags");
//
// var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
// if (groupName.Length != 4)
// {
//     groupName = Path.GetFileNameWithoutExtension(filePath);
// }
//
// var eng30TagsFilePath = Path.Combine(dqFolder, groupName + ".eng30tags.md.answer.md");
//
// Assert.True(File.Exists(eng30TagsFilePath),
//     $"Answer file '{eng30TagsFilePath}' does not exist.");
//
// var content = await File.ReadAllTextAsync(eng30TagsFilePath);
// var tags = content.Split(',').Select(s => s.Trim()).ToArray();
//
// if (tags.Length == 1)
// {
//     testOutputHelper.WriteLine(eng30TagsFilePath);
// }
//
// Assert.True(tags.Length > 5);
    public override string Key { get; }

    protected override Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        throw new NotImplementedException();
    }
}