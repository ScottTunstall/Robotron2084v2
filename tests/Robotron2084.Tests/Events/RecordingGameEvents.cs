using Robotron2084.Events;

namespace Robotron2084.Tests.Events;

/// <summary>A game events place that writes down every event it is given, so a test can read them back.</summary>
public sealed class RecordingGameEvents : IGameEvents, IGameEventListener
{
    private readonly List<GameEvent> _events = [];

    /// <summary>Every event raised so far, in order.</summary>
    public IReadOnlyList<GameEvent> Events => _events;

    /// <summary>Writes the event down.</summary>
    /// <param name="gameEvent">The event.</param>
    public void Raise(GameEvent gameEvent)
    {
        _events.Add(gameEvent);
    }

    /// <summary>Writes the event down.</summary>
    /// <param name="gameEvent">The event.</param>
    public void Handle(GameEvent gameEvent)
    {
        _events.Add(gameEvent);
    }
}
