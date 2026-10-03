using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>The player walked onto an electrode.</summary>
/// <param name="Electrode">The electrode the player touched.</param>
internal sealed record PlayerHitElectrodeResult(Electrode Electrode) : CollisionResult;
