using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A hulk walked onto an electrode.</summary>
/// <param name="Hulk">The hulk.</param>
/// <param name="Electrode">The electrode it walked onto.</param>
internal sealed record HulkHitElectrodeResult(Hulk Hulk, Electrode Electrode) : CollisionResult;
