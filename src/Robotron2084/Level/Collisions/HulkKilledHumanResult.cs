using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A hulk walked onto a human.</summary>
/// <param name="Hulk">The hulk.</param>
/// <param name="Human">The human it walked onto.</param>
internal sealed record HulkKilledHumanResult(Hulk Hulk, Human Human) : CollisionResult;
