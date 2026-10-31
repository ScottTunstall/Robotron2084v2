namespace Robotron2084.Level.Collisions;

/// <summary>
///     One rule about two kinds of thing touching, such as a laser hitting a robot or the player rescuing a human. A rule
///     only reports what is touching. It never changes the field.
/// </summary>
internal interface ICollisionRule
{
    /// <summary>Finds every pair this rule is about that is touching, and reports each one as it is found.</summary>
    /// <param name="collisionScene">What the rule may ask about the field.</param>
    /// <param name="fieldEntities">What is on the field.</param>
    /// <returns>The results, one at a time. The rule does not look for the next until it is asked for it.</returns>
    IEnumerable<CollisionResult> Detect(ICollisionScene collisionScene, FieldEntities fieldEntities);
}
