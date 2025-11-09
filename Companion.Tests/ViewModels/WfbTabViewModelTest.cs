using Moq;
using Companion.Events;
using Companion.Models;
using Companion.Services;
using Companion.ViewModels;
using Prism.Events;
using Serilog;
using Xunit;
using Assert = Xunit.Assert;

namespace OpenIPC.Companion.Tests.ViewModels;

public class WfbTabViewModelTest : ViewModelTestBase
{
    public const string DefaultWfbConfContent = @"
        ### unit: drone or gs
        unit=drone

        wlan=wlan0
        region=00
        ### By default used channel number, but, you may set freq instead. For ex: 2387M
        channel=161
        txpower=1
        driver_txpower_override=25
        bandwidth=20
        stbc=1
        ldpc=1
        mcs_index=1
        stream=0
        link_id=7669206
        udp_port=5600
        rcv_buf=456000
        frame_type=data
        fec_k=8
        fec_n=12
        pool_timeout=0
        guard_interval=long
    ";

    //private Mock<ISshClientService> _sshClientServiceMock;
    private Mock<AppMessageEvent> _appMessageEventMock;
    private Mock<IEventAggregator> _eventAggregatorMock;
    private Mock<ILogger> _loggerMock;


    private Mock<WfbConfContentUpdatedEvent> _wfbConfContentUpdatedEventMock;
    private Mock<IYamlConfigService> _mockYamlConfigService;
    private Mock<IGlobalSettingsService> _mockGlobalSettingsService;    


    [Test]
    public void SelectedPower24GHz_PropertyChange_RaisesNotification()
    {
        // Arrange
        var viewModel = new WfbTabViewModel(
            LoggerMock.Object,
            SshClientServiceMock.Object,
            EventSubscriptionServiceMock.Object,
            YamlConfigServiceMock.Object,
            GlobalSettingsServiceMock.Object
        );

        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(WfbTabViewModel.SelectedPower24GHz)) propertyChangedRaised = true;
        };

        // Act
        viewModel.SelectedPower24GHz = 20;

        // Assert
        Assert.True(propertyChangedRaised);
    }

    [Fact]
    public void RestartWfbCommand_ExecutesProperly()
    {
        // Arrange
        var tabMessageEventMock = new Mock<TabMessageEvent>();
        EventAggregatorMock
            .Setup(x => x.GetEvent<TabMessageEvent>())
            .Returns(tabMessageEventMock.Object);

        // Arrange
        var viewModel = new WfbTabViewModel(
            LoggerMock.Object,
            SshClientServiceMock.Object,
            EventSubscriptionServiceMock.Object,
            YamlConfigServiceMock.Object,
            GlobalSettingsServiceMock.Object
        );


        viewModel.WfbConfContent = DefaultWfbConfContent;

        // Act
        viewModel.RestartWfbCommand.Execute(null);

        // Assert
        // SshClientServiceMock.Verify(
        //     x => x.UploadFileStringAsync(It.IsAny<DeviceConfig>(), Companion.Models.OpenIPC.WfbConfFileLoc, It.IsAny<string>()),
        //     Times.Once
        // );

        SshClientServiceMock.Verify(
            x => x.ExecuteCommandAsync(It.IsAny<DeviceConfig>(), DeviceCommands.WfbRestartCommand),
            Times.Once
        );
    }


    [Fact]
    public void WfbConfContent_Setter_ParsesAndUpdatesProperties()
    {
        // Arrange
        var viewModel = new WfbTabViewModel(
            LoggerMock.Object,
            SshClientServiceMock.Object,
            EventSubscriptionServiceMock.Object,
            YamlConfigServiceMock.Object,
            GlobalSettingsServiceMock.Object
        );

        // Act
        viewModel.WfbConfContent = DefaultWfbConfContent; // This should trigger ParseWfbConfContent indirectly.

        // Assert
        Assert.Equal("5805 MHz [161]", viewModel.SelectedFrequency58String);
        Assert.Equal(25, viewModel.SelectedPower);
        Assert.Equal(20, viewModel.SelectedBandwidth);
        Assert.Equal(161, viewModel.SelectedChannel);
        Assert.Equal(1, viewModel.SelectedMcsIndex);
    }

    // TODO: debug why this is failing
    // [Fact]
    // public async Task RestartWfbCommand_UpdatesWfbConfContentCorrectly()
    // {
    //     var tabMessageEventMock = new Mock<TabMessageEvent>();
    //     EventAggregatorMock
    //         .Setup(x => x.GetEvent<TabMessageEvent>())
    //         .Returns(tabMessageEventMock.Object);
    //
    //
    //     // Arrange
    //     var viewModel = new WfbTabViewModel(
    //         LoggerMock.Object,
    //         SshClientServiceMock.Object,
    //         EventSubscriptionServiceMock.Object
    //     );
    //
    //     viewModel.WfbConfContent = DefaultWfbConfContent.Replace("\r\n", "\n"); ;
    //     //viewModel.SelectedFrequency58String = "5180 MHz [36]";
    //     viewModel.SelectedChannel = 36;
    //     //viewModel.SelectedFrequency24String = "2412 MHz [1]";
    //     viewModel.SelectedBandwidth = 40;
    //     viewModel.SelectedPower = 20;
    //     viewModel.SelectedPower24GHz = 15;
    //     viewModel.SelectedMcsIndex = 7;
    //     viewModel.SelectedStbc = 1;
    //     viewModel.SelectedLdpc = 1;
    //     viewModel.SelectedFecK = 4;
    //     viewModel.SelectedFecN = 6;
    //     //viewModel.SelectedChannel = 36;
    //
    //     // Act
    //     viewModel.RestartWfbCommand.Execute(null);
    //
    //     // Assert
    //     Assert.Contains("channel=36", viewModel.WfbConfContent);
    //     // Assert.Contains("txpower=15", viewModel.WfbConfContent);
    //     // Assert.Contains("bandwidth=40", viewModel.WfbConfContent);
    //     // Assert.Contains("mcs_index=7", viewModel.WfbConfContent);
    //     // Assert.Contains("fec_n=6", viewModel.WfbConfContent);
    // }
}