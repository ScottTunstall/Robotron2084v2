using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>The player touched something that kills on contact.</summary>
/// <param name="Thing">What the player touched.</param>
internal sealed record PlayerTouchedDeadThingResult(IEntity Thing) : CollisionResult;
