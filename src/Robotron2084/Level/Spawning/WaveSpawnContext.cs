using Robotron2084.Core;

namespace Robotron2084.Level.Spawning;

/// <summary>What a spawner needs to fill a field at the start of a wave.</summary>
/// <param name="Field">The field being filled.</param>
/// <param name="Placement">Chooses spots on the field.</param>
/// <param name="Random">The field's random source.</param>
/// <param name="PlayerStart">Where the player starts, which most kinds must keep away from.</param>
public sealed record WaveSpawnContext(PlayField Field, SpawnPlacement Placement, Random Random, IntVector2 PlayerStart);
