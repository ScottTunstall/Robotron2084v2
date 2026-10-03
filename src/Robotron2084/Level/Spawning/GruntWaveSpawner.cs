using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Tuning;

namespace Robotron2084.Level.Spawning;

/// <summary>Puts the wave's grunts on the field: off the electrodes and not too near where the player starts. Grunts may stand on each other.</summary>
/// <remarks>Each grunt gets the wave's stagger limit, which shrinks as grunts die (notes §29).</remarks>
public sealed class GruntWaveSpawner : IWaveSpawner
{
    /// <inheritdoc/>
    public void Spawn(WaveSpawnContext context)
    {
        PlayField field = context.Field;
        FieldEntities entities = context.Entities;
        SpawnPlacement placement = context.Placement;
        for (int i = 0; i < field.Parameters.GruntCount; i++)
        {
            IntVector2 position = placement.FindSpawnPointAwayFrom(
                context.PlayerStart, SpawnTuning.GruntMinDistanceFromPlayer, field.IsClearOfElectrodes);
            var grunt = new Grunt(field.Sprites, position, field.Parameters.GruntMoveDelay, random: context.Random);
            entities.Add(grunt);
            field.QueueMaterialise(grunt);
        }
    }
}
