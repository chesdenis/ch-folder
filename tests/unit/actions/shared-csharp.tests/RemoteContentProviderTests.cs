using Moq;
using shared_csharp.Abstractions;
using shared_csharp;
using shared_csharp.Infrastructure;
using Xunit;
using System.Net;
using Newtonsoft.Json;
using System.Text;
using Moq.Protected;

namespace shared_csharp_tests;

public class RemoteContentProviderTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHandler;

    public RemoteContentProviderTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHandler = new Mock<HttpMessageHandler>();
        var client = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };
        _mockHttpClientFactory.Setup(f => f.CreateClient("PhysicalContentProvider")).Returns(client);
    }

    [Fact]
    public async Task GetTags_ShouldHandleNullTagsSafely()
    {
        // Arrange
        var md5 = "0013b531bce839419d557766d767f0c4";
        var metadata = new FileMetadata(
            Partition: null, Section: null, Group: null, AverageHash: null, ColorHash: null,
            Description: null, Tags: null, // Tags are null
            Previews: null, EmbAnswer: null, EmbConversation: null, DqQuestion: null, DqAnswer: null,
            DqConversation: null, CommerceMarkQuestion: null, CommerceMarkAnswer: null,
            CommerceMarkConversation: null, Eng30TagsQuestion: null, Eng30TagsAnswer: null,
            Eng30TagsConversation: null, EngShortQuestion: null, EngShortAnswer: null,
            EngShortConversation: null, Ext: null, Md5: md5
        );
        var jsonResponse = JsonConvert.SerializeObject(new[] { metadata });

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        var provider = new RemoteContentProvider(_mockHttpClientFactory.Object, "http://localhost");

        // Act
        var result = await provider.GetTags(md5);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetColorHash_ShouldReturnColorHashNotDescription()
    {
        // Arrange
        var md5 = "test_md5";
        var expectedColorHash = "color_hash_value";
        var description = "description_value";
        var metadata = new FileMetadata(
            Partition: null, Section: null, Group: null, AverageHash: null, ColorHash: expectedColorHash,
            Description: description, Tags: null, Previews: null, EmbAnswer: null, EmbConversation: null,
            DqQuestion: null, DqAnswer: null, DqConversation: null, CommerceMarkQuestion: null,
            CommerceMarkAnswer: null, CommerceMarkConversation: null, Eng30TagsQuestion: null,
            Eng30TagsAnswer: null, Eng30TagsConversation: null, EngShortQuestion: null,
            EngShortAnswer: null, EngShortConversation: null, Ext: null, Md5: md5
        );
        var jsonResponse = JsonConvert.SerializeObject(new[] { metadata });

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        var provider = new RemoteContentProvider(_mockHttpClientFactory.Object, "http://localhost");

        // Act
        var result = await provider.GetColorHash(md5);

        // Assert
        Assert.Equal(expectedColorHash, result);
    }
}
