using Robotron2084.Entities;
using Robotron2084.Level.Spawning.Models;
using Robotron2084.Tuning;

namespace Robotron2084.Level.Spawning;

/// <summary>Puts the wave's electrodes on the field: clear of each other and not too near where the player starts.</summary>
/// <remarks>Electrodes do not appear strip by strip, so they are not queued to materialise.</remarks>
public sealed class ElectrodeWaveSpawner : IWaveSpawner
{
    /// <inheritdoc />
    public void Spawn(WaveSpawnParams waveSpawnParams)
    {
        var playField = waveSpawnParams.PlayField;
        var fieldEntities = waveSpawnParams.FieldEntities;
        var placement = waveSpawnParams.SpawnPlacement;
        var wave = playField.WaveParams.WaveNumber;

        // A spot is found for a square the size of most things. The "2084" electrode is wider than that, so it is moved left if it would stick into the right wall.
        var furthestRight = playField.GetPlayfieldBounds().Right - Electrode.GetCollisionSize(wave).Width;
        for (var i = 0; i < playField.WaveParams.ElectrodeCount; i++)
        {
            var position = placement.FindSpawnPointAwayFrom(
                waveSpawnParams.PlayerStart, SpawnTuning.ElectrodeMinDistanceFromPlayer, playField.FieldEntities.Electrodes.IsClearOf);
            position = position with { X = Math.Min(position.X, furthestRight) };
            fieldEntities.Electrodes.Add(new Electrode(playField.TextureSet, playField.GameEvents, position, wave));
        }
    }
}
