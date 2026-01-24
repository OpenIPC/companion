using Prism.Events;

namespace Companion.Events;

public class RadxaContentUpdateChangeEvent : PubSubEvent<RadxaContentUpdatedMessage>
{
}

public class RadxaContentUpdatedMessage
{
    public string WifiBroadcastContent { get; set; } = string.Empty;
    public string ScreenModeContent { get; set; } = string.Empty;
    public string WfbConfContent { get; set; } = string.Empty;

    public string DroneKeyContent { get; set; } = string.Empty;


    public override string ToString()
    {
        return
            $"{nameof(WifiBroadcastContent)}: {WifiBroadcastContent}, {nameof(ScreenModeContent)}: {ScreenModeContent}, {nameof(WfbConfContent)}: {WfbConfContent}, {nameof(DroneKeyContent)}: {DroneKeyContent}";
    }
}
