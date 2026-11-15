using Robotron2084.Entities.Models;
using Robotron2084.Entities;
using Robotron2084.Level.Spawning.Models;
using Robotron2084.Tuning;

namespace Robotron2084.Level.Spawning;

/// <summary>
///     Puts the wave's BerzerkRobots on the field, off the electrodes and not too near where the player starts, as
///     the grunts are.
/// </summary>
/// <remarks>
///     A new robot of the author's own with no wave table row yet (notes §138). It waits as long as the wave's grunts
///     do.
/// </remarks>
public sealed class BerzerkRobotWaveSpawner : IWaveSpawner
{
    /// <inheritdoc />
    public void Spawn(WaveSpawnParams waveSpawnParams)
    {
        var playField = waveSpawnParams.PlayField;
        for (var i = 0; i < playField.WaveParams.BerzerkRobotCount; i++)
        {
            var position = waveSpawnParams.SpawnPlacement.FindSpawnPointAwayFrom(
                waveSpawnParams.PlayerStart, SpawnTuning.GruntMinDistanceFromPlayer, playField.FieldEntities.Electrodes.IsClearOf);
            var berzerkRobot = new BerzerkRobot(playField.EntityParams, position, playField.WaveParams.GruntMoveDelay);
            waveSpawnParams.FieldEntities.BerzerkRobots.Add(berzerkRobot);
        }
    }
}
