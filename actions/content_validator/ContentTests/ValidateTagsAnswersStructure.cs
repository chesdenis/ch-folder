using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class ValidateTagsAnswersStructure(IFileSystem fs) : ContentValidationTest(fs)
{
    public override string Key => "TAGS_OK";

    protected override async Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        try
        {
            var tagsPath = PathExtensions.ResolveEng30TagsAnswerPath(filePath);
            var tagsExist = fs.FileExists(tagsPath);

            if (!tagsExist)
            {
                var s = $"Eng30Tags answer does not exist: {Path.GetFileName(filePath)}";
                failures.Add(new { file = filePath, reason = s });
                return false;
            }

            var tagsContent = await fs.GetEng30TagsAnswer(filePath);
            var tags = tagsContent.Split(',').Select(s => s.Trim()).ToArray();

            if (tags.Length < 6)
            {
                var s =
                    $"Eng30Tags answer '{PathExtensions.ResolveEng30TagsAnswerPath(filePath)}' must have at least 6 tags.";
                failures.Add(new { file = filePath, reason = s });
            }

            return true;
        }
        catch (Exception e)
        {
            failures.Add(new { file = filePath, reason = $"Fatal error for '{filePath}': {e.Message}" });
            return false;
        }
    }
}