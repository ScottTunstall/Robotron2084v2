using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A brain catching the human it is chasing. The brain stops and the human starts being turned into a prog.</summary>
/// <remarks>The test for being close enough is the brain's own (<see cref="Brain.GetCatchableTarget"/>). No brain
/// catches anyone while the robots are held still.</remarks>
internal sealed class BrainCatchRule : ICollisionRule
{
    /// <inheritdoc/>
    public IEnumerable<CollisionResult> Detect(ICollisionScene scene, FieldEntities entities)
    {
        if (scene.RobotsFrozen())
        {
            yield break;
        }

        foreach (Brain brain in entities.Brains)
        {
            if (brain.GetCatchableTarget() is { } human)
            {
                yield return new BrainCaughtHumanResult(brain, human);
            }
        }
    }
}
