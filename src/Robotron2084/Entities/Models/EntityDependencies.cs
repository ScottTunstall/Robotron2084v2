using Robotron2084.Events;
using Robotron2084.Graphics;

namespace Robotron2084.Entities.Models;

/// <summary>What every game entity shares with the rest of the game: its sprites, where it announces what happens to it, and its random numbers.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>An entity is built with all three, so that none can be forgotten.</item>
///         <item>Nothing here is optional. A test that does not care where announcements go gives <see cref="NullGameEvents.Instance" />.</item>
///     </list>
/// </remarks>
public sealed record EntityDependencies
{
    /// <summary>Makes the dependencies an entity is built with.</summary>
    /// <param name="textureSet">The shared texture set.</param>
    /// <param name="gameEvents">Where the entity announces what happens to it.</param>
    /// <param name="random">Where the entity's random numbers come from.</param>
    /// <exception cref="ArgumentNullException">Any of them is null.</exception>
    public EntityDependencies(TextureSet textureSet, IGameEvents gameEvents, Random random)
    {
        ArgumentNullException.ThrowIfNull(textureSet);
        ArgumentNullException.ThrowIfNull(gameEvents);
        ArgumentNullException.ThrowIfNull(random);

        TextureSet = textureSet;
        GameEvents = gameEvents;
        Random = random;
    }

    /// <summary>The shared texture set.</summary>
    public TextureSet TextureSet { get; }

    /// <summary>Where the entity announces what happens to it.</summary>
    public IGameEvents GameEvents { get; }

    /// <summary>Where the entity's random numbers come from.</summary>
    public Random Random { get; }
}
