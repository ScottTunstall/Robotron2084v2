using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>The player touched a human.</summary>
/// <param name="Human">The human the player touched.</param>
internal sealed record PlayerRescuedHumanResult(Human Human) : CollisionResult;
