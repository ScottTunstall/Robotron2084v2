using Robotron2084.Entities;
using Robotron2084.Tuning;

namespace Robotron2084.Level.Spawning;

/// <summary>
///     Puts the wave's grunts on the field: off the electrodes and not too near where the player starts. Grunts may
///     stand on each other.
/// </summary>
/// <remarks>Each grunt gets the wave's stagger limit, which shrinks as grunts die (notes §29).</remarks>
public sealed class GruntWaveSpawner : IWaveSpawner
{
    /// <inheritdoc />
    public void Spawn(WaveSpawnContext context)
    {
        var field = context.Field;
        var entities = context.Entities;
        var placement = context.Placement;
        for (var i = 0; i < field.Parameters.GruntCount; i++)
        {
            var position = placement.FindSpawnPointAwayFrom(
                context.PlayerStart, SpawnTuning.GruntMinDistanceFromPlayer, field.IsClearOfElectrodes);
            var grunt = new Grunt(field.Sprites, position, field.Parameters.GruntMoveDelay, context.Random);
            entities.Add(grunt);
        }
    }
}
