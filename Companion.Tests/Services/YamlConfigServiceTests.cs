using Moq;
using Companion.Services;
using Serilog;

namespace OpenIPC.Companion.Tests.Services;

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
        Assert.AreEqual("1920x1080", yamlConfig["video_size"]);
        Assert.AreEqual("30", yamlConfig["video_fps"]);
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
        Assert.IsTrue(result.Contains("video_size: 1920x1080"));
        Assert.IsTrue(result.Contains("video_fps: 30"));
    }
}