using System.Collections.ObjectModel;
using System;
using System.Linq;
using Moq;
using Companion.Models;
using Companion.Services;
using Companion.ViewModels;
using Serilog;
using System.Reflection;
using System.Threading.Tasks;

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
    private Mock<IFirmwareSourceService> _mockFirmwareSourceService;
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
        _mockFirmwareSourceService = new Mock<IFirmwareSourceService>();
        _mockMessageBoxService = new Mock<IMessageBoxService>();
        _mockPreferencesService.Setup(x => x.Load()).Returns(new UserPreferences());
        _mockFirmwareSourceService.Setup(x => x.GetSources()).Returns(FirmwareSourceService.CreateDefaults());
        
        _viewModel = new FirmwareTabViewModel(
            _mockLogger.Object,
            _mockSshClientService.Object,
            _mockEventSubscriptionService.Object,
            _mockGithubService.Object,
            _mockPreferencesService.Object,
            _mockFirmwareSourceService.Object,
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
        // Assert
        Assert.That(_viewModel.SelectedFirmwareTrack, Is.EqualTo("Stable"));
        Assert.That(_viewModel.SelectedFirmwareSource, Is.EqualTo("OpenIPC Builder"));
        Assert.That(_viewModel.SelectedFirmwareChannel, Is.EqualTo("Latest"));
    }

    [Test]
    public void BuildFirmwareDownloadUrl_DefaultSource_UsesOpenIpcReleaseUrl()
    {
        // Arrange / Act
        var url = (string)InvokePrivateMethod(_viewModel, "BuildFirmwareDownloadUrl", "test-fw.tgz");

        // Assert
        Assert.That(url, Is.EqualTo("https://github.com/OpenIPC/builder/releases/latest/download/test-fw.tgz"));
    }

    [Test]
    public void BuildFirmwareDownloadUrl_MaburSource_UsesGilankpamReleaseUrl()
    {
        // Arrange
        _viewModel.SelectedFirmwareTrack = "Test";
        _viewModel.SelectedFirmwareSource = "Mabur";

        // Act
        var url = (string)InvokePrivateMethod(_viewModel, "BuildFirmwareDownloadUrl", "test-fw.tgz");

        // Assert
        Assert.That(url, Is.EqualTo("https://github.com/gilankpam/openipc-builder/releases/latest/download/test-fw.tgz"));
    }

    [Test]
    public void BuildFirmwareDownloadUrl_GregSource_UsesRawGithubUrl()
    {
        // Arrange
        _viewModel.SelectedFirmwareTrack = "Test";
        _viewModel.SelectedFirmwareSource = "Greg APFPV";

        // Act
        var url = (string)InvokePrivateMethod(_viewModel, "BuildFirmwareDownloadUrl", "test-fw.tgz");

        // Assert
        Assert.That(url, Is.EqualTo("https://raw.githubusercontent.com/sickgreg/OpenIPC_sickgregFPV_apfpv/main/test-fw.tgz"));
    }

    [Test]
    public void SelectedFirmwareSource_GregSource_RefreshesAvailableChannels()
    {
        // Arrange / Act
        _viewModel.SelectedFirmwareTrack = "Test";
        _viewModel.SelectedFirmwareSource = "Greg APFPV";

        // Assert
        Assert.That(_viewModel.FirmwareChannels, Is.EqualTo(new[] { "Latest" }));
        Assert.That(_viewModel.SelectedFirmwareChannel, Is.EqualTo("Latest"));
    }

    [Test]
    public void SelectedFirmwareTrack_StableShowsStableSourcesAndTestShowsAllSources()
    {
        // Arrange / Act / Assert
        Assert.That(_viewModel.FirmwareSources, Is.EqualTo(new[] { "OpenIPC Builder" }));

        // Act
        _viewModel.SelectedFirmwareTrack = "Test";

        // Assert
        Assert.That(_viewModel.FirmwareSources, Is.EqualTo(new[] { "OpenIPC Builder", "Greg APFPV", "Mabur" }));
    }

    [Test]
    public void CanUseDropdowns_GregSource_ReturnsFalse()
    {
        // Arrange
        _viewModel.CanConnect = true;
        _viewModel.IsConnected = true;
        _viewModel.SelectedFirmwareTrack = "Test";
        _viewModel.SelectedFirmwareSource = "Greg APFPV";

        // Assert
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

    [Test]
    public void FilterBootloadersByStorage_DefaultsToNor_WhenStorageTypeIsUnknown()
    {
        var filenames = new[]
        {
            "ssc338q-u-boot-nand.bin",
            "ssc338q-u-boot-nor.bin"
        };

        var result = (IEnumerable<string>)InvokePrivateStaticMethod(
            typeof(FirmwareTabViewModel),
            "FilterBootloadersByStorage",
            filenames,
            null);

        Assert.That(result, Is.EqualTo(new[] { "ssc338q-u-boot-nor.bin" }));
    }

    [Test]
    public async Task GetFilenamesAsync_ExcludesJsonAssets()
    {
        _mockGithubService
            .Setup(x => x.GetGitHubDataAsync("https://api.github.com/repos/OpenIPC/builder/releases/latest"))
            .ReturnsAsync("""
                         {
                           "assets": [
                             { "name": "camera-firmware.tgz" },
                             { "name": "latest.json" },
                             { "name": "checksums.JSON" }
                           ]
                         }
                         """);

        var task = (Task<IEnumerable<string>>)InvokePrivateMethod(_viewModel, "GetFilenamesAsync");
        var result = await task;

        Assert.That(result.ToArray(), Is.EqualTo(new[] { "camera-firmware.tgz" }));
    }

    private const string MaburReleasesUrl = "https://api.github.com/repos/gilankpam/openipc-builder/releases?per_page=30";

    private const string MaburReleasesJson = """
        [
          { "tag_name": "mabur-4c94bc23", "draft": false, "prerelease": false, "published_at": "2026-10-23T19:00:27Z" },
          { "tag_name": "mabur-beta", "draft": false, "prerelease": true, "published_at": "2026-10-24T08:00:00Z" },
          { "tag_name": "mabur-05fad388", "draft": false, "prerelease": false, "published_at": "2026-10-23T17:00:09Z" }
        ]
        """;

    private async Task SelectMaburAsync()
    {
        _viewModel.SelectedFirmwareTrack = "Test";
        _viewModel.SelectedFirmwareSource = "Mabur";
        await _viewModel.LoadManufacturersAsync();
    }

    private UserPreferences CreateSavedMaburPreferences(string releaseTag)
    {
        var preferences = new UserPreferences
        {
            PreferredFirmwareTrack = "Test",
            PreferredFirmwareSource = "Mabur",
            PreferredFirmwareChannel = releaseTag
        };
        _mockPreferencesService.Setup(x => x.Load()).Returns(preferences);
        return preferences;
    }

    private FirmwareTabViewModel CreateViewModel() =>
        new(
            _mockLogger.Object,
            _mockSshClientService.Object,
            _mockEventSubscriptionService.Object,
            _mockGithubService.Object,
            _mockPreferencesService.Object,
            _mockFirmwareSourceService.Object,
            _mockMessageBoxService.Object);

    [Test]
    public async Task SelectedFirmwareSource_Mabur_ListsPublishedReleasesNewestFirst()
    {
        _mockGithubService.Setup(x => x.GetGitHubDataAsync(MaburReleasesUrl)).ReturnsAsync(MaburReleasesJson);

        await SelectMaburAsync();

        Assert.That(_viewModel.FirmwareChannels.Count, Is.EqualTo(2));
        Assert.That(_viewModel.FirmwareChannels[0], Does.EndWith("· mabur-4c94bc23 (latest)"));
        Assert.That(_viewModel.FirmwareChannels[1], Does.EndWith("· mabur-05fad388"));
        Assert.That(_viewModel.SelectedFirmwareChannel, Is.EqualTo(_viewModel.FirmwareChannels[0]));
    }

    [Test]
    public void Constructor_SavedMaburSource_LoadsReleasesWithoutDeviceConnection()
    {
        CreateSavedMaburPreferences("Latest");
        _mockGithubService.Setup(x => x.GetGitHubDataAsync(MaburReleasesUrl)).ReturnsAsync(MaburReleasesJson);

        _viewModel = CreateViewModel();

        Assert.That(_viewModel.FirmwareChannels.Count, Is.EqualTo(2));
        Assert.That(_viewModel.SelectedFirmwareChannel, Does.EndWith("· mabur-4c94bc23 (latest)"));
    }

    [Test]
    public async Task BuildFirmwareDownloadUrl_MaburOlderRelease_UsesReleaseTagUrl()
    {
        _mockGithubService.Setup(x => x.GetGitHubDataAsync(MaburReleasesUrl)).ReturnsAsync(MaburReleasesJson);
        await SelectMaburAsync();

        _viewModel.SelectedFirmwareChannel = _viewModel.FirmwareChannels[1];
        var url = (string)InvokePrivateMethod(_viewModel, "BuildFirmwareDownloadUrl", "test-fw.tgz");

        Assert.That(url, Is.EqualTo("https://github.com/gilankpam/openipc-builder/releases/download/mabur-05fad388/test-fw.tgz"));
    }

    [Test]
    public async Task SelectedFirmwareChannel_MaburRelease_SavesReleaseTag()
    {
        var preferences = new UserPreferences();
        _mockPreferencesService.Setup(x => x.Load()).Returns(preferences);
        _mockGithubService.Setup(x => x.GetGitHubDataAsync(MaburReleasesUrl)).ReturnsAsync(MaburReleasesJson);
        await SelectMaburAsync();

        _viewModel.SelectedFirmwareChannel = _viewModel.FirmwareChannels[1];

        Assert.That(preferences.PreferredFirmwareChannel, Is.EqualTo("mabur-05fad388"));
    }

    [Test]
    public async Task LoadManufacturersAsync_SavedMaburTag_RestoresThatRelease()
    {
        var preferences = CreateSavedMaburPreferences("mabur-05fad388");
        _mockGithubService.Setup(x => x.GetGitHubDataAsync(MaburReleasesUrl)).ReturnsAsync(MaburReleasesJson);

        _viewModel = CreateViewModel();
        await _viewModel.LoadManufacturersAsync();

        Assert.That(_viewModel.SelectedFirmwareChannel, Does.EndWith("· mabur-05fad388"));
        Assert.That(preferences.PreferredFirmwareChannel, Is.EqualTo("mabur-05fad388"));
    }

    [Test]
    public async Task LoadManufacturersAsync_SavedMaburTagDeleted_SelectsNewestRelease()
    {
        var preferences = CreateSavedMaburPreferences("mabur-deleted");
        _mockGithubService.Setup(x => x.GetGitHubDataAsync(MaburReleasesUrl)).ReturnsAsync(MaburReleasesJson);

        _viewModel = CreateViewModel();
        await _viewModel.LoadManufacturersAsync();

        Assert.That(_viewModel.SelectedFirmwareChannel, Does.EndWith("· mabur-4c94bc23 (latest)"));
        Assert.That(preferences.PreferredFirmwareChannel, Is.EqualTo("mabur-4c94bc23"));
    }

    [Test]
    public async Task LoadManufacturersAsync_MaburReleaseListUnavailable_FallsBackToLatest()
    {
        _mockGithubService.Setup(x => x.GetGitHubDataAsync(MaburReleasesUrl)).ReturnsAsync(string.Empty);

        await SelectMaburAsync();
        var url = (string)InvokePrivateMethod(_viewModel, "BuildFirmwareDownloadUrl", "test-fw.tgz");

        Assert.That(_viewModel.FirmwareChannels, Is.EqualTo(new[] { "Latest" }));
        Assert.That(url, Is.EqualTo("https://github.com/gilankpam/openipc-builder/releases/latest/download/test-fw.tgz"));
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
