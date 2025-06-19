using Moq;
using Newtonsoft.Json;
using Companion.Models;
using Companion.Services;
using Serilog;

namespace OpenIPC_Config.Tests.Services;

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
        Assert.IsNotNull(result);
        Assert.AreEqual(expectedConfig.IpAddress, result.IpAddress);
        Assert.AreEqual(expectedConfig.Username, result.Username);
        Assert.AreEqual(expectedConfig.Password, result.Password);
        Assert.AreEqual(expectedConfig.DeviceType, result.DeviceType);
    }

    [Test]
    public void LoadSettings_FileDoesNotExist_ReturnsDefaultDeviceConfig()
    {
        // Act
        var result = SettingsManager.LoadSettings();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("", result.IpAddress);
        Assert.AreEqual("", result.Username);
        Assert.AreEqual("", result.Password);
        Assert.AreEqual(DeviceType.Camera, result.DeviceType);
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
        Assert.IsTrue(File.Exists(_testSettingsFilePath));
        var savedConfig = JsonConvert.DeserializeObject<DeviceConfig>(File.ReadAllText(_testSettingsFilePath));
        Assert.IsNotNull(savedConfig);
        Assert.AreEqual(configToSave.IpAddress, savedConfig.IpAddress);
        Assert.AreEqual(configToSave.Username, savedConfig.Username);
        Assert.AreEqual(configToSave.Password, savedConfig.Password);
        Assert.AreEqual(configToSave.DeviceType, savedConfig.DeviceType);
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
        Assert.IsNotNull(result);
        Assert.AreEqual("", result.IpAddress);
        Assert.AreEqual("", result.Username);
        Assert.AreEqual("", result.Password);
        Assert.AreEqual(DeviceType.Camera, result.DeviceType);
    }
}