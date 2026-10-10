using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Level.Spawning;

/// <summary>Puts the wave's quarks on the field, each somewhere along the top wall or the bottom wall.</summary>
/// <remarks>Each quark rolls how many tanks it will drop from the wave's <c>ENFNUM</c>, and drops them at the wave's
/// <c>TDPTIM</c> pace. Where it starts is the quark's own rule (<see cref="Quark.GetStartPosition"/>).</remarks>
public sealed class QuarkWaveSpawner : IWaveSpawner
{
    /// <inheritdoc/>
    public void Spawn(WaveSpawnContext context)
    {
        PlayField field = context.Field;
        FieldEntities entities = context.Entities;
        for (int i = 0; i < field.Parameters.QuarkCount; i++)
        {
            IntVector2 position = Quark.GetStartPosition(field.GetPlayfieldBounds(), context.Random);
            var quark = new Quark(field.Sprites, position, context.Random, field.Parameters.MaxDropsX2, field.Parameters.QuarkDropDelay, field.Parameters.QuarkSpeedCap);
            entities.Add(quark);
        }
    }
}
