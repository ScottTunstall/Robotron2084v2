using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Level.Spawning;

/// <summary>Puts the wave's family on the field, off the electrodes: the Mikeys first, then the Mommies, then the Daddies.</summary>
/// <remarks>
/// Original source: <c>RRH11.ASM</c> <c>HUMSTV</c>, which places them at random. The order matters, because it decides who
/// holds which place in the <see cref="FamilyList"/>. A human will not step onto an electrode, so one put on top of an
/// electrode would be stuck for the whole wave; each is therefore placed with its own box clear of them (notes §77, §88).
/// </remarks>
public sealed class FamilyWaveSpawner : IWaveSpawner
{
    /// <inheritdoc/>
    public void Spawn(WaveSpawnContext context)
    {
        LevelParameters parameters = context.Field.Parameters;
        SpawnKind(context, HumanKind.Mikey, parameters.MikeyCount);
        SpawnKind(context, HumanKind.Mommy, parameters.MommyCount);
        SpawnKind(context, HumanKind.Daddy, parameters.DaddyCount);
    }

    /// <summary>Puts a number of one family member on the field.</summary>
    /// <param name="context">The field being filled and what is needed to choose spots on it.</param>
    /// <param name="kind">Which member.</param>
    /// <param name="count">How many of them.</param>
    private static void SpawnKind(WaveSpawnContext context, HumanKind kind, int count)
    {
        PlayField field = context.Field;
        FieldEntities entities = context.Entities;
        SpawnPlacement placement = context.Placement;
        for (int i = 0; i < count; i++)
        {
            IntVector2 position = placement.FindSpawnPoint(field.IsClearOfElectrodes, Human.SpawnSquarePortPixels(kind));
            entities.Add(new Human(field.Sprites, position, kind, context.Random));
        }
    }
}
