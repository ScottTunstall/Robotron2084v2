using Robotron2084.Entities;

namespace Robotron2084.Level.Spawning;

/// <summary>Puts the wave's quarks on the field, each somewhere along the top wall or the bottom wall.</summary>
/// <remarks>
///     Each quark picks at random how many tanks it will drop from the wave's <c>ENFNUM</c>, and drops them at the wave's
///     <c>TDPTIM</c> pace. Where it starts is the quark's own rule (<see cref="Quark.GetStartPosition" />).
/// </remarks>
public sealed class QuarkWaveSpawner : IWaveSpawner
{
    /// <inheritdoc />
    public void Spawn(WaveSpawnContext waveSpawnContext)
    {
        var playField = waveSpawnContext.PlayField;
        var fieldEntities = waveSpawnContext.FieldEntities;
        for (var i = 0; i < playField.WaveParameters.QuarkCount; i++)
        {
            var position = Quark.GetStartPosition(playField.GetPlayfieldBounds(), waveSpawnContext.Random);
            var quark = new Quark(playField.SpriteSet, position, waveSpawnContext.Random, playField.WaveParameters.MaxDropsX2,
                playField.WaveParameters.QuarkDropDelay, playField.WaveParameters.QuarkMaxSpeed);
            fieldEntities.Add(quark);
        }
    }
}
