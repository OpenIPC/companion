using System.Collections.ObjectModel;
using System;
using Moq;
using Companion.Models;
using Companion.Services;
using Companion.ViewModels;
using Serilog;
using System.Reflection;

namespace OpenIPC.Companion.Tests.ViewModels;

[TestFixture]
public class FirmwareTabViewModelTests
{
    
    
    private FirmwareTabViewModel _viewModel;
    private Mock<ILogger> _mockLogger;
    
    private Mock<ISshClientService> _mockSshClientService;
    private Mock<IEventSubscriptionService> _mockEventSubscriptionService;
    private Mock<IGitHubService> _mockGithubService;
    private Mock<IPreferencesService> _mockPreferencesService;
    private Mock<IMessageBoxService> _mockMessageBoxService;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
        _mockLogger.Setup(x => x.ForContext(It.IsAny<Type>())).Returns(_mockLogger.Object);

        _mockSshClientService = new Mock<ISshClientService>();
        _mockEventSubscriptionService = new Mock<IEventSubscriptionService>();
        _mockGithubService = new Mock<IGitHubService>();
        _mockPreferencesService = new Mock<IPreferencesService>();
        _mockMessageBoxService = new Mock<IMessageBoxService>();
        _mockPreferencesService.Setup(x => x.Load()).Returns(new UserPreferences());
        
        _viewModel = new FirmwareTabViewModel(
            _mockLogger.Object,
            _mockSshClientService.Object,
            _mockEventSubscriptionService.Object,
            _mockGithubService.Object,
            _mockPreferencesService.Object,
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
        Assert.That(canExecute, Is.False);
    }

    [Test]
    public void CanExecuteDownloadFirmware_ReturnsTrue_IfValidState()
    {
        // Arrange
        _viewModel.CanConnect = true;
        _viewModel.ManualLocalFirmwarePackageFile = "test.tgz";

        // Act
        var canExecute = _viewModel.DownloadFirmwareAsyncCommand.CanExecute(null);

        // Assert
        Assert.That(canExecute, Is.True);
    }

    [Test]
    public void SelectedFirmwareSource_DefaultsToOpenIpcBuilder()
    {
        Assert.That(_viewModel.SelectedFirmwareSource, Is.EqualTo("OpenIPC Builder"));
    }

    [Test]
    public void BuildFirmwareDownloadUrl_DefaultSource_UsesOpenIpcReleaseUrl()
    {
        var url = (string)InvokePrivateMethod(_viewModel, "BuildFirmwareDownloadUrl", "test-fw.tgz");
        Assert.That(url, Is.EqualTo("https://github.com/OpenIPC/builder/releases/download/latest/test-fw.tgz"));
    }

    [Test]
    public void BuildFirmwareDownloadUrl_GregSource_UsesRawGithubUrl()
    {
        _viewModel.SelectedFirmwareSource = "Greg APFPV";

        var url = (string)InvokePrivateMethod(_viewModel, "BuildFirmwareDownloadUrl", "test-fw.tgz");

        Assert.That(url, Is.EqualTo("https://raw.githubusercontent.com/sickgreg/OpenIPC_sickgregFPV_apfpv/main/test-fw.tgz"));
    }

    [Test]
    public void CanUseDropdowns_GregSource_ReturnsFalse()
    {
        _viewModel.CanConnect = true;
        _viewModel.IsConnected = true;
        _viewModel.SelectedFirmwareSource = "Greg APFPV";

        Assert.That(_viewModel.CanUseDropdowns, Is.False);
    }

    [Test]
    public void UpdateSysupgradeProgressFromLine_KernelWritingPercent_MapsToRange()
    {
        SetPrivateField(_viewModel, "_sysupgradeInProgress", true);
        _viewModel.ProgressValue = 0;

        InvokePrivateMethod(_viewModel, "UpdateSysupgradeProgressFromLine", "Kernel");
        InvokePrivateMethod(_viewModel, "UpdateSysupgradeProgressFromLine", "Writing kb: 100/1000 (10%)");

        var kernelStart = GetPrivateStaticInt(typeof(FirmwareTabViewModel), "KernelFlashStart");
        var kernelEnd = GetPrivateStaticInt(typeof(FirmwareTabViewModel), "KernelFlashEnd");
        var expected = kernelStart + (int)Math.Round((kernelEnd - kernelStart) * 0.1);

        Assert.That(_viewModel.ProgressValue, Is.EqualTo(expected));
    }

    [Test]
    public void UpdateSysupgradeProgressFromLine_RootfsVerifyPercent_MapsToRange()
    {
        SetPrivateField(_viewModel, "_sysupgradeInProgress", true);
        _viewModel.ProgressValue = 0;

        InvokePrivateMethod(_viewModel, "UpdateSysupgradeProgressFromLine", "RootFS");
        InvokePrivateMethod(_viewModel, "UpdateSysupgradeProgressFromLine", "Verifying kb: 7952/7952 (100%)");

        var rootfsEnd = GetPrivateStaticInt(typeof(FirmwareTabViewModel), "RootfsFlashEnd");
        Assert.That(_viewModel.ProgressValue, Is.EqualTo(rootfsEnd));
    }

    [Test]
    public void UpdateSysupgradeProgressFromLine_OverlayErasePercent_MapsToRange()
    {
        SetPrivateField(_viewModel, "_sysupgradeInProgress", true);
        _viewModel.ProgressValue = 0;

        InvokePrivateMethod(_viewModel, "UpdateSysupgradeProgressFromLine", "OverlayFS");
        InvokePrivateMethod(_viewModel, "UpdateSysupgradeProgressFromLine",
            "Erasing 64 Kibyte @ 2b0000 - 50% complete. Cleanmarker written at 2b0000.");

        var overlayStart = GetPrivateStaticInt(typeof(FirmwareTabViewModel), "OverlayEraseStart");
        var overlayEnd = GetPrivateStaticInt(typeof(FirmwareTabViewModel), "OverlayEraseEnd");
        var expected = overlayStart + (int)Math.Round((overlayEnd - overlayStart) * 0.5);

        Assert.That(_viewModel.ProgressValue, Is.EqualTo(expected));
    }

    [Test]
    public void UpdateSysupgradeProgressFromLine_AnsiStripping_RecognizesKernelPhase()
    {
        SetPrivateField(_viewModel, "_sysupgradeInProgress", true);
        _viewModel.ProgressValue = 0;

        InvokePrivateMethod(_viewModel, "UpdateSysupgradeProgressFromLine", "[1;33mKernel[0m");

        var kernelStart = GetPrivateStaticInt(typeof(FirmwareTabViewModel), "KernelFlashStart");
        Assert.That(_viewModel.ProgressValue, Is.EqualTo(kernelStart));
    }

    [Test]
    public void ParseMtdIndices_ReturnsAllPartitionIndices()
    {
        const string mtdOutput = """
                                 dev:    size   erasesize  name
                                 mtd0: 00040000 00010000 "boot"
                                 mtd1: 00010000 00010000 "env"
                                 mtd2: 00200000 00010000 "kernel"
                                 mtd3: 00800000 00010000 "rootfs"
                                 mtd4: 005b0000 00010000 "rootfs_data"
                                 """;

        var result = (List<int>)InvokePrivateStaticMethod(typeof(FirmwareTabViewModel), "ParseMtdIndices", mtdOutput);

        Assert.That(result, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
    }

    [Test]
    public void ParseMtdIndices_IgnoresInvalidLines()
    {
        const string mtdOutput = """
                                 dev:    size   erasesize  name
                                 nonsense
                                 mtd0: 00040000 00010000 "boot"
                                 other text
                                 mtd2: 00200000 00010000 "kernel"
                                 """;

        var result = (List<int>)InvokePrivateStaticMethod(typeof(FirmwareTabViewModel), "ParseMtdIndices", mtdOutput);

        Assert.That(result, Is.EqualTo(new[] { 0, 2 }));
    }

    [Test]
    public void CanBackupFirmware_ReturnsTrue_WhenConnectedAndIdle()
    {
        _viewModel.IsConnected = true;
        _viewModel.BootloaderInProgress = false;
        _viewModel.FirmwareUpgradeInProgress = false;
        _viewModel.BackupInProgress = false;

        Assert.That(_viewModel.CanBackupFirmware, Is.True);
        Assert.That(_viewModel.BackupFirmwareAsyncCommand.CanExecute(null), Is.True);
    }

    [Test]
    public void CanBackupFirmware_ReturnsFalse_WhenBackupAlreadyRunning()
    {
        _viewModel.IsConnected = true;
        _viewModel.BackupInProgress = true;

        Assert.That(_viewModel.CanBackupFirmware, Is.False);
        Assert.That(_viewModel.BackupFirmwareAsyncCommand.CanExecute(null), Is.False);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            throw new InvalidOperationException($"Field '{fieldName}' not found.");
        field.SetValue(target, value);
    }

    private static object InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
            throw new InvalidOperationException($"Method '{methodName}' not found.");
        return method.Invoke(target, args);
    }

    private static object InvokePrivateStaticMethod(Type type, string methodName, params object[] args)
    {
        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null)
            throw new InvalidOperationException($"Static method '{methodName}' not found.");
        return method.Invoke(null, args);
    }

    private static int GetPrivateStaticInt(Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null)
            throw new InvalidOperationException($"Field '{fieldName}' not found.");
        return (int)field.GetValue(null);
    }
}
