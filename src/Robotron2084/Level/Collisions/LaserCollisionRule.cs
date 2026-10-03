using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>The player's lasers against every kind of robot. A laser is used up by the first thing it hits.</summary>
/// <remarks>The kinds are tried in <see cref="RobotKinds.All"/>'s order, and that order is behaviour, because a laser
/// cannot hit two things in one tick. What a hit does to a robot, the sound it makes and its score all come from the
/// kind's own row.</remarks>
internal sealed class LaserCollisionRule : ICollisionRule
{
    /// <inheritdoc/>
    public IEnumerable<CollisionResult> Detect(ICollisionScene scene, FieldEntities entities)
    {
        foreach (RobotKindInfo robot in RobotKinds.All)
        {
            foreach (PlayerLaser laser in scene.GetActiveLasers())
            {
                IEntity? target = entities.GetEntities(robot.Kind).FirstOrDefault(entity => entity.IsAlive() && scene.Touches(laser, entity));
                if (target is not null)
                {
                    yield return new LaserHitResult(laser, target, robot);
                }
            }
        }
    }
}
