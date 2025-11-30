using Companion.Models;
using Prism.Events;

namespace Companion.Events;

public class DeviceContentUpdateEvent : PubSubEvent<DeviceContentUpdatedMessage>
{
}

public class DeviceContentUpdatedMessage
{
    // Dictionary to store content as key-value pairs


    //public Dictionary<string, string> DeviceContent { get; set; } = new Dictionary<string, string>();

    public DeviceConfig DeviceConfig { get; set; } = new();
    public string WifiBroadcastContent { get; set; } = string.Empty;
    public string ScreenModeContent { get; set; } = string.Empty;
    public string WfbConfContent { get; set; } = string.Empty;

    public string MajesticContent { get; set; } = string.Empty;

    public string TelemetryContent { get; set; } = string.Empty;


    public override string ToString()
    {
        return
            $"{nameof(DeviceConfig)}: {DeviceConfig}, {nameof(WifiBroadcastContent)}: {WifiBroadcastContent}, " +
            $"{nameof(ScreenModeContent)}: {ScreenModeContent}, {nameof(WfbConfContent)}: {WfbConfContent}, " +
            $"{nameof(MajesticContent)}: {MajesticContent}, {nameof(TelemetryContent)}: {TelemetryContent}";
    }
}