using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A brain caught the human it was chasing.</summary>
/// <param name="Brain">The brain.</param>
/// <param name="Human">The human it caught.</param>
internal sealed record BrainCaughtHumanResult(Brain Brain, Human Human) : CollisionResult;
