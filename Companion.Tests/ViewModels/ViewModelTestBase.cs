using Moq;
using Companion.Events;
using Companion.Services;
using Companion.ViewModels;
using Prism.Events;
using Serilog;

namespace OpenIPC.Companion.Tests.ViewModels;

public abstract class ViewModelTestBase
{
    // xUnit does not have [SetUp], so use a constructor for initialization.
    protected ViewModelTestBase()
    {
        SetUpMocks();
    }

    protected Mock<ILogger> LoggerMock { get; private set; }
    protected Mock<IEventAggregator> EventAggregatorMock { get; private set; }
    protected Mock<ISshClientService> SshClientServiceMock { get; private set; }
    protected Mock<IYamlConfigService> YamlConfigServiceMock { get; private set; }
    
    protected Mock<IGlobalSettingsService> GlobalSettingsServiceMock { get; private set; }

    protected Mock<IEventSubscriptionService> EventSubscriptionServiceMock { get; private set; }

    protected Mock<WfbConfContentUpdatedEvent> WfbConfContentUpdatedEventMock { get; private set; }
    protected Mock<AppMessageEvent> AppMessageEventMock { get; private set; }
    protected Mock<MajesticContentUpdatedEvent> MajesticContentUpdatedEventMock { get; private set; }

    private void SetUpMocks()
    {
        LoggerMock = new Mock<ILogger>();
        LoggerMock.Setup(x => x.ForContext(It.IsAny<Type>())).Returns(LoggerMock.Object);
        
        EventAggregatorMock = new Mock<IEventAggregator>();
        SshClientServiceMock = new Mock<ISshClientService>();
        WfbConfContentUpdatedEventMock = new Mock<WfbConfContentUpdatedEvent>();
        AppMessageEventMock = new Mock<AppMessageEvent>();
        MajesticContentUpdatedEventMock = new Mock<MajesticContentUpdatedEvent>();
        YamlConfigServiceMock = new Mock<IYamlConfigService>();
        EventSubscriptionServiceMock = new Mock<IEventSubscriptionService>();
        GlobalSettingsServiceMock = new Mock<IGlobalSettingsService>();

        EventAggregatorMock
            .Setup(x => x.GetEvent<WfbConfContentUpdatedEvent>())
            .Returns(WfbConfContentUpdatedEventMock.Object);

        EventAggregatorMock
            .Setup(x => x.GetEvent<AppMessageEvent>())
            .Returns(AppMessageEventMock.Object);

        EventAggregatorMock
            .Setup(x => x.GetEvent<MajesticContentUpdatedEvent>())
            .Returns(MajesticContentUpdatedEventMock.Object);
    }
}