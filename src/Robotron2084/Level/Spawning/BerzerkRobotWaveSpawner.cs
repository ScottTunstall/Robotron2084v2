using Robotron2084.Entities;
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
    public void Spawn(WaveSpawnContext waveSpawnContext)
    {
        var playField = waveSpawnContext.PlayField;
        for (var i = 0; i < playField.WaveParameters.BerzerkRobotCount; i++)
        {
            var position = waveSpawnContext.SpawnPlacement.FindSpawnPointAwayFrom(
                waveSpawnContext.PlayerStart, SpawnTuning.GruntMinDistanceFromPlayer, playField.IsClearOfElectrodes);
            var berzerkRobot = new BerzerkRobot(playField.SpriteSet, position, playField.WaveParameters.GruntMoveDelay, waveSpawnContext.Random);
            waveSpawnContext.FieldEntities.Add(berzerkRobot);
        }
    }
}
