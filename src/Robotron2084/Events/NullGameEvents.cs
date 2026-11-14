using Robotron2084.Events.Models;

namespace Robotron2084.Events;

/// <summary>A place to announce events to that tells nobody. It is what an entity announces to until it is given a real one.</summary>
public sealed class NullGameEvents : IGameEvents
{
    /// <summary>The one instance. It holds nothing, so it is shared.</summary>
    public static readonly NullGameEvents Instance = new();

    private NullGameEvents()
    {
    }

    /// <summary>Does nothing.</summary>
    /// <param name="gameEvent">Ignored.</param>
    public void Raise(GameEvent gameEvent)
    {
    }
}
