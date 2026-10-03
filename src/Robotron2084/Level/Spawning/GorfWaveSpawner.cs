using Robotron2084.Entities;

namespace Robotron2084.Level.Spawning;

/// <summary>Puts the wave's Gorfs on the field. Each starts off the screen, so there is nothing to place.</summary>
/// <remarks>A new robot of the author's own with no wave table row yet (notes §138.2). The wave's own drop bound (the spheroids' <c>ENFNUM</c>) says how many grunts each may drop.</remarks>
public sealed class GorfWaveSpawner : IWaveSpawner
{
    /// <inheritdoc/>
    public void Spawn(WaveSpawnContext context)
    {
        PlayField field = context.Field;
        for (int i = 0; i < field.Parameters.GorfCount; i++)
        {
            context.Entities.Add(new Gorf(field.Sprites, context.Random, field.PlayfieldBounds, field.Parameters.MaxDropsX2));
        }
    }
}
