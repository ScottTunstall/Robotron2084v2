using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A player laser hit a robot.</summary>
/// <param name="Laser">The laser that hit.</param>
/// <param name="Target">The robot it hit.</param>
/// <param name="KindInfo">The registry row of the robot's kind, which says what a hit does.</param>
internal sealed record LaserHitResult(PlayerLaser Laser, IEntity Target, RobotKindInfo KindInfo) : CollisionResult;
