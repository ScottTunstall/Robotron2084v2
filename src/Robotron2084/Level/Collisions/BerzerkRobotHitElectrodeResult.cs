using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A BerzerkRobot walked onto an electrode.</summary>
/// <param name="Robot">The robot.</param>
/// <param name="Electrode">The electrode it touched.</param>
internal sealed record BerzerkRobotHitElectrodeResult(BerzerkRobot Robot, Electrode Electrode) : CollisionResult;
