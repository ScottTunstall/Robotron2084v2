using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>The player touching anything that is deadly to touch: the walking robots and every enemy shot. Only the player dies.</summary>
/// <remarks>Which kinds are deadly comes from <see cref="RobotKindInfo.KillsPlayerOnContact"/>. The thing touched is not
/// removed; only a laser removes a shot.</remarks>
public sealed class PlayerContactKillRule : ICollisionRule
{
    /// <inheritdoc/>
    public void Resolve(PlayField field)
    {
        if (field.Player.LifeState != EntityLifeState.Alive || field.Player.IsInvincible)
        {
            return;
        }

        foreach (RobotKindInfo robot in RobotKinds.All)
        {
            if (robot.KillsPlayerOnContact)
            {
                KillPlayerOnContact(field, field.Entities.GetList(robot.Kind));
            }
        }
    }

    /// <summary>Kills the player if they are touching any living thing in a list.</summary>
    /// <param name="field">The field the entities are on.</param>
    /// <param name="robots">The kind's list.</param>
    /// <remarks>The player is checked again before every one, because an earlier one may already have killed them, and a
    /// second kill would start the death again.</remarks>
    private static void KillPlayerOnContact(PlayField field, IEntityList robots)
    {
        Player player = field.Player;
        foreach (IEntity entity in robots.Entities)
        {
            if (player.LifeState != EntityLifeState.Alive)
            {
                return;
            }

            if (entity.LifeState == EntityLifeState.Alive && field.Touches(player, entity))
            {
                player.Kill();
                return;
            }
        }
    }
}
