using Prism.Events;

namespace Companion.Events;

public class RadxaContentUpdateChangeEvent : PubSubEvent<RadxaContentUpdatedMessage>
{
}

public class RadxaContentUpdatedMessage
{
    public string WifiBroadcastContent { get; set; }
    public string ScreenModeContent { get; set; }
    public string WfbConfContent { get; set; }

    public string DroneKeyContent { get; set; }


    public override string ToString()
    {
        return
            $"{nameof(WifiBroadcastContent)}: {WifiBroadcastContent}, {nameof(ScreenModeContent)}: {ScreenModeContent}, {nameof(WfbConfContent)}: {WfbConfContent}, {nameof(DroneKeyContent)}: {DroneKeyContent}";
    }
}