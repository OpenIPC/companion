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
        Assert.AreEqual("test_unit", _wfbConfigParser.Unit);
        Assert.AreEqual("wlan0", _wfbConfigParser.Wlan);
        Assert.AreEqual("US", _wfbConfigParser.Region);
        Assert.AreEqual("6", _wfbConfigParser.Channel);
        Assert.AreEqual(30, _wfbConfigParser.TxPower);
        Assert.AreEqual(1, _wfbConfigParser.DriverTxPowerOverride);
        Assert.AreEqual(20, _wfbConfigParser.Bandwidth);
        Assert.AreEqual(1, _wfbConfigParser.Stbc);
        Assert.AreEqual(1, _wfbConfigParser.Ldpc);
        Assert.AreEqual(7, _wfbConfigParser.McsIndex);
        Assert.AreEqual(2, _wfbConfigParser.Stream);
        Assert.AreEqual(12345, _wfbConfigParser.LinkId);
        Assert.AreEqual(14550, _wfbConfigParser.UdpPort);
        Assert.AreEqual(1048576, _wfbConfigParser.RcvBuf);
        Assert.AreEqual("data", _wfbConfigParser.FrameType);
        Assert.AreEqual(10, _wfbConfigParser.FecK);
        Assert.AreEqual(20, _wfbConfigParser.FecN);
        Assert.AreEqual(100, _wfbConfigParser.PoolTimeout);
        Assert.AreEqual("long", _wfbConfigParser.GuardInterval);
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
        Assert.AreEqual("test_unit", _wfbConfigParser.Unit);
        Assert.AreEqual("6", _wfbConfigParser.Channel);
    }
}