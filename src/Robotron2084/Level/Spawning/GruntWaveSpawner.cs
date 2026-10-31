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
    public void Spawn(WaveSpawnContext waveSpawnContext)
    {
        var playField = waveSpawnContext.PlayField;
        var fieldEntities = waveSpawnContext.FieldEntities;
        var placement = waveSpawnContext.SpawnPlacement;
        for (var i = 0; i < playField.WaveParameters.GruntCount; i++)
        {
            var position = placement.FindSpawnPointAwayFrom(
                waveSpawnContext.PlayerStart, SpawnTuning.GruntMinDistanceFromPlayer, playField.IsClearOfElectrodes);
            var grunt = new Grunt(playField.SpriteSet, position, playField.WaveParameters.GruntMoveDelay, waveSpawnContext.Random);
            fieldEntities.Add(grunt);
        }
    }
}
