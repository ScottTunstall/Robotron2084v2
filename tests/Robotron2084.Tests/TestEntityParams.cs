using Robotron2084.Entities.Models;
using Robotron2084.Entities;
using Robotron2084.Events;

namespace Robotron2084.Tests;

/// <summary>Builds the <see cref="EntityParams" /> a test needs to make an entity: the shared test sprites, and the random numbers the test chooses.</summary>
internal static class TestEntityParams
{
    /// <summary>Makes the dependencies for one entity.</summary>
    /// <param name="random">Where the entity's random numbers come from.</param>
    /// <param name="gameEvents">Where the entity announces what happens to it, or null for a place that tells nobody.</param>
    public static EntityParams Create(Random random, IGameEvents? gameEvents = null)
    {
        return new EntityParams(TestTextures.SharedTextureSet, gameEvents ?? NullGameEvents.Instance, random);
    }
}
