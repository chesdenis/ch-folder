using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using shared_csharp;
using shared_csharp.Extensions;
using Xunit.Abstractions;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace pa_validate_folder;

public class ValidationTests(ITestOutputHelper testOutputHelper)
{
    // Define the regex for MD5 prefix (32 characters of hexadecimal)
    private readonly Regex _md5PrefixRegex = new Regex(@"^[a-fA-F0-9]{32}$", RegexOptions.Compiled);

    private static readonly string ContextPath = "/Volumes/AnnaB/PhotoHive-merge";
    // private static readonly string ContextPath = "/Volumes/AnnaB/PhotoHive";

    public static readonly object[][] TestFilePaths = GetStorageFoldersForTests(ContextPath);

    private static string[] GetPreviewKinds() => ["16", "32", "64", "128", "512", "2000"];

    [Fact]
    public async Task PrintFsStatistic()
    {
        testOutputHelper.WriteLine(ContextPath);
        if (!GetTestingFiles().Any())
        {
            Assert.Fail("No files found in the testing folder.");
        }

        var sb = new StringBuilder();
        foreach (var storageFoldersForTest in GetStorageFoldersForTests(ContextPath)
                     .OrderByDescending(x => x[0] as string))
        {
            var storageFolder = storageFoldersForTest[0] as string;

            long totalSize = 0;
            long totalFiles = 0;
            foreach (var file in PathExtensions.GetFilesInFolder(ContextPath, [storageFolder]))
            {
                var fileSize = new FileInfo(file).Length;
                totalSize += fileSize;
                totalFiles++;
                sb.AppendLine(fileSize.ToString());
            }
            testOutputHelper.WriteLine(storageFolder);
            testOutputHelper.WriteLine($"Total size: {totalSize} bytes");
            testOutputHelper.WriteLine($"Total files: {totalFiles}");
        }

        var totalString = sb.ToString();
        testOutputHelper.WriteLine(totalString.AsSha256());
    }

    [Fact]
    public async Task AllFilesAreUnique()
    {
        var ht = new Dictionary<string, string>();
        var duplicates = new List<string>();

        if (!GetTestingFiles().Any())
        {
            Assert.Fail("No files found in the testing folder.");
        }

        foreach (var filePath in GetTestingFiles())
        {
            var md5 = await (filePath[0] as string).CalculateMd5Async();
            if (ht.ContainsKey(md5))
            {
                duplicates.Add(filePath[0] as string);
            }
            else
            {
                ht.Add(md5, filePath[0] as string);
            }
        }

        if (duplicates.Any())
        {
            foreach (var duplicate in duplicates)
            {
                testOutputHelper.WriteLine(duplicate);
            }

            foreach (var duplicate in duplicates)
            {
                File.Delete(duplicate);
            }

            Assert.Fail($"Duplicate file found");
        }
    }

    [Theory]
    [MemberData(nameof(TestFilePaths))]
    public void ValidateFolder(string folderPath) => Assert.NotEmpty(folderPath);

    [Theory]
    [MemberData(nameof(GetTestingFiles))]
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
    [MemberData(nameof(GetTestingFiles))]
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
    [MemberData(nameof(GetTestingFiles))]
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
    [MemberData(nameof(GetTestingFiles))]
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
    [MemberData(nameof(GetTestingFiles))]
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
    [MemberData(nameof(GetTestingFiles))]
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
    [MemberData(nameof(GetTestingFiles))]
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
    [MemberData(nameof(GetTestingFiles))]
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
    [MemberData(nameof(GetTestingFiles))]
    public async Task ValidateAndFixTagsAnswersStructure(string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var dqFolder = Path.Combine(directoryName, "eng30tags");

        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }

        var eng30TagsFilePath = Path.Combine(dqFolder, groupName + ".eng30tags.md.answer.md");

        Assert.True(File.Exists(eng30TagsFilePath),
            $"Answer file '{eng30TagsFilePath}' does not exist.");

        var content = await File.ReadAllTextAsync(eng30TagsFilePath);
        var tags = content.Split(',').Select(s => s.Trim()).ToArray();

        // attempting to fix when tags splitted by dash
        if (tags.Length == 1)
        {
            var fixedTags = tags[0].Split('-').Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

            await File.WriteAllTextAsync(eng30TagsFilePath, string.Join(",", fixedTags));

            // re read again
            content = await File.ReadAllTextAsync(eng30TagsFilePath);
            tags = content.Split(',').Select(s => s.Trim()).ToArray();

            // attempting to fix another way
            if (tags.Length == 1)
            {
                fixedTags = Regex.Replace(content, "\\d+. ", string.Empty).Split('\n')
                    .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

                if (fixedTags.Length > 1)
                {
                    await File.WriteAllTextAsync(eng30TagsFilePath, string.Join(",", fixedTags));

                    // re read again
                    content = await File.ReadAllTextAsync(eng30TagsFilePath);
                    tags = content.Split(',').Select(s => s.Trim()).ToArray();
                }
            }
        }

        if (tags.Length == 1)
        {
            testOutputHelper.WriteLine(eng30TagsFilePath);
        }

        Assert.True(tags.Length > 5);
    }

    [Theory]
    [MemberData(nameof(GetTestingFiles))]
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
    [MemberData(nameof(GetTestingFiles))]
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
         "rate-explanation" : "Good resolution... ."
       }
     */
    public record CommerceJson(
        [property: JsonProperty("rate")] int Rate,
        [property: JsonProperty("rate-explanation")]
        string RateExplanation
    );

    [Theory]
    [MemberData(nameof(GetTestingFiles))]
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
    [MemberData(nameof(GetTestingFiles))]
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
    [MemberData(nameof(GetTestingFiles))]
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
    [MemberData(nameof(GetTestingFiles))]
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
    [MemberData(nameof(GetTestingFiles))]
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
        var allFilesInFolder = GetTestingFiles().ToArray();
        foreach (var fileData in allFilesInFolder)
        {
            foreach (var kind in GetPreviewKinds())
            {
                yield return [fileData[0], kind];
            }
        }
    }

    public static IEnumerable<object[]> GetTestingFiles()
        => GetFilesInFolderForTests(ContextPath, TestFilePaths);

    private static object[][] GetStorageFoldersForTests(string contextPath)
    {
        if (!Directory.Exists(contextPath))
            return Array.Empty<object[]>();
        return PathExtensions.GetStorageFolders(ContextPath)
            .Select(storageFolder => (object[])[storageFolder])
            .ToArray();
    }

    private static IEnumerable<object[]> GetFilesInFolderForTests(string contextPath, object[][] storageFolders,
        Action<string> onFolderProcessed = null)
    {
        foreach (var inputArgs in storageFolders)
        foreach (string arg in inputArgs)
        {
            foreach (var file in PathExtensions.GetFilesInFolder(contextPath, [arg]))
            {
                yield return [file];
            }

            onFolderProcessed?.Invoke(arg);
        }
    }
}