using Robotron2084.Events.Models;

namespace Robotron2084.Events;

/// <summary>Something that wants to be told when things happen on the field.</summary>
public interface IGameEventListener
{
    /// <summary>Reacts to something that has happened.</summary>
    /// <param name="gameEvent">What happened and where.</param>
    void Handle(GameEvent gameEvent);
}
