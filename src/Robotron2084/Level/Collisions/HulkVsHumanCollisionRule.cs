using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A hulk walking onto a human. The human dies at once and leaves a skull.</summary>
/// <remarks>Original source: <c>RRH11.ASM</c> <c>HULK</c>, the only robot whose collision check walks the family list,
/// and <c>HUMKIL</c> for the skull and the sound. No hulk kills anyone while the robots are held still.</remarks>
internal sealed class HulkVsHumanCollisionRule : ICollisionRule
{
    /// <inheritdoc/>
    public IEnumerable<CollisionResult> Detect(ICollisionScene scene, FieldEntities entities)
    {
        if (scene.RobotsFrozen())
        {
            yield break;
        }

        foreach (Human human in entities.GetFamilyMembers())
        {
            Hulk? hulk = human.IsGraspable()
                ? entities.Hulks.FirstOrDefault(candidate => candidate.IsAlive() && scene.Touches(candidate, human))
                : null;
            if (hulk is not null)
            {
                yield return new HulkKilledHumanResult(hulk, human);
            }
        }
    }
}
