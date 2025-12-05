using Prism.Events;

namespace Companion.Events;

public class WfbConfContentUpdatedEvent : PubSubEvent<WfbConfContentUpdatedMessage>
{
}

public class WfbConfContentUpdatedMessage
{
    public WfbConfContentUpdatedMessage(string content)
    {
        Content = content;
    }

    public string Content { get; set; }
}