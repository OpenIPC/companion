using System;
using Prism.Events;
using Serilog;

namespace Companion.Services;

public interface IEventSubscriptionService
{
    void Subscribe<TEvent, TPayload>(Action<TPayload> action) where TEvent : PubSubEvent<TPayload>, new();

    void Publish<TEvent, TPayload>(TPayload payload) where TEvent : PubSubEvent<TPayload>, new();
}

public class EventSubscriptionService : IEventSubscriptionService
{
    private readonly IEventAggregator _eventAggregator;
    private readonly ILogger _logger;

    public EventSubscriptionService(IEventAggregator eventAggregator, ILogger logger)
    {
        _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        _logger = logger?.ForContext(GetType()) ?? 
                  throw new ArgumentNullException(nameof(logger));
    }
    
    public void Subscribe<TEvent, TPayload>(Action<TPayload> action) where TEvent : PubSubEvent<TPayload>, new()
    {
        // Use BackgroundThread for testing to avoid the UI thread restriction
        _eventAggregator.GetEvent<TEvent>().Subscribe(action, ThreadOption.BackgroundThread);
        _logger.Verbose($"Subscribed to event {typeof(TEvent).Name}");
        //Console.WriteLine($"Subscribe: Payload received: {action}");  
    }

    public void Publish<TEvent, TPayload>(TPayload payload) where TEvent : PubSubEvent<TPayload>, new()
    {
        _eventAggregator.GetEvent<TEvent>().Publish(payload);
        _logger.Verbose($"Published event {typeof(TEvent).Name} with payload {payload}");
        //Console.WriteLine($"Publish: Payload received: {payload}");
    }
}