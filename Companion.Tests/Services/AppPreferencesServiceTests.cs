using Companion.Models;
using Companion.Services;
using Serilog;

namespace OpenIPC.Companion.Tests.Services;

[TestFixture]
public class AppPreferencesServiceTests
{
    private string _testPreferencesFilePath = null!;
    private string _originalPreferencesFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        _testPreferencesFilePath = Path.Combine(Path.GetTempPath(), $"openipc_preferences_{Guid.NewGuid():N}.json");
        _originalPreferencesFilePath = AppPreferencesService.PreferencesFilename;
        AppPreferencesService.PreferencesFilename = _testPreferencesFilePath;
    }

    [TearDown]
    public void TearDown()
    {
        AppPreferencesService.PreferencesFilename = _originalPreferencesFilePath;

        if (File.Exists(_testPreferencesFilePath))
            File.Delete(_testPreferencesFilePath);
    }

    [Test]
    public void Load_WhenFileDoesNotExist_ReturnsDefaultsAndCreatesFile()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        var service = new AppPreferencesService(logger);

        var preferences = service.Load();

        Assert.That(preferences.AutoScanOpenIpcDevices, Is.True);
        Assert.That(File.Exists(_testPreferencesFilePath), Is.True);
    }

    [Test]
    public void SaveAndLoad_RoundTripsPreferences()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        var service = new AppPreferencesService(logger);
        var expected = new AppPreferences
        {
            AutoScanOpenIpcDevices = false
        };

        service.Save(expected);
        var actual = service.Load();

        Assert.That(actual.AutoScanOpenIpcDevices, Is.False);
    }
}
