using Microsoft.Xna.Framework;
using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>The player's lasers against every kind of robot. A laser is used up by the first thing it hits.</summary>
/// <remarks>The kinds are tried in <see cref="RobotKinds.All"/>'s order, and that order is behaviour, because a laser
/// cannot hit two things in one tick. What a hit does to a robot, the sound it makes and its score all come from the
/// kind's own row.</remarks>
public sealed class LaserCollisionPhase : ICollisionPhase
{
    /// <inheritdoc/>
    public void Resolve(PlayField field)
    {
        foreach (RobotKindInfo robot in RobotKinds.All)
        {
            ResolveKind(field, robot);
        }
    }

    /// <summary>Tries every laser against one kind of robot.</summary>
    /// <param name="field">The field the entities are on.</param>
    /// <param name="robot">The kind's row.</param>
    private static void ResolveKind(PlayField field, RobotKindInfo robot)
    {
        foreach (PlayerLaser laser in field.PlayerLasers.GetActiveLasers())
        {
            foreach (IEntity target in field.Entities.GetList(robot.Kind).Entities)
            {
                if (target.LifeState != EntityLifeState.Alive || !field.Touches(laser, target))
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
