using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>A brain catching the human it is chasing. The brain stops and the human starts being turned into a prog.</summary>
/// <remarks>The test for being close enough is the brain's own (<see cref="Brain.CatchTargetIfInReach"/>). No brain
/// catches anyone while the robots are held still.</remarks>
public sealed class BrainCatchRule : ICollisionRule
{
    /// <inheritdoc/>
    public void Resolve(PlayField field, FieldEntities entities)
    {
        if (field.RobotsFrozen)
        {
            return;
        }

        foreach (Brain brain in entities.Brains)
        {
            brain.CatchTargetIfInReach(field.PlayfieldBounds);
        }
    }
}
