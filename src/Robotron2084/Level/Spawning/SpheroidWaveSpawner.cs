using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Tuning;

namespace Robotron2084.Level.Spawning;

/// <summary>Puts the wave's spheroids on the field, well away from where the player starts and most often near a wall.</summary>
/// <remarks>Each spheroid rolls how many enforcers it will drop from the wave's <c>ENFNUM</c>, and drops them at the wave's <c>CDPTIM</c> pace (notes §11.2).</remarks>
public sealed class SpheroidWaveSpawner : IWaveSpawner
{
    /// <inheritdoc/>
    public void Spawn(WaveSpawnContext context)
    {
        PlayField field = context.Field;
        for (int i = 0; i < field.Parameters.SpheroidCount; i++)
        {
            IntVector2 position = context.Placement.FindSpheroidSpawnPointAwayFrom(context.PlayerStart, SpheroidTuning.MinDistanceFromPlayer);
            var spheroid = new Spheroid(field.Sprites, position, context.Random, field.Parameters.MaxDropsX2, field.Parameters.SpheroidDropDelay);
            field.Entities.Spheroids.Add(spheroid);
            field.QueueMaterialise(spheroid);
        }
    }
}
