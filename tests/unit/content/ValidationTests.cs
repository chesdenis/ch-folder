
using System.Text.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using shared_csharp;
using shared_csharp.Extensions;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace pa_validate_folder;

public class ValidationTests
{
    // Define the regex for MD5 prefix (32 characters of hexadecimal)
    private readonly Regex _md5PrefixRegex = new Regex(@"^[a-fA-F0-9]{32}$", RegexOptions.Compiled);
    private static readonly string ContextPath = "C:\\PhotoHive";
    
    private static readonly object[][] TestFilePaths =
        File.ReadAllLines(Path.Combine(ContextPath, "spec.info"))
            .Select(line => line
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => (object)s.Trim())
                .ToArray())
            .Where(arr => arr.Length > 0)
            .ToArray();
    private static string[] GetPreviewKinds()
    {
        return new[] { "16", "32", "64", "128", "512", "2000" };
    }

    [Fact]
    public async Task ValidateFolder()
    {
        Assert.NotEmpty(TestFilePaths);

        foreach (var p in TestFilePaths)
        {
            Assert.NotEmpty(p);
        }
        
    }

    [Theory]
    [MemberData(nameof(GetFilesInFolder))]
    public async Task ValidateEmbeddingsResult(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var dqFolder = Path.Combine(directoryName, "dq");

        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        Assert.True(File.Exists(Path.Combine(dqFolder, groupName + ".dq.emb.json")),
            $"Embeddings file '{Path.Combine(dqFolder, groupName + ".dq.emb.json")}' does not exist.");

        var embeddingPath = Path.Combine(dqFolder, $"{groupName}.dq.emb.json");

        var embeddingContent = await File.ReadAllTextAsync(embeddingPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var item = JsonSerializer.Deserialize<EmbeddingFile>(embeddingContent, options);
        if (item?.data == null || item.data.Count == 0)
        {
            Assert.Fail($"No embeddings found for {filePath}.");
        }

        if (item?.data[0].embedding == null || item.data[0].embedding.Count == 0)
        {
            Assert.Fail($"No embeddings data found for {filePath}.");
        }
    }

    [Theory]
    [MemberData(nameof(GetFilesInFolder))]
    public void ValidateDescriptionQueries(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var dqFolder = Path.Combine(directoryName, "dq");

        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        Assert.True(File.Exists(Path.Combine(dqFolder, groupName + ".dq.md")),
            $"Query file '{Path.Combine(dqFolder, groupName + ".dq.md")}' does not exist.");
    }

    [Theory]
    [MemberData(nameof(GetFilesInFolder))]
    public void ValidateEnglish10WordsQueries(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var dqFolder = Path.Combine(directoryName, "engShort");

        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        Assert.True(File.Exists(Path.Combine(dqFolder, groupName + ".engShort.md")),
            $"Query file '{Path.Combine(dqFolder, groupName + ".engShort.md")}' does not exist.");
    }

    [Theory]
    [MemberData(nameof(GetFilesInFolder))]
    public void ValidateTagsQueries(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var dqFolder = Path.Combine(directoryName, "eng30tags");

        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        Assert.True(File.Exists(Path.Combine(dqFolder, groupName + ".eng30tags.md")),
            $"Query file '{Path.Combine(dqFolder, groupName + ".eng30tags.md")}' does not exist.");
    }

    [Theory]
    [MemberData(nameof(GetFilesInFolder))]
    public void ValidateCommerceMarkQueries(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var dqFolder = Path.Combine(directoryName, "commerceMark");

        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        Assert.True(File.Exists(Path.Combine(dqFolder, groupName + ".commerceMark.md")),
            $"Query file '{Path.Combine(dqFolder, groupName + ".commerceMark.md")}' does not exist.");
    }

    [Theory]
    [MemberData(nameof(GetFilesInFolder))]
    public void ValidateDescriptionAnswers(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var dqFolder = Path.Combine(directoryName, "dq");

        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        Assert.True(File.Exists(Path.Combine(dqFolder, groupName + ".dq.md.conversation.md")),
            $"Conversation file '{Path.Combine(dqFolder, groupName + ".dq.md.conversation.md")}' does not exist.");
        Assert.True(File.Exists(Path.Combine(dqFolder, groupName + ".dq.md.answer.md")),
            $"Answer file '{Path.Combine(dqFolder, groupName + ".dq.md.answer.md")}' does not exist.");
    }

    [Theory]
    [MemberData(nameof(GetFilesInFolder))]
    public void ValidateEnglish10WordsAnswers(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var dqFolder = Path.Combine(directoryName, "engShort");

        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        Assert.True(File.Exists(Path.Combine(dqFolder, groupName + ".engShort.md.conversation.md")),
            $"Conversation file '{Path.Combine(dqFolder, groupName + ".engShort.md.conversation.md")}' does not exist.");
        Assert.True(File.Exists(Path.Combine(dqFolder, groupName + ".engShort.md.answer.md")),
            $"Answer file '{Path.Combine(dqFolder, groupName + ".engShort.md.answer.md")}' does not exist.");
    }

    [Theory]
    [MemberData(nameof(GetFilesInFolder))]
    public void ValidateTagsAnswers(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var dqFolder = Path.Combine(directoryName, "eng30tags");

        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        Assert.True(File.Exists(Path.Combine(dqFolder, groupName + ".eng30tags.md.conversation.md")),
            $"Conversation file '{Path.Combine(dqFolder, groupName + ".eng30tags.md.conversation.md")}' does not exist.");
        Assert.True(File.Exists(Path.Combine(dqFolder, groupName + ".eng30tags.md.answer.md")),
            $"Answer file '{Path.Combine(dqFolder, groupName + ".eng30tags.md.answer.md")}' does not exist.");
    }

    [Theory]
    [MemberData(nameof(GetFilesInFolder))]
    public void ValidateCommerceAnswers(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var dqFolder = Path.Combine(directoryName, "commerceMark");

        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        Assert.True(File.Exists(Path.Combine(dqFolder, groupName + ".commerceMark.md.conversation.md")),
            $"Conversation file '{Path.Combine(dqFolder, groupName + ".commerceMark.md.conversation.md")}' does not exist.");
        Assert.True(File.Exists(Path.Combine(dqFolder, groupName + ".commerceMark.md.answer.md")),
            $"Answer file '{Path.Combine(dqFolder, groupName + ".commerceMark.md.answer.md")}' does not exist.");
    }

    [Theory]
    [MemberData(nameof(GetFilesInFolder))]
    public async Task CheckCommerceJsonInAnswers(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var dqFolder = Path.Combine(directoryName, "commerceMark");

        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        var answerFile = Path.Combine(dqFolder, groupName + ".commerceMark.md.answer.md");

        Assert.True(File.Exists(answerFile),
            $"Answer file '{answerFile}' does not exist.");

        var fileContent = await File.ReadAllTextAsync(answerFile);

        var data = JsonConvert.DeserializeObject<CommerceJson>(fileContent);

        Assert.True(data != null, $"CommerceJson is null in '{answerFile}'");
        Assert.True(data.Rate != null, $"Rate is null in '{answerFile}'");
        Assert.True(data.RateExplanation != null, $"RateExplanation is null in '{answerFile}'");
    }

    /*
     * {
         "rate" : 2,
         "rate-explanation" : "Good resolution and an appealing concept (colorful overhead decorations, cobblestone street, copy space), but the scene includes identifiable storefront signage and readable license plates, which limits commercial usability without retouching or releases. Backlit sun creates harsh contrast and deep shadows, and the road sign at left is a distracting element. Composition lacks a clear hero subject, making it less targeted for advertisers. Stronger commercial potential if plates/signage are removed, lighting is softer, and the frame is cleaned or reframed to focus on the decorations or a specific concept."
       }
     */
    public record CommerceJson(
        [property: JsonProperty("rate")] int Rate,
        [property: JsonProperty("rate-explanation")]
        string RateExplanation
    );

    [Theory]
    [MemberData(nameof(GetFilesInFolder))]
    public async Task CheckThatEachAnswerMustHaveMultipleSentences(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var dqFolder = Path.Combine(directoryName, "dq");

        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        var answerPath = Path.Combine(dqFolder, groupName + ".dq.md.answer.md");

        Assert.True(File.Exists(answerPath),
            $"Answer file '{Path.Combine(dqFolder, groupName + ".dq.md.answer.md")}' does not exist.");

        var answerText = await File.ReadAllTextAsync(answerPath);
        var sentences = answerText.Split(".");
        Assert.True(sentences.Length > 2, $"Answer file '{answerPath}' must have multiple sentences.");
    }

    [Theory]
    [MemberData(nameof(GetFilesInFolder))]
    public void ValidateEmbeddings(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var dqFolder = Path.Combine(directoryName, "dq");

        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        Assert.True(File.Exists(Path.Combine(dqFolder, groupName + ".dq.emb.json")),
            $"Embeddings file '{Path.Combine(dqFolder, groupName + ".dq.emb.json")}' does not exist.");
    }

    [Theory]
    [MemberData(nameof(GetFileAndPreviewCombinations))]
    public void ValidatePreviews(string filePath, string previewKind)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var previewFolder = Path.Combine(directoryName, "preview");

        var previewFileName = Path.GetFileNameWithoutExtension(filePath) + "_p" + previewKind + ".jpg";
        var previewPath = Path.Combine(previewFolder, previewFileName);

        Assert.True(File.Exists(previewPath), $"Preview file '{previewPath}' does not exist.");
    }

    [Theory]
    [MemberData(nameof(GetFilesInFolder))]
    public void ValidateFileNamesConvention(string filePath)
    {
        var fileIdParts = Path.GetFileNameWithoutExtension(filePath).Split("_");
        if (fileIdParts.Length == 4)
        {
            return;
        }

        if (fileIdParts.Length == 3)
        {
            return;
        }

        if (fileIdParts.Length == 1)
        {
            Assert.True(_md5PrefixRegex.IsMatch(fileIdParts[0]),
                $"File '{filePath}' does not contain md5 hash marker.");
            return;
        }

        Assert.Fail($"Wrong naming convention for file '{filePath}'");
    }

    [Theory]
    [MemberData(nameof(GetFilesInFolder))]
    public void ValidateFileNameHasMd5Prefix(string filePath)
    {
        // Assert that the file has an MD5 hash prefix
        var fileIdParts = Path.GetFileNameWithoutExtension(filePath).Split("_");
        if (fileIdParts.Length == 4 && fileIdParts[0].Length == 4)
        {
            // we assume that fileIdParts[0] is a group ID, 4 characters long
            // then we assume that fileIdParts[3] is md5 hash
            Assert.True(_md5PrefixRegex.IsMatch(fileIdParts[3]),
                $"File '{filePath}' does not contain md5 hash marker.");
            return;
        }

        if (fileIdParts.Length == 3)
        {
            // we assume that fileIdParts[0..1] are preview hashes
            // then we assume that fileIdParts[2] is md5 hash
            Assert.True(_md5PrefixRegex.IsMatch(fileIdParts[2]),
                $"File '{filePath}' does not contain md5 hash marker.");
            return;
        }

        if (fileIdParts.Length == 1)
        {
            Assert.True(_md5PrefixRegex.IsMatch(fileIdParts[0]),
                $"File '{filePath}' does not contain md5 hash marker.");
            return;
        }

        Assert.Fail($"Wrong naming convention for file '{filePath}'");
    }

    [Theory]
    [MemberData(nameof(GetFilesInFolder))]
    public async Task ValidateFileHasCorrectMd5Prefix(string filePath)
    {
        // Assert that the file has an MD5 hash prefix
        var fileIdParts = Path.GetFileNameWithoutExtension(filePath).Split("_");
        string md5Hash = string.Empty;

        if (fileIdParts.Length == 4 && fileIdParts[0].Length == 4)
        {
            // we assume that fileIdParts[0] is a group ID, 4 characters long
            // then we assume that fileIdParts[3] is md5 hash
            md5Hash = fileIdParts[3];
        }

        if (fileIdParts.Length == 3)
        {
            // we assume that fileIdParts[0..1] are preview hashes
            // then we assume that fileIdParts[2] is md5 hash
            md5Hash = fileIdParts[2];
        }

        if (fileIdParts.Length == 1)
        {
            md5Hash = fileIdParts[0];
        }

        string actualHash = await filePath.CalculateMd5Async();

        Assert.True(md5Hash.Equals(actualHash, StringComparison.InvariantCultureIgnoreCase),
            $"MD5 hash is incorrect: {filePath}");
    }

    public static IEnumerable<object[]> GetFileAndPreviewCombinations()
    {
        var allFilesInFolder = GetFilesInFolder().ToArray();
        foreach (var fileData in allFilesInFolder)
        {
            foreach (var kind in GetPreviewKinds())
            {
                yield return new object[] { fileData[0], kind };
            }
        }
    }

    public static IEnumerable<object[]> GetFilesInFolder()
    {
        foreach (var inputArgs in TestFilePaths)
        foreach (string arg in inputArgs)
        {
            var folderPath = Path.Combine(ContextPath, arg);
            // Ensure the folder exists
            if (!Directory.Exists(folderPath))
                yield break;

            // Get all files in the folder and add their names to the data
            // use top directory only because other folders are system, preview, etc.
            var files = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly).Select(s => new
            {
                fileName = Path.GetFileName(s),
                filePath = s
            }).ToArray();
            // excluding preview and system files and unsupported file types
            files = files.Where(f => !f.fileName.EndsWith(".DS_Store")).ToArray();
            files = files.Where(f => !f.fileName.EndsWith(".mov")).ToArray();
            files = files.Where(f => !f.fileName.EndsWith(".MOV")).ToArray();
            files = files.Where(f => !f.fileName.EndsWith(".mp4")).ToArray();
            files = files.Where(f => !f.fileName.EndsWith(".MP4")).ToArray();
            files = files.Where(f => !f.fileName.StartsWith("._")).ToArray();
            foreach (var file in files)
            {
                yield return new object[] { file.filePath };
            }
        }
    }
}