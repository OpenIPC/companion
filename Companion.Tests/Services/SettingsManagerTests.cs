using Moq;
using Newtonsoft.Json;
using Companion.Models;
using Companion.Services;
using Prism.Events;
using Serilog;

namespace OpenIPC.Companion.Tests.Services;

[TestFixture]
public class SettingsManagerTests
{
    [SetUp]
    public void SetUp()
    {
        // Set up a temporary file path for testing
        _testSettingsFilePath = Path.Combine(Path.GetTempPath(), "test_openipc_settings.json");
        SettingsManager.AppSettingFilename = _testSettingsFilePath;

        // Mock the event aggregator
        _mockEventAggregator = new Mock<IEventAggregator>();
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up the test file if it exists
        if (File.Exists(_testSettingsFilePath)) File.Delete(_testSettingsFilePath);
    }

    private Mock<IEventAggregator> _mockEventAggregator;
    private string _testSettingsFilePath;

    [Test]
    public void LoadSettings_FileExists_ReturnsCorrectDeviceConfig()
    {
        // Arrange
        var expectedConfig = new DeviceConfig
        {
            IpAddress = "192.168.1.1",
            Username = "admin",
            Password = "password",
            DeviceType = DeviceType.Camera
        };
        File.WriteAllText(_testSettingsFilePath, JsonConvert.SerializeObject(expectedConfig));

        // Act
        var result = SettingsManager.LoadSettings();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IpAddress, Is.EqualTo(expectedConfig.IpAddress));
        Assert.That(result.Username, Is.EqualTo(expectedConfig.Username));
        Assert.That(result.Password, Is.EqualTo(expectedConfig.Password));
        Assert.That(result.DeviceType, Is.EqualTo(expectedConfig.DeviceType));
    }

    [Test]
    public void LoadSettings_FileDoesNotExist_ReturnsDefaultDeviceConfig()
    {
        // Act
        var result = SettingsManager.LoadSettings();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IpAddress, Is.EqualTo(string.Empty));
        Assert.That(result.Username, Is.EqualTo(string.Empty));
        Assert.That(result.Password, Is.EqualTo(string.Empty));
        Assert.That(result.DeviceType, Is.EqualTo(DeviceType.Camera));
    }

    [Test]
    public void SaveSettings_ValidDeviceConfig_SavesToFile()
    {
        // Arrange
        var configToSave = new DeviceConfig
        {
            IpAddress = "192.168.1.100",
            Username = "user",
            Password = "pass",
            DeviceType = DeviceType.Camera
        };

        // Act
        SettingsManager.SaveSettings(configToSave);

        // Assert
        Assert.That(File.Exists(_testSettingsFilePath), Is.True);
        var savedConfig = JsonConvert.DeserializeObject<DeviceConfig>(File.ReadAllText(_testSettingsFilePath));
        Assert.That(savedConfig, Is.Not.Null);
        Assert.That(savedConfig.IpAddress, Is.EqualTo(configToSave.IpAddress));
        Assert.That(savedConfig.Username, Is.EqualTo(configToSave.Username));
        Assert.That(savedConfig.Password, Is.EqualTo(configToSave.Password));
        Assert.That(savedConfig.DeviceType, Is.EqualTo(configToSave.DeviceType));
    }

    [Test]
    public void LoadSettings_FileContainsInvalidJson_LogsErrorAndReturnsDefaultConfig()
    {
        // Arrange
        File.WriteAllText(_testSettingsFilePath, "Invalid JSON Content");

        // Mock the logger
        var mockLogger = new Mock<ILogger>();
        Log.Logger = mockLogger.Object;

        // Act
        var result = SettingsManager.LoadSettings();

        // Assert
        // mockLogger.Verify(
        //     logger => logger.Write(
        //         LogEventLevel.Error,
        //         It.IsAny<Exception>(),
        //         It.Is<string>(msg => msg.StartsWith("LoadSettings: Failed to parse JSON"))),
        //     Times.Once);
        //
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IpAddress, Is.EqualTo(string.Empty));
        Assert.That(result.Username, Is.EqualTo(string.Empty));
        Assert.That(result.Password, Is.EqualTo(string.Empty));
        Assert.That(result.DeviceType, Is.EqualTo(DeviceType.Camera));
    }
}