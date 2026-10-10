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
        FieldEntities entities = context.Entities;
        SpawnPlacement placement = context.Placement;
        int wave = field.Parameters.LevelNumber;

        // A spot is found for a square the size of most things. The "2084" electrode is wider than that, so it is moved left if it would stick into the right wall.
        int furthestRight = field.GetPlayfieldBounds().Right - Electrode.GetCollisionSize(wave).Width;
        for (int i = 0; i < field.Parameters.ElectrodeCount; i++)
        {
            IntVector2 position = placement.FindSpawnPointAwayFrom(
                context.PlayerStart, SpawnTuning.ElectrodeMinDistanceFromPlayer, field.IsClearOfElectrodes);
            position = position with { X = Math.Min(position.X, furthestRight) };
            entities.Add(new Electrode(field.Sprites, position, wave));
        }
    }
}
