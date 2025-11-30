using Moq;
using OpenIPC_Config.Services;
using Serilog;

namespace OpenIPC_Config.Tests.Services;

[TestFixture]
public class YamlConfigServiceTests
{
    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger>();
        
        // Mocking the ForContext method of the logger to return the mock logger itself
        _mockLogger.Setup(x => x.ForContext(It.IsAny<Type>())).Returns(_mockLogger.Object);
        
        _yamlConfigService = new YamlConfigService(_mockLogger.Object);
    }

    private Mock<ILogger> _mockLogger;
    private IYamlConfigService _yamlConfigService;

    [Test]
    public void ParseYaml_ValidContent_ParsesSuccessfully()
    {
        // Arrange
        var yamlContent = "video_size: 1920x1080\nvideo_fps: 30";
        var yamlConfig = new Dictionary<string, string>();

        // Act
        _yamlConfigService.ParseYaml(yamlContent, yamlConfig);

        // Assert
        Assert.That(yamlConfig["video_size"], Is.EqualTo("1920x1080"));
        Assert.That(yamlConfig["video_fps"], Is.EqualTo("30"));
    }

    [Test]
    public void UpdateYaml_ValidConfig_GeneratesYamlContent()
    {
        // Arrange
        var yamlConfig = new Dictionary<string, string>
        {
            { "video_size", "1920x1080" },
            { "video_fps", "30" }
        };

        // Act
        var result = _yamlConfigService.UpdateYaml(yamlConfig);

        // Assert
        Assert.That(result, Does.Contain("video_size: 1920x1080"));
        Assert.That(result, Does.Contain("video_fps: 30"));
    }
}