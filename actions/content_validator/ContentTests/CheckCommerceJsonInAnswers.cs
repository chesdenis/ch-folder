using Newtonsoft.Json;
using shared_csharp;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace content_validator.ContentTests;

internal sealed class CheckCommerceJsonInAnswers(IFileSystem fs) : ContentValidationTest(fs)
{
    public override string Key => "CM_JSON";

    protected override async Task<bool> Validate(Func<dynamic, Task> log, string filePath, List<object> failures)
    {
        var path = PathExtensions.ResolveCommerceMarkAnswerPath(filePath);
        var pathExist = fs.FileExists(path);
        if (!pathExist)
        {
            var s = $"Commerce mark file does not exist: {Path.GetFileName(filePath)}";
            failures.Add(new { file = filePath, reason = s });
            return false;
        }

        var commerceJson = await fs.GetCommerceMarkAnswer(filePath);

        try
        {
            var fileContent = await File.ReadAllTextAsync(commerceJson);
            var data = JsonConvert.DeserializeObject<CommerceJson>(fileContent);

            if (data == null || data.Rate == null || data.RateExplanation == null)
            {
                var s = $"CommerceJson is null in: {Path.GetFileName(path)}";
                failures.Add(new { file = filePath, reason = s });
                return false;
            }
        }
        catch (Exception e)
        {
            failures.Add(new { file = filePath, reason = $"Fatal error for '{filePath}': {e.Message}" });
            return false;
        }
        
        return true;
    }
}