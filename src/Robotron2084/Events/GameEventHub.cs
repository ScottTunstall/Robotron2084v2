namespace Robotron2084.Events;

/// <summary>Passes each event it is given on to every listener, in the order they subscribed.</summary>
public sealed class GameEventHub : IGameEvents
{
    private readonly List<IGameEventListener> _listeners = [];

    /// <summary>How many listeners are subscribed.</summary>
    public int ListenerCount => _listeners.Count;

    /// <summary>Starts telling a listener about every event from now on.</summary>
    /// <param name="listener">The listener.</param>
    /// <exception cref="ArgumentNullException"><paramref name="listener" /> is null.</exception>
    public void Subscribe(IGameEventListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        _listeners.Add(listener);
    }

    /// <summary>Tells every listener what happened.</summary>
    /// <param name="gameEvent">What happened and where.</param>
    public void Raise(GameEvent gameEvent)
    {
        foreach (var listener in _listeners) listener.Handle(gameEvent);
    }
}
