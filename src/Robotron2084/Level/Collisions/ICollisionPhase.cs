namespace Robotron2084.Level.Collisions;

/// <summary>One rule about what happens when two kinds of thing touch, such as a laser hitting a robot or the player rescuing a human.</summary>
/// <remarks>The field runs every rule once a tick, in the order <see cref="CollisionPhases.InArcadeOrder"/> gives.</remarks>
public interface ICollisionPhase
{
    /// <summary>Finds every pair this rule is about that is touching, and does what the rule says to them.</summary>
    /// <param name="field">The field the entities are on.</param>
    void Resolve(PlayField field);
}
