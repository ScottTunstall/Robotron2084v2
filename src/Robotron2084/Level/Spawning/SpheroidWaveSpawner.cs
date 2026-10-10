using Robotron2084.Entities;
using Robotron2084.Tuning;

namespace Robotron2084.Level.Spawning;

/// <summary>Puts the wave's spheroids on the field, well away from where the player starts and most often near a wall.</summary>
/// <remarks>
///     Each spheroid rolls how many enforcers it will drop from the wave's <c>ENFNUM</c>, and drops them at the
///     wave's <c>CDPTIM</c> pace (notes §11.2).
/// </remarks>
public sealed class SpheroidWaveSpawner : IWaveSpawner
{
    /// <inheritdoc />
    public void Spawn(WaveSpawnContext context)
    {
        var field = context.Field;
        var entities = context.Entities;
        var placement = context.Placement;
        for (var i = 0; i < field.Parameters.SpheroidCount; i++)
        {
            var position =
                placement.FindSpheroidSpawnPointAwayFrom(context.PlayerStart, SpheroidTuning.MinDistanceFromPlayer);
            var spheroid = new Spheroid(field.Sprites, position, context.Random, field.Parameters.MaxDropsX2,
                field.Parameters.SpheroidDropDelay);
            entities.Add(spheroid);
        }
    }
}
