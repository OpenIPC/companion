using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Companion.Events;
using Companion.Models;
using Companion.Services;
using Serilog;

namespace Companion.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    protected readonly IEventSubscriptionService EventSubscriptionService;
    protected readonly ILogger Logger;
    protected readonly ISshClientService SshClientService;


    protected ViewModelBase(
        ILogger logger,
        ISshClientService sshClientService,
        IEventSubscriptionService eventSubscriptionService)
    {
        //Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Logger = logger?.ForContext(GetType()) ?? 
                 throw new ArgumentNullException(nameof(logger));
        SshClientService = sshClientService ?? throw new ArgumentNullException(nameof(sshClientService));
        EventSubscriptionService = eventSubscriptionService ??
                                   throw new ArgumentNullException(nameof(eventSubscriptionService));
    }

    /// <summary>
    ///     Publishes a UI message via the event aggregator.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public virtual void UpdateUIMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            Logger.Warning("UpdateUIMessage called with an empty message.");
            return;
        }

        Logger.Verbose("Publishing UI message: {Message}", message);
        EventSubscriptionService.Publish<AppMessageEvent, AppMessage>(new AppMessage
        {
            Message = message,
            UpdateLogView = false,
            CanConnect = DeviceConfig.Instance.CanConnect
        });
    }
}