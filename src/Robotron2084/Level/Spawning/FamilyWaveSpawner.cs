using Robotron2084.Entities;

namespace Robotron2084.Level.Spawning;

/// <summary>Puts the wave's family on the field, off the electrodes: the Mikeys first, then the Mommies, then the Daddies.</summary>
/// <remarks>
///     Original source: <c>RRH11.ASM</c> <c>HUMSTV</c>, which places them at random. The order matters, because it decides
///     who
///     holds which place in the <see cref="FamilyList" />. A human will not step onto an electrode, so one put on top of
///     an
///     electrode would be stuck for the whole wave; each is therefore placed with its own box clear of them (notes §77,
///     §88).
/// </remarks>
public sealed class FamilyWaveSpawner : IWaveSpawner
{
    /// <inheritdoc />
    public void Spawn(WaveSpawnContext waveSpawnContext)
    {
        var waveParameters = waveSpawnContext.PlayField.WaveParameters;
        SpawnKind(waveSpawnContext, HumanKind.Mikey, waveParameters.MikeyCount);
        SpawnKind(waveSpawnContext, HumanKind.Mommy, waveParameters.MommyCount);
        SpawnKind(waveSpawnContext, HumanKind.Daddy, waveParameters.DaddyCount);
    }

    /// <summary>Puts a number of one family member on the field.</summary>
    /// <param name="waveSpawnContext">The field being filled and what is needed to choose spots on it.</param>
    /// <param name="kind">Which member.</param>
    /// <param name="count">How many of them.</param>
    private static void SpawnKind(WaveSpawnContext waveSpawnContext, HumanKind kind, int count)
    {
        var playField = waveSpawnContext.PlayField;
        var fieldEntities = waveSpawnContext.FieldEntities;
        var placement = waveSpawnContext.SpawnPlacement;
        for (var i = 0; i < count; i++)
        {
            var position = placement.FindSpawnPoint(playField.IsClearOfElectrodes, Human.GetSpawnSquarePortPixels(kind));
            fieldEntities.Add(new Human(playField.SpriteSet, position, kind, waveSpawnContext.Random));
        }
    }
}
