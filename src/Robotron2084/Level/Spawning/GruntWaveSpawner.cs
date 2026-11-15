using Robotron2084.Entities.Models;
using Robotron2084.Entities;
using Robotron2084.Level.Spawning.Models;
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
    public void Spawn(WaveSpawnParams waveSpawnParams)
    {
        var playField = waveSpawnParams.PlayField;
        var fieldEntities = waveSpawnParams.FieldEntities;
        var placement = waveSpawnParams.SpawnPlacement;
        for (var i = 0; i < playField.WaveParams.GruntCount; i++)
        {
            var position = placement.FindSpawnPointAwayFrom(
                waveSpawnParams.PlayerStart, SpawnTuning.GruntMinDistanceFromPlayer, playField.FieldEntities.Electrodes.IsClearOf);
            var grunt = new Grunt(playField.EntityParams, position, playField.WaveParams.GruntMoveDelay);
            fieldEntities.Grunts.Add(grunt);
        }
    }
}
