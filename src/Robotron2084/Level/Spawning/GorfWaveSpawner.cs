using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Tuning;

namespace Robotron2084.Level.Spawning;

/// <summary>Puts the wave's Gorfs on the field, off the electrodes and not too near where the player starts.</summary>
/// <remarks>A new robot of the author's own with no wave table row yet (notes §138.2).</remarks>
public sealed class GorfWaveSpawner : IWaveSpawner
{
    /// <inheritdoc/>
    public void Spawn(WaveSpawnContext context)
    {
        PlayField field = context.Field;
        for (int i = 0; i < field.Parameters.GorfCount; i++)
        {
            IntVector2 position = context.Placement.FindSpawnPointAwayFrom(
                context.PlayerStart, SpawnTuning.GruntMinDistanceFromPlayer, field.IsClearOfElectrodes);
            var gorf = new Gorf(field.Sprites, position);
            context.Entities.Add(gorf);
            field.QueueMaterialise(gorf);
        }
    }
}
