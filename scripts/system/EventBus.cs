using System;
using System.Collections.Generic;

public interface IGameEvent { }

// ✅ Marker: events that should replay last value to new subscribers
public interface IStickyEvent : IGameEvent { }

public static class EventBus
{
    private static readonly Dictionary<Type, Delegate> _subscribers = new();

    // ✅ stores last value for sticky events only
    private static readonly Dictionary<Type, IGameEvent> _lastSticky = new();

    public static void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
    {
        var type = typeof(T);

        if (_subscribers.TryGetValue(type, out var existingDelegate))
        {
            _subscribers[type] = (Action<T>)existingDelegate + handler;
        }
        else
        {
            _subscribers[type] = handler;
        }

        // ✅ BehaviorSubject behavior: replay last sticky event immediately
        if (typeof(IStickyEvent).IsAssignableFrom(type) && _lastSticky.TryGetValue(type, out var last))
        {
            handler((T)last);
        }
    }

    public static void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent
    {
        var type = typeof(T);

        if (_subscribers.TryGetValue(type, out var existingDelegate))
        {
            var current = (Action<T>)existingDelegate;
            current -= handler;

            if (current == null) _subscribers.Remove(type);
            else _subscribers[type] = current;
        }
    }

    public static void Raise<T>(T gameEvent) where T : struct, IGameEvent
    {
        var type = typeof(T);

        // ✅ store last value if sticky
        if (gameEvent is IStickyEvent)
            _lastSticky[type] = gameEvent;

        if (_subscribers.TryGetValue(type, out var existingDelegate))
        {
            var callback = (Action<T>)existingDelegate;
            callback?.Invoke(gameEvent);
        }
    }

    public static void ClearAll()
    {
        _subscribers.Clear();
        _lastSticky.Clear(); // ✅ also clear sticky cache
    }

    // (Optional) If you ever want to clear only stickies:
    public static void ClearSticky()
    {
        _lastSticky.Clear();
    }
}
