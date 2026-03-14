using Companion.Models;
using Companion.Services;
using Moq;
using Newtonsoft.Json;
using Serilog;

namespace OpenIPC.Companion.Tests.Services;

[TestFixture]
public class PreferencesServiceTests
{
    private Mock<ILogger> _loggerMock = null!;
    private string _preferencesFilePath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _preferencesFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_preferences.json");
        _loggerMock = new Mock<ILogger>();
        _loggerMock.Setup(x => x.ForContext<PreferencesService>()).Returns(_loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_preferencesFilePath))
            File.Delete(_preferencesFilePath);
    }

    [Test]
    public void Load_FileMissing_ReturnsDefaultPreferences()
    {
        var service = new PreferencesService(_loggerMock.Object, _preferencesFilePath);

        var result = service.Load();

        Assert.That(result.CheckForUpdatesOnStartup, Is.True);
        Assert.That(result.PreferredFirmwareSource, Is.EqualTo("OpenIPC Builder"));
        Assert.That(result.FirmwareFocusedMode, Is.True);
        Assert.That(result.LastSelectedTab, Is.Empty);
        Assert.That(result.IsTabsCollapsed, Is.False);
    }

    [Test]
    public void Save_ThenLoad_RoundTripsPreferences()
    {
        var service = new PreferencesService(_loggerMock.Object, _preferencesFilePath);
        var expected = new UserPreferences
        {
            CheckForUpdatesOnStartup = false,
            PreferredFirmwareSource = "Greg APFPV",
            FirmwareFocusedMode = false,
            LastSelectedTab = "Firmware",
            IsTabsCollapsed = true
        };

        service.Save(expected);
        var result = service.Load();

        Assert.That(result.CheckForUpdatesOnStartup, Is.EqualTo(expected.CheckForUpdatesOnStartup));
        Assert.That(result.PreferredFirmwareSource, Is.EqualTo(expected.PreferredFirmwareSource));
        Assert.That(result.FirmwareFocusedMode, Is.EqualTo(expected.FirmwareFocusedMode));
        Assert.That(result.LastSelectedTab, Is.EqualTo(expected.LastSelectedTab));
        Assert.That(result.IsTabsCollapsed, Is.EqualTo(expected.IsTabsCollapsed));
    }

    [Test]
    public void Load_InvalidJson_ReturnsDefaultPreferences()
    {
        var service = new PreferencesService(_loggerMock.Object, _preferencesFilePath);
        File.WriteAllText(_preferencesFilePath, "{ invalid json");

        var result = service.Load();

        Assert.That(result.CheckForUpdatesOnStartup, Is.True);
        Assert.That(result.PreferredFirmwareSource, Is.EqualTo("OpenIPC Builder"));
        Assert.That(result.FirmwareFocusedMode, Is.True);
        Assert.That(result.LastSelectedTab, Is.Empty);
        Assert.That(result.IsTabsCollapsed, Is.False);
    }

    [Test]
    public void Save_WritesJsonFile()
    {
        var service = new PreferencesService(_loggerMock.Object, _preferencesFilePath);
        var preferences = new UserPreferences { LastSelectedTab = "Setup" };

        service.Save(preferences);

        Assert.That(File.Exists(_preferencesFilePath), Is.True);
        var savedPreferences = JsonConvert.DeserializeObject<UserPreferences>(File.ReadAllText(_preferencesFilePath));
        Assert.That(savedPreferences, Is.Not.Null);
        Assert.That(savedPreferences!.LastSelectedTab, Is.EqualTo("Setup"));
    }
}
