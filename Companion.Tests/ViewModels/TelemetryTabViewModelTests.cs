using Moq;
using Companion.Events;
using Companion.Models;
using Companion.Services;
using Companion.ViewModels;
using Serilog;

namespace OpenIPC_Config.Tests.ViewModels;

[TestFixture]
public class TelemetryTabViewModelTests
{
    private Mock<ILogger> _mockLogger;
    private Mock<ISshClientService> _mockSshClientService;
    private Mock<IEventSubscriptionService> _mockEventSubscriptionService;
    private TelemetryTabViewModel _viewModel;
    private Mock<IMessageBoxService> _mockMessageBoxService;
    private Mock<IYamlConfigService> _mockYamlConfigService;
    private Mock<IGlobalSettingsService> _mockGlobalSettingsService;

    
    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger>();
        _mockLogger.Setup(x => x.ForContext(It.IsAny<Type>())).Returns(_mockLogger.Object);

        _mockSshClientService = new Mock<ISshClientService>();
        _mockEventSubscriptionService = new Mock<IEventSubscriptionService>();
        _mockMessageBoxService = new Mock<IMessageBoxService>();
        _mockYamlConfigService = new Mock<IYamlConfigService>();
        _mockGlobalSettingsService = new Mock<IGlobalSettingsService>();
        
            
        _viewModel = new TelemetryTabViewModel(
            _mockLogger.Object,
            _mockSshClientService.Object,
            _mockEventSubscriptionService.Object,
            _mockMessageBoxService.Object,
            _mockYamlConfigService.Object,
            _mockGlobalSettingsService.Object
        );

        // Initialize default telemetry content
        _viewModel.TelemetryContent = "serial=/dev/ttyS0\nbaud=9600\nrouter=1\nmcs_index=5\naggregate=4\nchannels=3";
    }



    [Test]
    public void Constructor_InitializesWithDefaults()
    {
        Assert.That(_viewModel.SerialPorts, Is.Not.Null);
        Assert.That(_viewModel.BaudRates, Is.Not.Null);
        Assert.That(_viewModel.McsIndex, Is.Not.Null);
        Assert.That(_viewModel.SerialPorts[0], Is.EqualTo("ttyS0"));
        Assert.That(_viewModel.BaudRates[0], Is.EqualTo("4800"));
        Assert.That(_viewModel.McsIndex[0], Is.EqualTo("0"));
    }

    [Test]
    public void HandleTelemetryContentUpdated_ParsesAndSetsProperties()
    {
        // Arrange
        var telemetryContent = "serial=/dev/ttyS0\nbaud=9600\nrouter=1\nmcs_index=3";
        var message = new TelemetryContentUpdatedMessage(telemetryContent);

        // Act
        _viewModel.HandleTelemetryContentUpdated(message);

        // Assert
        Assert.That(_viewModel.SelectedSerialPort, Is.EqualTo("ttyS0"));
        Assert.That(_viewModel.SelectedBaudRate, Is.EqualTo("9600"));
        Assert.That(_viewModel.SelectedRouter, Is.EqualTo("mavlink-routed"));
        Assert.That(_viewModel.SelectedMcsIndex, Is.EqualTo("3"));
    }

    [Test]
    public void SaveAndRestartTelemetry_UpdatesAndUploadsTelemetryContent()
    {
        // Arrange
        _viewModel.SelectedSerialPort = "/dev/ttyS0";
        _viewModel.SelectedBaudRate = "9600";
        _viewModel.SelectedRouter = "1";
        _viewModel.SelectedMcsIndex = "3";
        _viewModel.SelectedAggregate = "10";
        _viewModel.SelectedRcChannel = "2";

        // Act
        _viewModel.SaveAndRestartTelemetryCommand.Execute(null);

        // Assert
        _mockSshClientService.Verify(
            service => service.UploadFileStringAsync(
                It.IsAny<DeviceConfig>(),
                Companion.Models.OpenIPC.TelemetryConfFileLoc,
                It.Is<string>(content =>
                    content.Contains("serial=/dev/ttyS0") &&
                    content.Contains("baud=9600") &&
                    content.Contains("router=1") &&
                    content.Contains("mcs_index=3") &&
                    content.Contains("aggregate=10") &&
                    content.Contains("channels=2")
                )
            ),
            Times.Once
        );

        _mockSshClientService.Verify(
            service => service.ExecuteCommandAsync(
                It.IsAny<DeviceConfig>(),
                DeviceCommands.RebootCommand
            ),
            Times.Once
        );
    }

    [Test]
    public async Task UploadLatestVtxMenu_ExecutesCommandsAndUploadsFiles()
    {
        // Arrange
        var vtxmenuIni = "vtxmenu.ini";

        _mockSshClientService
            .Setup(service => service.UploadBinaryAsync(It.IsAny<DeviceConfig>(), "/etc", vtxmenuIni))
            .Returns(Task.CompletedTask);

        // Act
        await Task.Run(() => _viewModel.UploadLatestVtxMenuCommand.Execute(null));

        // Assert
        _mockSshClientService.Verify(
            service => service.UploadBinaryAsync(It.IsAny<DeviceConfig>(), "/etc", vtxmenuIni),
            Times.Once
        );
    }

    [Test]
    public void EnableUART0Command_ExecutesUART0OnCommand()
    {
        // Act
        _viewModel.EnableUART0Command.Execute(null);

        // Assert
        _mockSshClientService.Verify(
            service => service.ExecuteCommandAsync(
                It.IsAny<DeviceConfig>(),
                DeviceCommands.UART0OnCommand
            ),
            Times.Once
        );
    }

    [Test]
    public void DisableUART0Command_ExecutesUART0OffCommand()
    {
        // Act
        _viewModel.DisableUART0Command.Execute(null);

        // Assert
        _mockSshClientService.Verify(
            service => service.ExecuteCommandAsync(
                It.IsAny<DeviceConfig>(),
                DeviceCommands.UART0OffCommand
            ),
            Times.Once
        );
    }

    [Test]
    public void AddMavlinkCommand_ExecutesCommands()
    {
        // Act
        _viewModel.AddMavlinkCommand.Execute(null);

        // Assert
        _mockSshClientService.Verify(
            service => service.ExecuteCommandAsync(
                It.IsAny<DeviceConfig>(),
                TelemetryCommands.Extra
            ),
            Times.Once
        );

        _mockSshClientService.Verify(
            service => service.ExecuteCommandAsync(
                It.IsAny<DeviceConfig>(),
                DeviceCommands.RebootCommand
            ),
            Times.Once
        );
    }
}