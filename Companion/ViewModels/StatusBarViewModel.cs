using CommunityToolkit.Mvvm.ComponentModel;
using Companion.Events;
using Companion.Services;
using Serilog;

namespace Companion.ViewModels;

public partial class StatusBarViewModel : ViewModelBase
{
    [ObservableProperty] private string _appVersionText = string.Empty;

    [ObservableProperty] private string _hostNameText = string.Empty;

    [ObservableProperty] private string _messageText = string.Empty;

    [ObservableProperty] private string _statusText = string.Empty;

    public StatusBarViewModel(ILogger logger,
        ISshClientService sshClientService,
        IEventSubscriptionService eventSubscriptionService)
        : base(logger, sshClientService, eventSubscriptionService)
    {
        EventSubscriptionService.Subscribe<AppMessageEvent, AppMessage>(UpdateStatus);

        _appVersionText = GetFormattedAppVersion();
    }

    private string GetFormattedAppVersion()
    {
        var fullVersion = VersionHelper.GetAppVersion();

        // Extract the first part of the version (e.g., "1.0.0")
        var truncatedVersion = fullVersion.Split('+')[0]; // Handles semantic versions like "1.0.0+buildinfo"
        return truncatedVersion.Length > 10 ? truncatedVersion.Substring(0, 10) : truncatedVersion;
    }


    private void UpdateStatus(AppMessage appMessage)
    {
        Log.Verbose(appMessage.ToString());


        if (!string.IsNullOrEmpty(appMessage.Status)) StatusText = appMessage.Status;
        if (!string.IsNullOrEmpty(appMessage.Message)) MessageText = appMessage.Message;

        if (!string.IsNullOrEmpty(appMessage.DeviceConfig.Hostname)) HostNameText = appMessage.DeviceConfig.Hostname;
    }
}
