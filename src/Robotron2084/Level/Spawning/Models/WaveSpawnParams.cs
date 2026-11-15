using Robotron2084.Core.Models;

namespace Robotron2084.Level.Spawning.Models;

/// <summary>What a spawner needs to fill a field at the start of a wave.</summary>
public sealed record WaveSpawnParams
{
    /// <summary>Makes what a spawner is given.</summary>
    /// <param name="playField">The field being filled.</param>
    /// <param name="fieldEntities">What is on the field, which the spawner adds to.</param>
    /// <param name="spawnPlacement">Chooses spots on the field.</param>
    /// <param name="random">The field's random source.</param>
    /// <param name="playerStart">Where the player starts, which most kinds must keep away from.</param>
    /// <param name="familySlotsLeftOver">
    ///     The places in the family list that still held a family member when the last wave or
    ///     life ended, lowest place first. The hulks pick what to stalk from these.
    /// </param>
    /// <exception cref="ArgumentNullException">Any of the objects is null.</exception>
    public WaveSpawnParams(
        PlayField playField,
        FieldEntities fieldEntities,
        SpawnPlacement spawnPlacement,
        Random random,
        IntVector2 playerStart,
        IReadOnlyList<int> familySlotsLeftOver)
    {
        ArgumentNullException.ThrowIfNull(playField);
        ArgumentNullException.ThrowIfNull(fieldEntities);
        ArgumentNullException.ThrowIfNull(spawnPlacement);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(familySlotsLeftOver);

        PlayField = playField;
        FieldEntities = fieldEntities;
        SpawnPlacement = spawnPlacement;
        Random = random;
        PlayerStart = playerStart;
        FamilySlotsLeftOver = familySlotsLeftOver;
    }

    /// <summary>The field being filled.</summary>
    public PlayField PlayField { get; }

    /// <summary>What is on the field, which the spawner adds to.</summary>
    public FieldEntities FieldEntities { get; }

    /// <summary>Chooses spots on the field.</summary>
    public SpawnPlacement SpawnPlacement { get; }

    /// <summary>The field's random source.</summary>
    public Random Random { get; }

    /// <summary>Where the player starts.</summary>
    public IntVector2 PlayerStart { get; }

    /// <summary>The places in the family list that still held a family member when the last wave or life ended.</summary>
    public IReadOnlyList<int> FamilySlotsLeftOver { get; }
}
