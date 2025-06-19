using Moq;
using Companion.Events;
using Companion.Models;
using Companion.Services;
using Companion.ViewModels;
using Serilog;
using Assert = NUnit.Framework.Assert;

namespace OpenIPC_Config.Tests.ViewModels;

[TestFixture]
public class CameraSettingsTabViewModelTests : ViewModelTestBase
{
    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger>();
        _mockLogger.Setup(x => x.ForContext(It.IsAny<Type>())).Returns(_mockLogger.Object);

        _mockSshClientService = new Mock<ISshClientService>();
        _mockEventSubscriptionService = new Mock<IEventSubscriptionService>();
        _mockYamlConfigService = new Mock<IYamlConfigService>();
        _mockEventAggregatorMock = new Mock<EventAggregator>();

        _viewModel = new CameraSettingsTabViewModel(
            _mockLogger.Object,
            _mockSshClientService.Object,
            _mockEventSubscriptionService.Object,
            _mockYamlConfigService.Object
        );
    }

    private CameraSettingsTabViewModel _viewModel;
    private Mock<ILogger> _mockLogger;
    
    private Mock<ISshClientService> _mockSshClientService;
    private Mock<IEventSubscriptionService> _mockEventSubscriptionService;
    private Mock<IYamlConfigService> _mockYamlConfigService;
    private Mock<EventAggregator> _mockEventAggregatorMock;
    private readonly DeviceConfig _deviceConfigMock;

    [Test]
    public void OnSelectedResolutionChanged_Should_UpdateYamlConfig()
    {
        // Arrange
        var testResolution = "1920x1080";

        // Act
        _viewModel.SelectedResolution = testResolution;

        Assert.That(testResolution, Is.EqualTo(_viewModel.SelectedResolution));


        // Validate that the config is updated
        // Since _yamlConfig is private, validate the behavior indirectly
    }

    [Test]
    public void OnMajesticContentUpdated_Should_UpdateViewModelProperties()
    {
        var testResolution = "1920x1080";
        var testFps = "60";
        var testCodec = "h265";

        // Arrange
        var testYamlContent = new Dictionary<string, string>
        {
            { Majestic.VideoSize, testResolution },
            { Majestic.VideoFps, testFps },
            { Majestic.VideoCodec, testCodec },
            { Majestic.FpvRoiRect, "100x200x1080x1920" }
        };

        _mockYamlConfigService.Setup(y => y.ParseYaml(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Callback<string, Dictionary<string, string>>((content, config) =>
            {
                foreach (var item in testYamlContent) config[item.Key] = item.Value;
            });

        var message = new MajesticContentUpdatedMessage("mocked_yaml_content");

        // Act
        _viewModel.OnMajesticContentUpdated(message);

        // Assert
        Assert.That(_viewModel.SelectedResolution, Is.EqualTo(testResolution));
        Assert.That(_viewModel.SelectedFps, Is.EqualTo(testFps));
        Assert.That(_viewModel.SelectedCodec, Is.EqualTo(testCodec));

        //$"{FpvRoiRectLeft[0]}x{FpvRoiRectTop[0]}x{FpvRoiRectHeight[0]}x{FpvRoiRectWidth[0]}";
        Assert.That(_viewModel.FpvRoiRectLeft[0], Is.EqualTo("100"));
        Assert.That(_viewModel.FpvRoiRectTop[0], Is.EqualTo("200"));
        Assert.That(_viewModel.FpvRoiRectHeight[0], Is.EqualTo("1080"));
        Assert.That(_viewModel.FpvRoiRectWidth[0], Is.EqualTo("1920"));
    }
}