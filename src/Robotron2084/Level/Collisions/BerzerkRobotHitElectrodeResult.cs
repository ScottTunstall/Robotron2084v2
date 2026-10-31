using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A BerzerkRobot walked onto an electrode.</summary>
/// <param name="BerzerkRobot">The robot.</param>
/// <param name="Electrode">The electrode it touched.</param>
internal sealed record BerzerkRobotHitElectrodeResult(BerzerkRobot BerzerkRobot, Electrode Electrode) : CollisionResult;
