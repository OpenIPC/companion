using Microsoft.Extensions.DependencyInjection;
using Moq;
using Companion.Services;
using Companion.ViewModels;
using Prism.Events;
using Serilog;

namespace OpenIPC.Companion.Tests;

public class DependencyInjectionTests
{
    [Test]
    public void ViewModel_CanBeResolvedFromDI()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddSingleton<IEventAggregator, EventAggregator>();
        services.AddSingleton<IEventSubscriptionService, EventSubscriptionService>();
        services.AddSingleton<ISshClientService, SshClientService>();
        services.AddSingleton<IYamlConfigService, YamlConfigService>();
        
        
        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(x => x.ForContext(It.IsAny<Type>())).Returns(loggerMock.Object);
        services.AddSingleton<ILogger>(sp => loggerMock.Object);

        services.AddTransient<CameraSettingsTabViewModel>();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var viewModel = serviceProvider.GetService<CameraSettingsTabViewModel>();

        // Assert
        Assert.IsNotNull(viewModel);
    }
}