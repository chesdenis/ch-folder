using Moq;
using shared_csharp.Abstractions;
using shared_csharp;
using meta_uploader;
using Xunit;
using shared_csharp.Extensions;

namespace shared_csharp_tests;

public class RegisteredObjectUploaderTests
{
    [Fact]
    public async Task CollectRecords_ShouldCollectAllObjectTypes()
    {
        // Arrange
        var mockContentProvider = new Mock<IContentProvider>();
        var primaryMd5 = "001b3f6e463c1337925d73bcdfcb5e98";
        var previewMd5 = "266567399e67d266e746535d51842888";
        var embAnswer = "emb answer content";
        var dqAnswer = "dq answer content";
        var commerceAnswer = "commerce answer content";
        var engShortAnswer = "eng short answer content";
        var eng30Tags = new[] { "tag1", "tag2" };
        var commerceJson = new CommerceJson(5, "excellent");

        var metadata = new FileMetadata(
            Partition: null, Section: null, Group: null, AverageHash: null, ColorHash: null,
            Description: null, Tags: null, 
            Previews: new Dictionary<string, string> { { "small", previewMd5 } },
            EmbAnswer: null, EmbConversation: null, DqQuestion: null, DqAnswer: null,
            DqConversation: null, CommerceMarkQuestion: null, CommerceMarkAnswer: null,
            CommerceMarkConversation: null, Eng30TagsQuestion: null, Eng30TagsAnswer: null,
            Eng30TagsConversation: null, EngShortQuestion: null, EngShortAnswer: null,
            EngShortConversation: null, Ext: null, Md5: primaryMd5
        );

        mockContentProvider.Setup(c => c.GetMetadataWithPreviews(primaryMd5))
            .ReturnsAsync(metadata);
        mockContentProvider.Setup(c => c.GetEmbAnswer(primaryMd5)).ReturnsAsync(embAnswer);
        mockContentProvider.Setup(c => c.GetDqAnswer(primaryMd5)).ReturnsAsync(dqAnswer);
        mockContentProvider.Setup(c => c.GetCommerceMarkAnswer(primaryMd5)).ReturnsAsync(commerceAnswer);
        mockContentProvider.Setup(c => c.GetEngShortAnswer(primaryMd5)).ReturnsAsync(engShortAnswer);
        mockContentProvider.Setup(c => c.GetEng30Tags(primaryMd5)).ReturnsAsync(eng30Tags);
        mockContentProvider.Setup(c => c.GetCommerceMarkAnswerJson(primaryMd5)).ReturnsAsync(commerceJson);

        // Need to set env vars for constructor
        Environment.SetEnvironmentVariable("PG_HOST", "localhost");
        Environment.SetEnvironmentVariable("PG_PORT", "5432");
        Environment.SetEnvironmentVariable("PG_DATABASE", "test");
        Environment.SetEnvironmentVariable("PG_USERNAME", "test");
        Environment.SetEnvironmentVariable("PG_PASSWORD", "test");

        var uploader = new RegisteredObjectUploader(mockContentProvider.Object);

        // Act
        var records = await uploader.CollectRecords(primaryMd5);

        // Assert
        Assert.Contains(records, r => r.md5_hash == primaryMd5 && r.object_type == "primary");
        Assert.Contains(records, r => r.md5_hash == previewMd5 && r.object_type == "preview");
        Assert.Contains(records, r => r.md5_hash == embAnswer.AsMd5() && r.object_type == "emb_answer");
        Assert.Contains(records, r => r.md5_hash == dqAnswer.AsMd5() && r.object_type == "dq_answer");
        Assert.Contains(records, r => r.md5_hash == commerceAnswer.AsMd5() && r.object_type == "commerce_mark_answer");
        Assert.Contains(records, r => r.md5_hash == engShortAnswer.AsMd5() && r.object_type == "eng_short_answer");
        Assert.Contains(records, r => r.md5_hash == string.Join(", ", eng30Tags).AsMd5() && r.object_type == "eng_30_tags");
        
        var expectedJson = Newtonsoft.Json.JsonConvert.SerializeObject(commerceJson);
        Assert.Contains(records, r => r.md5_hash == expectedJson.AsMd5() && r.object_type == "commerce_mark_answer_json");
    }

    [Fact]
    public async Task CollectRecords_ShouldHashLongPreviewValues()
    {
        // Arrange
        var mockContentProvider = new Mock<IContentProvider>();
        var primaryMd5 = "001b3f6e463c1337925d73bcdfcb5e98";
        // 10KB string to trigger potential index issue if not hashed
        var longPreviewContent = new string('a', 10000); 

        var metadata = new FileMetadata(
            Partition: null, Section: null, Group: null, AverageHash: null, ColorHash: null,
            Description: null, Tags: null, 
            Previews: new Dictionary<string, string> { { "large", longPreviewContent } },
            EmbAnswer: null, EmbConversation: null, DqQuestion: null, DqAnswer: null,
            DqConversation: null, CommerceMarkQuestion: null, CommerceMarkAnswer: null,
            CommerceMarkConversation: null, Eng30TagsQuestion: null, Eng30TagsAnswer: null,
            Eng30TagsConversation: null, EngShortQuestion: null, EngShortAnswer: null,
            EngShortConversation: null, Ext: null, Md5: primaryMd5
        );

        mockContentProvider.Setup(c => c.GetMetadataWithPreviews(primaryMd5))
            .ReturnsAsync(metadata);

        var uploader = new RegisteredObjectUploader(mockContentProvider.Object);

        // Act
        var records = await uploader.CollectRecords(primaryMd5);

        // Assert
        Assert.Contains(records, r => r.md5_hash == primaryMd5 && r.object_type == "primary");
        // We expect the long preview content to be hashed into its MD5
        var expectedHash = longPreviewContent.AsMd5();
        Assert.Contains(records, r => r.md5_hash == expectedHash && r.object_type == "preview");
        Assert.DoesNotContain(records, r => r.md5_hash == longPreviewContent);
    }
}
