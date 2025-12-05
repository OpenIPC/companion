using Moq;
using Companion.Services;

namespace OpenIPC.Companion.Tests.Services;

[TestFixture]
public class VersionHelperTests
{
    [SetUp]
    public void SetUp()
    {
        _mockFileSystem = new Mock<IFileSystem>();
        VersionHelper.SetFileSystem(_mockFileSystem.Object);
    }

    [TearDown]
    public void TearDown()
    {
        // Reset the file system to the default implementation
        VersionHelper.SetFileSystem(new FileSystem());
    }

    private Mock<IFileSystem> _mockFileSystem;

    [Test]
    public void GetAppVersion_ReturnsVersionFromFile_InDevelopmentEnvironment()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        var expectedVersion = "1.0.0-test";

        _mockFileSystem.Setup(fs => fs.Exists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.ReadAllText(It.IsAny<string>())).Returns(expectedVersion);

        // Act
        var version = VersionHelper.GetAppVersion();

        // Assert
        Assert.That(version, Is.EqualTo(expectedVersion));
    }


    [Test]
    public void GetAppVersion_ReturnsUnknownVersion_OnException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        _mockFileSystem.Setup(fs => fs.Exists(It.IsAny<string>())).Throws(new Exception("Test exception"));

        // Act
        var version = VersionHelper.GetAppVersion();

        // Assert
        Assert.That(version, Is.EqualTo("Unknown Version"));
    }
}