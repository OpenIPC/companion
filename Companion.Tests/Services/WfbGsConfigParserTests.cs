using Moq;
using Companion.Services;
using Serilog;
using Serilog.Events;
using Xunit;
using Assert = Xunit.Assert;

namespace OpenIPC_Config.Tests.Services;

public class WfbGsConfigParserTests : IDisposable
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly StringWriter _logOutput;
    private readonly WfbGsConfigParser _parser;

    public WfbGsConfigParserTests()
    {
        _loggerMock = new Mock<ILogger>();
        _logOutput = new StringWriter();
        Log.Logger = new LoggerConfiguration()
            .WriteTo.TextWriter(_logOutput)
            .CreateLogger();
        _parser = new WfbGsConfigParser();
    }

    public void Dispose()
    {
        _logOutput.Dispose();
        Log.CloseAndFlush();
    }

    [Fact]
    public void ParseConfigString_EmptyConfigContent_LogsError()
    {
        // Arrange
        var configContent = string.Empty;

        // Configure the logger to use the mock logger
        Log.Logger = _loggerMock.Object;

        // Act
        _parser.ParseConfigString(configContent);

        // Assert
        _loggerMock.Verify(
            l => l.Write(
                It.Is<LogEventLevel>(level => level == LogEventLevel.Error),
                It.Is<string>(msg => msg.Contains("Config content is empty or null."))),
            Times.Once);
    }

    [Fact]
    public void ParseConfigString_ValidConfigContent_ParsesTxPower()
    {
        // Arrange
        var configContent = "options 88XXau_wfb rtw_tx_pwr_idx_override=25";

        // Act
        _parser.ParseConfigString(configContent);

        // Assert
        Assert.Equal("25", _parser.TxPower);
    }

    [Fact]
    public void ParseConfigString_ConfigContentWithComments_ParsesTxPower()
    {
        // Arrange
        var configContent = "# Comment\noptions 88XXau_wfb rtw_tx_pwr_idx_override=1";

        // Act
        _parser.ParseConfigString(configContent);

        // Assert
        Assert.Equal("1", _parser.TxPower);
    }

    [Fact]
    public void GetUpdatedConfigString_UpdatedTxPower_ReturnsUpdatedConfig()
    {
        // Arrange
        var configContent = "options 88XXau_wfb rtw_tx_pwr_idx_override=1";
        _parser.ParseConfigString(configContent);
        _parser.TxPower = "30";

        // Act
        var updatedConfig = _parser.GetUpdatedConfigString();

        // Assert
        Assert.Contains("rtw_tx_pwr_idx_override=30", updatedConfig);
    }

    [Fact]
    public void GetUpdatedConfigString_PreservesComments_ReturnsUpdatedConfig()
    {
        // Arrange
        var configContent = "# Comment\noptions 88XXau_wfb rtw_tx_pwr_idx_override=1";
        _parser.ParseConfigString(configContent);
        _parser.TxPower = "2";

        // Act
        var updatedConfig = _parser.GetUpdatedConfigString();

        // Assert
        Assert.Contains("# Comment", updatedConfig);
        Assert.Contains("rtw_tx_pwr_idx_override=2", updatedConfig);
    }
}