using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A player laser hit a robot.</summary>
/// <param name="PlayerLaser">The laser that hit.</param>
/// <param name="TargetEntity">The robot it hit.</param>
/// <param name="RobotKindInfo">The registry row of the robot's kind, which says what a hit does.</param>
internal sealed record LaserHitResult(PlayerLaser PlayerLaser, IEntity TargetEntity, RobotKindInfo RobotKindInfo) : CollisionResult;
