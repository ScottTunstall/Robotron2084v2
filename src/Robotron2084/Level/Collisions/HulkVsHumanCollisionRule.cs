using Robotron2084.Audio;
using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A hulk walking onto a human. The human dies at once and leaves a skull.</summary>
/// <remarks>Original source: <c>RRH11.ASM</c> <c>HULK</c>, the only robot whose collision check walks the family list,
/// and <c>HUMKIL</c> for the skull and the sound. No hulk kills anyone while the robots are held still.</remarks>
public sealed class HulkVsHumanCollisionRule : ICollisionRule
{
    /// <inheritdoc/>
    public void Resolve(PlayField field)
    {
        if (field.RobotsFrozen)
        {
            return;
        }

        foreach (Human human in field.Entities.Family.Members)
        {
            if (human.IsGraspable() && IsTouchedByAHulk(field, human))
            {
                human.Kill();
                field.LeaveSkull(human.Position);
                field.PlaySoundFrom(SoundTables.KillAHuman, human.Bounds);
            }
        }
    }

    /// <summary>Says whether any living hulk is touching a human.</summary>
    /// <param name="field">The field the entities are on.</param>
    /// <param name="human">The human.</param>
    private static bool IsTouchedByAHulk(PlayField field, Human human) =>
        field.Entities.Hulks.Any(hulk => hulk.LifeState == EntityLifeState.Alive && field.Touches(hulk, human));
}
