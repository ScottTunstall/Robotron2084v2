using Robotron2084.Entities;
using Robotron2084.Level.Models;

namespace Robotron2084.Level.Collisions.Models;

/// <summary>A player laser hit a robot.</summary>
/// <param name="PlayerLaser">The laser that hit.</param>
/// <param name="TargetEntity">The robot it hit.</param>
/// <param name="RobotKindRow">The registry row of the robot's kind, which says what a hit does.</param>
internal sealed record LaserHitResult(PlayerLaser PlayerLaser, IEntity TargetEntity, RobotKindRow RobotKindRow) : CollisionResult;
