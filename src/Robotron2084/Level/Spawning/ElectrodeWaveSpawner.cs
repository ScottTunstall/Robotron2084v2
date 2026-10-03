using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Tuning;

namespace Robotron2084.Level.Spawning;

/// <summary>Puts the wave's electrodes on the field: clear of each other and not too near where the player starts.</summary>
/// <remarks>Electrodes do not appear strip by strip, so they are not queued to materialise.</remarks>
public sealed class ElectrodeWaveSpawner : IWaveSpawner
{
    /// <inheritdoc/>
    public void Spawn(WaveSpawnContext context)
    {
        PlayField field = context.Field;
        for (int i = 0; i < field.Parameters.ElectrodeCount; i++)
        {
            IntVector2 position = context.Placement.FindSpawnPointAwayFrom(
                context.PlayerStart, SpawnTuning.ElectrodeMinDistanceFromPlayer, field.IsClearOfElectrodes);
            field.Entities.Electrodes.Add(new Electrode(field.Sprites, position, field.Parameters.LevelNumber));
        }
    }
}
