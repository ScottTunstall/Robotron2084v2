using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>The player walking onto an electrode. Both the player and the electrode die, unless the player cannot be hurt just now.</summary>
public sealed class PlayerVsElectrodeCollisionRule : ICollisionRule
{
    /// <inheritdoc/>
    public void Resolve(PlayField field, FieldEntities entities)
    {
        if (!field.CanPlayerBeHurt())
        {
            return;
        }

        foreach (Electrode electrode in entities.Electrodes)
        {
            if (electrode.IsAlive() && field.TouchesPlayer(electrode))
            {
                field.KillPlayer();
                electrode.Kill();
                return;
            }
        }
    }
}
