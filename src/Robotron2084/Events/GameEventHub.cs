using Robotron2084.Events.Models;

namespace Robotron2084.Events;

/// <summary>Passes each event it is given on to every listener, in the order they subscribed.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>It belongs to the game's one thread: nothing here is safe to call from two threads at once.</item>
///         <item>
///             A listener is subscribed once, however many times it is asked to subscribe. A listener that subscribes or
///             unsubscribes (itself or another) while an event is being passed on does not change who that event reaches;
///             it takes effect from the next event.
///         </item>
///         <item>A listener that throws stops the event: the listeners after it are not told, and the exception goes to whoever raised it.</item>
///     </list>
/// </remarks>
public sealed class GameEventHub : IGameEvents
{
    private IGameEventListener[] _listeners = [];

    /// <summary>How many listeners are subscribed.</summary>
    public int ListenerCount => _listeners.Length;

    /// <summary>Tells every listener what happened.</summary>
    /// <param name="gameEvent">What happened and where.</param>
    public void Raise(GameEvent gameEvent)
    {
        // The array is replaced, never changed, when a listener comes or goes, so this loop keeps the listeners it started with.
        foreach (var listener in _listeners) listener.Handle(gameEvent);
    }

    /// <summary>Starts telling a listener about every event from now on. A listener that is already subscribed is left as it is.</summary>
    /// <param name="listener">The listener.</param>
    /// <exception cref="ArgumentNullException"><paramref name="listener" /> is null.</exception>
    public void Subscribe(IGameEventListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        if (Array.IndexOf(_listeners, listener) >= 0) return;

        _listeners = [.. _listeners, listener];
    }

    /// <summary>Stops telling a listener about events. A listener that is not subscribed is left alone.</summary>
    /// <param name="listener">The listener.</param>
    /// <exception cref="ArgumentNullException"><paramref name="listener" /> is null.</exception>
    public void Unsubscribe(IGameEventListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        _listeners = [.. _listeners.Where(subscribed => !ReferenceEquals(subscribed, listener))];
    }
}
