using Moq;
using Companion.Services;
using Serilog;

namespace OpenIPC.Companion.Tests.Services;

[TestFixture]
public class WfbConfigParserTests
{
    [SetUp]
    public void SetUp()
    {
        // Mock the logger
        _mockLogger = new Mock<ILogger>();
        Log.Logger = _mockLogger.Object;

        // Initialize WfbConfigParser
        _wfbConfigParser = new WfbConfigParser();
    }

    private Mock<ILogger> _mockLogger;
    private WfbConfigParser _wfbConfigParser;

    [Test]
    public void ParseConfigString_ValidConfig_SetsProperties()
    {
        // Arrange
        var configContent = """
                                unit = 'test_unit'
                                wlan = 'wlan0'
                                region = 'US'
                                channel = '6'
                                txpower = 30
                                driver_txpower_override = 1
                                bandwidth = 20
                                stbc = 1
                                ldpc = 1
                                mcs_index = 7
                                stream = 2
                                link_id = 12345
                                udp_port = 14550
                                rcv_buf = 1048576
                                frame_type = 'data'
                                fec_k = 10
                                fec_n = 20
                                pool_timeout = 100
                                guard_interval = 'long'
                            """;

        // Act
        _wfbConfigParser.ParseConfigString(configContent);

        // Assert
        Assert.That(_wfbConfigParser.Unit, Is.EqualTo("test_unit"));
        Assert.That(_wfbConfigParser.Wlan, Is.EqualTo("wlan0"));
        Assert.That(_wfbConfigParser.Region, Is.EqualTo("US"));
        Assert.That(_wfbConfigParser.Channel, Is.EqualTo("6"));
        Assert.That(_wfbConfigParser.TxPower, Is.EqualTo(30));
        Assert.That(_wfbConfigParser.DriverTxPowerOverride, Is.EqualTo(1));
        Assert.That(_wfbConfigParser.Bandwidth, Is.EqualTo(20));
        Assert.That(_wfbConfigParser.Stbc, Is.EqualTo(1));
        Assert.That(_wfbConfigParser.Ldpc, Is.EqualTo(1));
        Assert.That(_wfbConfigParser.McsIndex, Is.EqualTo(7));
        Assert.That(_wfbConfigParser.Stream, Is.EqualTo(2));
        Assert.That(_wfbConfigParser.LinkId, Is.EqualTo(12345));
        Assert.That(_wfbConfigParser.UdpPort, Is.EqualTo(14550));
        Assert.That(_wfbConfigParser.RcvBuf, Is.EqualTo(1048576));
        Assert.That(_wfbConfigParser.FrameType, Is.EqualTo("data"));
        Assert.That(_wfbConfigParser.FecK, Is.EqualTo(10));
        Assert.That(_wfbConfigParser.FecN, Is.EqualTo(20));
        Assert.That(_wfbConfigParser.PoolTimeout, Is.EqualTo(100));
        Assert.That(_wfbConfigParser.GuardInterval, Is.EqualTo("long"));
    }

    [Test]
    public void ParseConfigString_InvalidLine_IgnoresLine()
    {
        // Arrange
        var configContent = """
                                unit = 'test_unit'
                                invalid_line_without_equals
                                channel = '6'
                            """;

        // Act
        _wfbConfigParser.ParseConfigString(configContent);

        // Assert
        Assert.That(_wfbConfigParser.Unit, Is.EqualTo("test_unit"));
        Assert.That(_wfbConfigParser.Channel, Is.EqualTo("6"));
    }
}