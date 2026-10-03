using Microsoft.Xna.Framework;
using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>The player's lasers against every kind of robot. A laser is used up by the first thing it hits.</summary>
/// <remarks>The kinds are tried in <see cref="RobotKinds.All"/>'s order, and that order is behaviour, because a laser
/// cannot hit two things in one tick. What a hit does to a robot, the sound it makes and its score all come from the
/// kind's own row.</remarks>
public sealed class LaserCollisionRule : ICollisionRule
{
    /// <inheritdoc/>
    public void Resolve(PlayField field, FieldEntities entities)
    {
        foreach (RobotKindInfo robot in RobotKinds.All)
        {
            CheckForLaserHittingRobot(field, entities, robot);
        }
    }

    /// <summary>Tries every laser against one kind of robot.</summary>
    /// <param name="field">The field the entities are on.</param>
    /// <param name="entities">What is on the field.</param>
    /// <param name="robot">The kind's row.</param>
    private static void CheckForLaserHittingRobot(PlayField field, FieldEntities entities, RobotKindInfo robot)
    {
        foreach (PlayerLaser laser in field.GetActiveLasers())
        {
            foreach (IEntity target in entities.GetEntities(robot.Kind))
            {
                if (!target.IsAlive() || !field.Touches(laser, target))
                {
                    continue;
                }

                Rectangle hitBounds = target.Bounds;
                robot.LaserHit(field, target, laser.Direction);
                field.PlaySoundFrom(robot.LaserHitSound, hitBounds);
                field.AwardScore(robot.Score);
                laser.Kill();
                break;
            }
        }
    }
}
