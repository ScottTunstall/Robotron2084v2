using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>The player walking onto an electrode. Both the player and the electrode die, unless the player cannot be hurt just now.</summary>
public sealed class PlayerVsElectrodeCollisionPhase : ICollisionPhase
{
    /// <inheritdoc/>
    public void Resolve(PlayField field)
    {
        Player player = field.Player;
        if (player.LifeState != EntityLifeState.Alive || player.IsInvincible)
        {
            return;
        }

        foreach (Electrode electrode in field.Entities.Electrodes)
        {
            if (electrode.LifeState == EntityLifeState.Alive && field.Touches(player, electrode))
            {
                player.Kill();
                electrode.Kill();
                return;
            }
        }
    }
}
