using Prism.Events;

namespace Companion.Events;

public class MajesticContentUpdatedEvent : PubSubEvent<MajesticContentUpdatedMessage>
{
}

public class MajesticContentUpdatedMessage
{
    public MajesticContentUpdatedMessage(string content)
    {
        Content = content;
    }

    public string Content { get; set; }
}