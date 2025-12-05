using Microsoft.Extensions.Caching.Memory;
using Moq;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Moq.Protected;
using Companion.Services;
using Serilog;
using Xunit;
using Assert = NUnit.Framework.Assert;

public class GitHubServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly GitHubService _gitHubService;
    private readonly ILogger _logger;
    

    public GitHubServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
        _gitHubService = new GitHubService(_memoryCache, _httpClient, _logger);
    }

    [Fact]
    public async Task GetGitHubDataAsync_ReturnsDataFromCache_WhenAvailable()
    {
        // Arrange
        string url = "https://api.github.com/repos/example/repo";
        string cachedData = "cached response";
        _memoryCache.Set("GitHubData", cachedData, TimeSpan.FromMinutes(60));

        // Act
        string result = await _gitHubService.GetGitHubDataAsync(url);

        // Assert
        Assert.That(cachedData, Is.EqualTo(result));
    }

    [Fact]
    public async Task GetGitHubDataAsync_FetchesFromGitHub_WhenNotCached()
    {
        // Arrange
        string url = "https://api.github.com/repos/example/repo";
        string apiResponse = "api response";
        // string cachedData = "cached response";
        // _memoryCache.Set("GitHubData", cachedData, TimeSpan.FromMinutes(60));

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(apiResponse)
            });

        // Act
        string result = await _gitHubService.GetGitHubDataAsync(url);

        // Assert
        Assert.That(apiResponse, Is.EqualTo(result));
        _memoryCache.TryGetValue("GitHubData", out string? cachedResult);
        
        // Assert that the cached data matches the API response
        Assert.That(cachedResult, Is.Not.Null);
        Assert.That(apiResponse, Is.EqualTo(cachedResult));
    }

    [Fact]
    public async Task GetGitHubDataAsync_ReturnsNull_OnHttpRequestException()
    {
        // Arrange
        string url = "https://api.github.com/repos/example/repo";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        string result = await _gitHubService.GetGitHubDataAsync(url);

        // Assert
        Assert.Null(result);
    }
}