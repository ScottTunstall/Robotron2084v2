using Robotron2084.Audio;
using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>The player touching a human rescues them. The player scores a bonus that grows with each rescue, and the bonus is shown where the human stood.</summary>
/// <remarks>Original source: <c>RRG23.ASM</c> <c>COLCHK</c>, and <c>RRH11.ASM</c> <c>HUMKIL</c> for the score shown. The
/// touch leaves <c>PCFLG</c> set, so it is a bonus and no skull, and the player is not harmed.</remarks>
public sealed class PlayerRescueRule : ICollisionRule
{
    /// <inheritdoc/>
    public void Resolve(PlayField field, FieldEntities entities)
    {
        foreach (Human human in entities.GetFamilyMembers())
        {
            if (!human.IsGraspable() || !field.IsPlayerAlive() || !field.TouchesPlayer(human))
            {
                continue;
            }

            human.Rescue();
            int rescues = field.CountRescue();
            field.ShowRescueScore(human.Position);
            field.PlaySoundFrom(SoundTables.SaveAHuman, human.Bounds);
            field.AwardRescueBonus(rescues);
        }
    }
}
