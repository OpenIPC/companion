using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Companion.Logging;

public static class LogQueue
{
    private static readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
    private static bool _isReady = false;
    private static readonly List<System.Action<string>> _subscribers = new List<System.Action<string>>();

    public static void Enqueue(string message)
    {
        _queue.Enqueue(message);
        if (_isReady)
        {
            while (_queue.TryDequeue(out var queuedMessage))
            {
                foreach (var subscriber in _subscribers)
                    subscriber(queuedMessage);
            }
        }
    }

    public static void Subscribe(System.Action<string> subscriber)
    {
        _subscribers.Add(subscriber);

        // If queue has messages, trigger the event immediately
        while (_queue.TryDequeue(out var message))
        {
            subscriber(message);
        }

        _isReady = true;
    }
}