using Robotron2084.Events.Models;

namespace Robotron2084.Events;

/// <summary>Somewhere to announce that something has happened on the field.</summary>
public interface IGameEvents
{
    /// <summary>Announces that something has happened.</summary>
    /// <param name="gameEvent">What happened and where.</param>
    void Raise(GameEvent gameEvent);
}
