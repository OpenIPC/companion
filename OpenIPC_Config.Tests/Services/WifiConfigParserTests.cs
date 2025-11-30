using Moq;
using OpenIPC_Config.Services;
using Serilog;

namespace OpenIPC_Config.Tests.Services;

[TestFixture]
public class WifiConfigParserTests
{
    [SetUp]
    public void SetUp()
    {
        // Mock the logger
        _mockLogger = new Mock<ILogger>();
        Log.Logger = _mockLogger.Object;

        // Initialize WifiConfigParser
        _wifiConfigParser = new WifiConfigParser();
    }

    private Mock<ILogger> _mockLogger;
    private WifiConfigParser _wifiConfigParser;

    [Test]
    public void ParseConfigString_ValidConfig_SetsProperties()
    {
        // Arrange
        var configContent = """
                                wifi_channel = 6
                                wifi_region = 'US'
                            
                                [gs_mavlink]
                                peer = '192.168.0.2'
                            
                                [gs_video]
                                peer = '192.168.0.3'
                            """;

        // Act
        _wifiConfigParser.ParseConfigString(configContent);

        // Assert
        Assert.That(_wifiConfigParser.WifiChannel, Is.EqualTo(6));
        Assert.That(_wifiConfigParser.WifiRegion, Is.EqualTo("US"));
        Assert.That(_wifiConfigParser.GsMavlinkPeer, Is.EqualTo("192.168.0.2"));
        Assert.That(_wifiConfigParser.GsVideoPeer, Is.EqualTo("192.168.0.3"));
    }

    [Test]
    public void GetUpdatedConfigString_ValidUpdates_ReturnsUpdatedConfig()
    {
        // Arrange
        var configContent = """
                                wifi_channel = 6
                                wifi_region = 'US'
                            
                                [gs_mavlink]
                                peer = '192.168.0.2'
                            
                                [gs_video]
                                peer = '192.168.0.3'
                            """;
        _wifiConfigParser.ParseConfigString(configContent);

        // Update properties
        _wifiConfigParser.WifiChannel = 11;
        _wifiConfigParser.WifiRegion = "EU";
        _wifiConfigParser.GsMavlinkPeer = "192.168.1.1";
        _wifiConfigParser.GsVideoPeer = "192.168.1.2";

        // Act
        var updatedConfig = _wifiConfigParser.GetUpdatedConfigString();

        // Assert
        Assert.That(updatedConfig, Does.Contain("wifi_channel = 11"));
        Assert.That(updatedConfig, Does.Contain("wifi_region = 'EU'"));
        Assert.That(updatedConfig, Does.Contain("peer = '192.168.1.1'"));
        Assert.That(updatedConfig, Does.Contain("peer = '192.168.1.2'"));
    }


    [Test]
    public void ParseConfigString_InvalidLine_IgnoresLine()
    {
        // Arrange
        var configContent = """
                                wifi_channel = 6
                                invalid_line_without_equals
                                wifi_region = 'US'
                            """;

        // Act
        _wifiConfigParser.ParseConfigString(configContent);

        // Assert
        Assert.That(_wifiConfigParser.WifiChannel, Is.EqualTo(6));
        Assert.That(_wifiConfigParser.WifiRegion, Is.EqualTo("US"));
    }
}