using Robotron2084.Core;

namespace Robotron2084.Level.Spawning;

/// <summary>What a spawner needs to fill a field at the start of a wave.</summary>
/// <param name="Field">The field being filled.</param>
/// <param name="Entities">What is on the field, which the spawner adds to.</param>
/// <param name="Placement">Chooses spots on the field.</param>
/// <param name="Random">The field's random source.</param>
/// <param name="PlayerStart">Where the player starts, which most kinds must keep away from.</param>
/// <param name="FamilySlotsLeftOver">The places in the family list that still held a family member when the last wave or life ended, lowest place first. The hulks pick what to stalk from these.</param>
public sealed record WaveSpawnContext(PlayField Field, FieldEntities Entities, SpawnPlacement Placement, Random Random, IntVector2 PlayerStart, IReadOnlyList<int> FamilySlotsLeftOver);
