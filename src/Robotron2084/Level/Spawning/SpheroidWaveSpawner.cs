using Robotron2084.Entities;
using Robotron2084.Tuning;

namespace Robotron2084.Level.Spawning;

/// <summary>Puts the wave's spheroids on the field, well away from where the player starts and most often near a wall.</summary>
/// <remarks>
///     Each spheroid picks at random how many enforcers it will drop from the wave's <c>ENFNUM</c>, and drops them at the
///     wave's <c>CDPTIM</c> pace (notes §11.2).
/// </remarks>
public sealed class SpheroidWaveSpawner : IWaveSpawner
{
    /// <inheritdoc />
    public void Spawn(WaveSpawnContext waveSpawnContext)
    {
        var playField = waveSpawnContext.PlayField;
        var fieldEntities = waveSpawnContext.FieldEntities;
        var placement = waveSpawnContext.SpawnPlacement;
        for (var i = 0; i < playField.WaveParameters.SpheroidCount; i++)
        {
            var position =
                placement.FindSpheroidSpawnPointAwayFrom(waveSpawnContext.PlayerStart, SpheroidTuning.MinDistanceFromPlayer);
            var spheroid = new Spheroid(playField.SpriteSet, position, waveSpawnContext.Random, playField.WaveParameters.MaxDropsX2,
                playField.WaveParameters.SpheroidDropDelay);
            fieldEntities.Add(spheroid);
        }
    }
}
