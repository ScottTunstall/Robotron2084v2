namespace Robotron2084.Level.Collisions;

/// <summary>One rule about what happens when two kinds of thing touch, such as a laser hitting a robot or the player rescuing a human.</summary>
/// <remarks>A rule finds the pairs that are touching and reports each as a <see cref="CollisionResult"/>. It never acts on the field.
/// The field takes each result as it is reported and responds (<see cref="CollisionResponder"/>) before the rule goes on, so the
/// rule sees the field as it is after each response: a laser that has hit something is gone before the next robot is tried.
/// The rules run in the order <see cref="CollisionRules.InArcadeOrder"/> gives.</remarks>
internal interface ICollisionRule
{
    /// <summary>Finds every pair this rule is about that is touching, and reports each one as it is found.</summary>
    /// <param name="scene">What the rule may ask about the field.</param>
    /// <param name="entities">What is on the field.</param>
    /// <returns>The results, one at a time. The rule does not go on until the caller has asked for the next.</returns>
    IEnumerable<CollisionResult> Detect(ICollisionScene scene, FieldEntities entities);
}
