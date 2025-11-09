using System.Collections.ObjectModel;
using Moq;
using Companion.Services;
using Companion.ViewModels;
using Serilog;

namespace OpenIPC.Companion.Tests.ViewModels;

[TestFixture]
public class FirmwareTabViewModelTests
{
    
    
    private FirmwareTabViewModel _viewModel;
    private Mock<ILogger> _mockLogger;
    
    private Mock<ISshClientService> _mockSshClientService;
    private Mock<IEventSubscriptionService> _mockEventSubscriptionService;
    private Mock<IGitHubService> _mockGithubService;
    private Mock<IMessageBoxService> _mockMessageBoxService;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
        _mockLogger.Setup(x => x.ForContext(It.IsAny<Type>())).Returns(_mockLogger.Object);

        _mockSshClientService = new Mock<ISshClientService>();
        _mockEventSubscriptionService = new Mock<IEventSubscriptionService>();
        _mockGithubService = new Mock<IGitHubService>();
        _mockMessageBoxService = new Mock<IMessageBoxService>();
        
        _viewModel = new FirmwareTabViewModel(
            _mockLogger.Object,
            _mockSshClientService.Object,
            _mockEventSubscriptionService.Object,
            _mockGithubService.Object,
            _mockMessageBoxService.Object);
    }


    [Test]
    public void LoadDevices_ValidManufacturer_PopulatesDevices()
    {
        // Arrange
        _viewModel.GetType()
            .GetField("_firmwareData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(_viewModel, new FirmwareData
            {
                Manufacturers = new ObservableCollection<Manufacturer>
                {
                    new Manufacturer
                    {
                        Name = "TestManufacturer",
                        Devices = new ObservableCollection<Device>
                        {
                            new Device { FriendlyName = "TestDevice" }
                        }
                    }
                }
            });

        // Act
        _viewModel.LoadDevices("TestManufacturer");

        // Assert
        Assert.That(_viewModel.Devices, Is.Not.Empty);
        Assert.That(_viewModel.Devices, Does.Contain("TestDevice"));
    }

    [Test]
    public void LoadFirmwares_ValidDevice_PopulatesFirmwares()
    {
        // Arrange
        _viewModel.GetType()
            .GetField("_firmwareData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(_viewModel, new FirmwareData
            {
                Manufacturers = new ObservableCollection<Manufacturer>
                {
                    new Manufacturer
                    {
                        Name = "TestManufacturer",
                        Devices = new ObservableCollection<Device>
                        {
                            new Device
                            {
                                Name = "TestDevice",
                                FriendlyName = "fpv-sensor-nand",
                                FirmwarePackages = new ObservableCollection<FirmwarePackage> { new FirmwarePackage()
                                {
                                    FriendlyName = "fpv-sensor-nand",
                                    Name = "fpv-sensor-nand" 
                                } }
                            }
                        }
                    }
                }
            });

        _viewModel.SelectedManufacturer = "TestManufacturer";

        // Act
        _viewModel.LoadFirmwares("TestDevice");

        // Assert
        Assert.That(_viewModel.Firmwares, Is.Not.Empty);
        Assert.That(_viewModel.Firmwares, Does.Contain("fpv-sensor-nand"));
    }

    
    

    [Test]
    public void CanExecuteDownloadFirmware_ReturnsFalse_IfInvalidState()
    {
        // Arrange
        _viewModel.ManualLocalFirmwarePackageFile = null;
        _viewModel.SelectedManufacturer = null;
        _viewModel.SelectedDevice = null;
        _viewModel.SelectedFirmware = null;

        // Act
        var canExecute = _viewModel.DownloadFirmwareAsyncCommand.CanExecute(null);

        // Assert
        Assert.IsFalse(canExecute);
    }

    [Test]
    public void CanExecuteDownloadFirmware_ReturnsTrue_IfValidState()
    {
        // Arrange
        _viewModel.ManualLocalFirmwarePackageFile = "test.tgz";

        // Act
        var canExecute = _viewModel.DownloadFirmwareAsyncCommand.CanExecute(null);

        // Assert
        Assert.IsTrue(canExecute);
    }
}