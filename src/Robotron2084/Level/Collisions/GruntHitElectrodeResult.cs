using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A grunt walked onto an electrode.</summary>
/// <param name="Grunt">The grunt.</param>
/// <param name="Electrode">The electrode it walked onto.</param>
internal sealed record GruntHitElectrodeResult(Grunt Grunt, Electrode Electrode) : CollisionResult;
