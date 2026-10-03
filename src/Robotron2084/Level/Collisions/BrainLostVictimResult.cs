using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A brain was shot while it was reprogramming a human.</summary>
/// <param name="Brain">The dead brain.</param>
internal sealed record BrainLostVictimResult(Brain Brain) : CollisionResult;
