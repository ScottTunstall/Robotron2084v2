using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A brain catching the family member it is chasing. The brain stops and the family member starts being turned into a prog.</summary>
/// <remarks>
///     The test for being close enough is the brain's own (<see cref="Brain.GetCatchableTarget" />). No brain
///     catches anyone while the robots are held still.
/// </remarks>
internal sealed class BrainCatchRule : ICollisionRule
{
    /// <inheritdoc />
    public IEnumerable<CollisionResult> Detect(ICollisionScene collisionScene, FieldEntities fieldEntities)
    {
        if (collisionScene.RobotsFrozen()) yield break;

        foreach (var brain in fieldEntities.Brains)
            if (brain.GetCatchableTarget() is { } familyMember)
                yield return new BrainCaughtFamilyMemberResult(brain, familyMember);
    }
}
