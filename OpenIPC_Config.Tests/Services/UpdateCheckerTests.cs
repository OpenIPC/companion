using System.Net;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using OpenIPC_Config.Services;

namespace OpenIPC_Config.Tests.Services;

public class UpdateCheckerTests
{
    private const string MockUpdateJson = @"{
        'version': 'release-v1.2.0',
        'release_notes': 'Bug fixes and performance improvements.',
        'download_url': 'https://example.com/download'
    }";

    [Test]
    public async Task CheckForUpdateAsync_ShouldReturnUpdateAvailable_WhenNewVersionExists()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                'version': 'release-v1.2.0',
                'release_notes': 'Bug fixes and performance improvements.',
                'download_url': 'https://example.com/download'
            }")
            });

        var mockHttpClient = new HttpClient(mockHttpMessageHandler.Object);

        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(c => c["UpdateChecker:LatestJsonUrl"]).Returns("https://mock-url/latest.json");

        var updateChecker = new UpdateChecker(mockHttpClient, mockConfiguration.Object);

        // Act
        var result = await updateChecker.CheckForUpdateAsync("v0.0.1");

        // Assert
        Assert.That(result.HasUpdate, Is.True);
        Assert.That(result.ReleaseNotes, Is.EqualTo("Bug fixes and performance improvements."));
        Assert.That(result.DownloadUrl, Is.EqualTo("https://example.com/download"));
        Assert.That(result.NewVersion, Is.EqualTo("release-v1.2.0"));
    }
}