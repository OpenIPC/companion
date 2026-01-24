using Companion.Models;
using Prism.Events;

namespace Companion.Events;

public class AppMessageEvent : PubSubEvent<AppMessage>
{
}

public class AppMessage
{
    private string _status = string.Empty;

    public bool UpdateLogView { get; set; } = false;

    public string Message { get; set; } = string.Empty;

    public string? Status
    {
        get => _status;
        set => _status = value ?? string.Empty;
    }

    public DeviceConfig DeviceConfig { get; set; } = DeviceConfig.Instance;


    public bool CanConnect { get; set; }

    public override string ToString()
    {
        return $"{nameof(Message)}: {Message}, {nameof(Status)}: {Status}, " +
               $"{nameof(DeviceConfig)}: {DeviceConfig}, {nameof(CanConnect)}: {CanConnect}";
    }
}
