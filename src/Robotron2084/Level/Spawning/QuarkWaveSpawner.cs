using Robotron2084.Entities;

using Robotron2084.Entities.Models;
using Robotron2084.Level.Spawning.Models;

namespace Robotron2084.Level.Spawning;

/// <summary>Puts the wave's quarks on the field, each somewhere along the top wall or the bottom wall.</summary>
/// <remarks>
///     Each quark picks at random how many tanks it will drop from the wave's <c>ENFNUM</c>, and drops them at the wave's
///     <c>TDPTIM</c> pace. Where it starts is the quark's own rule (<see cref="Quark.GetStartPosition" />).
/// </remarks>
public sealed class QuarkWaveSpawner : IWaveSpawner
{
    /// <inheritdoc />
    public void Spawn(WaveSpawnParams waveSpawnParams)
    {
        var playField = waveSpawnParams.PlayField;
        var fieldEntities = waveSpawnParams.FieldEntities;
        for (var i = 0; i < playField.WaveParams.QuarkCount; i++)
        {
            var position = Quark.GetStartPosition(playField.GetPlayfieldBounds(), waveSpawnParams.Random);
            var quark = new Quark(playField.EntityParams, position,
                new QuarkWaveParams(playField.WaveParams.MaxDropsX2, playField.WaveParams.QuarkDropDelay, playField.WaveParams.QuarkMaxSpeed));
            fieldEntities.Quarks.Add(quark);
        }
    }
}
