namespace Robotron2084.Level.Collisions;

/// <summary>Every collision rule, in the order the arcade applies them.</summary>
/// <remarks>
/// The order is behaviour: a laser is spent on the first thing it meets, and the player's death is checked before a
/// rescue. Lasers against the wall are not here: a laser tests the wall itself as it moves. No rule makes a spheroid,
/// an enforcer, a quark or a tank fatal to touch; they harm the player only through what they drop and fire.
/// </remarks>
public static class CollisionPhases
{
    /// <summary>The rules, first to last.</summary>
    public static readonly ICollisionPhase[] InArcadeOrder =
    [
        new LaserCollisionPhase(),
        new RobotVsElectrodeCollisionPhase(),
        new PlayerVsElectrodeCollisionPhase(),
        new PlayerContactKillPhase(),
        new BrainVictimReleasePhase(),
        new BrainCatchPhase(),
        new HulkVsHumanCollisionPhase(),
        new PlayerRescuePhase(),
    ];
}
