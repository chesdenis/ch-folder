using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using shared_csharp;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;
using shared_csharp.Infrastructure;
using Xunit.Abstractions;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace pa_validate_folder;

public class ValidationTests(ITestOutputHelper testOutputHelper)
{
    // Define the regex for MD5 prefix (32 characters of hexadecimal)
    private readonly Regex _md5PrefixRegex = new Regex(@"^[a-fA-F0-9]{32}$", RegexOptions.Compiled);

    private readonly IFileSystem _fileSystem = new PhysicalFileSystem();

    // private static readonly string ContextPath = "/Volumes/AnnaR/to-process-2022";
    //private static readonly string ContextPath = "/Volumes/AnnaB/PhotoHive";
    private static readonly string ContextPath = "/Volumes/AnnaR/to-process";

    public static readonly object[][] TestFilePaths = GetStorageFoldersForTests(ContextPath).ToArray();

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

            // foreach (var duplicate in duplicates)
            // {
            //     File.Delete(duplicate);
            // }

            Assert.Fail($"Duplicate file found");
        }
    }

    [Theory]
    [MemberData(nameof(GetTestingFiles))]
    public async Task ValidateEmbeddingsResult(string filePath)
    {
        var embeddingContent = await _fileSystem.GetEmbAnswer(filePath);
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

    // done
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

    // done
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

    // done
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

    // done 
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

    // done
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

    // done
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

    // done
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
    public async Task ValidateTagsAnswersStructure(string filePath)
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

        if (tags.Length == 1)
        {
            testOutputHelper.WriteLine(eng30TagsFilePath);
        }

        Assert.True(tags.Length > 5);
    }

    // done
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

        try
        {
            var fileContent = await File.ReadAllTextAsync(answerFile);
            var data = JsonConvert.DeserializeObject<CommerceJson>(fileContent);

            Assert.True(data != null, $"CommerceJson is null in '{answerFile}'");
            Assert.True(data.Rate != null, $"Rate is null in '{answerFile}'");
            Assert.True(data.RateExplanation != null, $"RateExplanation is null in '{answerFile}'");
        }
        catch (Exception e)
        {
            Assert.Fail($"Cant read {answerFile}");
        }
    }
 
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
    public void ValidateEmbeddingsExistance(string filePath)
    {
        Assert.True(File.Exists(PathExtensions.ResolveEmbAnswer(filePath)),
            $"Embeddings file answer '{PathExtensions.ResolveEmbAnswer(filePath)}' does not exist.");
        
        Assert.True(File.Exists(PathExtensions.ResolveEmbAnswer(filePath)),
            $"Embeddings file conversation '{PathExtensions.ResolveEmbConversation(filePath)}' does not exist.");
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

    // done
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

    // done
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

    [Theory]
    [MemberData(nameof(GetTestingFiles))]
    public async Task QuestionsMustHaveFileKey(string filePath)
    {
        var fileKey = PathExtensions.ResolveFileKey(filePath);

        var commerceMarkQuestion = await _fileSystem.GetCommerceMarkQuestion(filePath);
        var engShortQuestion = await _fileSystem.GetEngShortQuestion(filePath);
        var eng30TagsQuestion = await  _fileSystem.GetEng30TagsQuestion(filePath);
        var dqQuestion = await  _fileSystem.GetDqQuestion(filePath);

        // file key must be inside question and inside conversation. This is because we pass preview for each input. 
        Assert.True(commerceMarkQuestion.Contains(fileKey),
            $"'commerceMarkQuestion {filePath}' does not contain '{fileKey}'");
        Assert.True(engShortQuestion.Contains(fileKey), $"'engShortQuestion {filePath}' does not contain '{fileKey}'");
        Assert.True(eng30TagsQuestion.Contains(fileKey),
            $"'eng30TagsQuestion {filePath}' does not contain '{fileKey}'");
        Assert.True(dqQuestion.Contains(fileKey), $"'dqQuestion {filePath}' does not contain '{fileKey}'");
    }

    [Theory]
    [MemberData(nameof(GetTestingFiles))]
    public async Task ConversationsMustHaveFileKey(string filePath)
    {
        var fileKey = PathExtensions.ResolveFileKey(filePath);

        var commerceMarkConversation = await _fileSystem.GetCommerceMarkConversation(filePath);
        var engShortConversation = await _fileSystem.GetEngShortConversation(filePath);
        var eng30TagsConversation = await _fileSystem.GetEng30TagsConversation(filePath);
        var dqConversation = await _fileSystem.GetDqConversation(filePath);

        // file key must be inside question and inside conversation. This is because we pass preview for each input. 
        Assert.True(commerceMarkConversation.Contains(fileKey),
            $"'commerceMarkConversation {filePath}' does not contain '{fileKey}'");
        Assert.True(engShortConversation.Contains(fileKey),
            $"'engShortConversation {filePath}' does not contain '{fileKey}'");
        Assert.True(eng30TagsConversation.Contains(fileKey),
            $"'eng30TagsConversation {filePath}' does not contain '{fileKey}'");
        Assert.True(dqConversation.Contains(fileKey),
            $"'dqConversation {filePath}' does not contain '{fileKey}'");
    }

    
    //[Theory]
    //[MemberData(nameof(GetTestingFiles))]
    public async Task SyncAnswerWithConversation(string filePath)
    {
        var commerceMarkConversation = await _fileSystem.GetCommerceMarkConversation(filePath);
        var engShortConversation = await _fileSystem.GetEngShortConversation(filePath);
        var eng30TagsConversation = await _fileSystem.GetEng30TagsConversation(filePath);
        var dqConversation = await _fileSystem.GetDqConversation(filePath);
        
        var commerceMarkAnswer = await _fileSystem.GetCommerceMarkAnswer(filePath);
        var engShortAnswer = await _fileSystem.GetEngShortAnswer(filePath);
        var eng30TagsAnswer = await _fileSystem.GetEng30TagsAnswer(filePath);
        var dqAnswer = await _fileSystem.GetDqAnswer(filePath);
    
        await UpdateAnswerWithConversation(commerceMarkConversation, commerceMarkAnswer, PathExtensions.ResolveCommerceMarkAnswerPath(filePath));
        await UpdateAnswerWithConversation(engShortConversation, engShortAnswer, PathExtensions.ResolveEngShortAnswerPath(filePath));
        await UpdateAnswerWithConversation(eng30TagsConversation, eng30TagsAnswer, PathExtensions.ResolveEng30TagsAnswerPath(filePath));
        await UpdateAnswerWithConversation(dqConversation, dqAnswer, PathExtensions.ResolveDqAnswerPath(filePath));
    
        await Task.CompletedTask;
    }

    private async Task UpdateAnswerWithConversation(string conversation, string answer, string fileToPatch)
    {
        var assistantMarker = "### assistant";
        
        var sp = conversation
            .IndexOf(assistantMarker, StringComparison.Ordinal) + assistantMarker.Length;
        var expected = conversation[sp..];
        
        if(!string.Equals(answer.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            await File.WriteAllTextAsync(fileToPatch, expected);
            testOutputHelper.WriteLine($"Updated answer for '{fileToPatch}'");
        }
    }

    [Theory]
    [MemberData(nameof(GetTestingFiles))]
    public async Task EmbeddingAnswersMustBeInsideConversation(string filePath)
    {
        var embeddingAnswer = await _fileSystem.GetEmbAnswer(filePath);
        var embeddingConversation = await _fileSystem.GetEmbConversation(filePath);
        
        var descriptionAnswer = await _fileSystem.GetDqAnswer(filePath);
        
        Assert.True(embeddingConversation.Contains(embeddingAnswer), 
            $"'embeddingAnswer {PathExtensions.ResolveEmbAnswer(filePath)}' does not have in conversation");
        
        Assert.True(embeddingConversation.Contains(descriptionAnswer), 
            $"'descriptionAnswer {PathExtensions.ResolveDqAnswerPath(filePath)}' does not have in conversation");
    } 
    
     
    [Theory]
    [MemberData(nameof(GetTestingFiles))]
    public async Task AnswersMustBeInsideConversation(string filePath)
    {
        var commerceMarkConversation = await _fileSystem.GetCommerceMarkConversation(filePath);
        var engShortConversation = await _fileSystem.GetEngShortConversation(filePath);
        var eng30TagsConversation = await _fileSystem.GetEng30TagsConversation(filePath);
        var dqConversation = await _fileSystem.GetDqConversation(filePath);

        var commerceMarkAnswer = await _fileSystem.GetCommerceMarkAnswer(filePath);
        var engShortAnswer = await _fileSystem.GetEngShortAnswer(filePath);
        var eng30TagsAnswer = await _fileSystem.GetEng30TagsAnswer(filePath);
        var dqAnswer = await _fileSystem.GetDqAnswer(filePath);

        // each answer must be inside conversation:
        Assert.True(commerceMarkConversation.Contains(commerceMarkAnswer), 
            $"'commerceMarkAnswer {PathExtensions.ResolveCommerceMarkAnswerPath(filePath)}' does not have in conversation");
        Assert.True(engShortConversation.Contains(engShortAnswer),
            $"'engShortAnswer {PathExtensions.ResolveEngShortConversationPath(filePath)}' does not exist in conversation");
        Assert.True(eng30TagsConversation.Contains(eng30TagsAnswer),
            $"'eng30TagsAnswer {PathExtensions.ResolveEng30TagsConversationPath(filePath)}' does not exist in conversation");
        Assert.True(dqConversation.Contains(dqAnswer),
            $"'dqAnswer {PathExtensions.ResolveDqConversationPath(filePath)}' does not exist in conversation");
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

    private static IEnumerable<object[]> _testingFiles = new List<object[]>();

    public static IEnumerable<object[]> GetTestingFiles()
    {
        if (!_testingFiles.Any())
        {
            _testingFiles = GetFilesInFolderForTests(ContextPath, GetStorageFoldersForTests(ContextPath))
                .ToArray();
        }

        return _testingFiles;
    }

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