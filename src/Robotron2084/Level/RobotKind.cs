namespace Robotron2084.Level;

/// <summary>The kinds of robot and enemy shot that a laser can hit, or that the player can run into.</summary>
/// <remarks>
/// The family (<see cref="Entities.Human"/>) is not a kind here, and neither are the skulls, rescue scores, explosions and score bursts that the
/// port leaves on the field, because nothing collides with those.
/// The source file of each kind's routines is given on its value.
/// </remarks>
public enum RobotKind
{
    /// <summary>A fixed obstacle that kills whatever walks into it, except a hulk.</summary>
    /// <remarks>Original source: the <c>POSTS</c> list, kept by <c>RRP8.ASM</c>.</remarks>
    Electrode,

    /// <summary>The basic robot, which walks towards the player and speeds up as its friends die.</summary>
    /// <remarks>Original source: <c>RRP8.ASM</c>.</remarks>
    Grunt,

    /// <summary>The heavy robot that cannot be killed and tramples the family.</summary>
    /// <remarks>Original source: <c>RRH11.ASM</c>.</remarks>
    Hulk,

    /// <summary>A robot that drifts about and drops enforcers.</summary>
    /// <remarks>Original source: <c>RRC11.ASM</c>.</remarks>
    Spheroid,

    /// <summary>A robot dropped by a spheroid, which fires sparks at the player.</summary>
    /// <remarks>Original source: <c>RRC11.ASM</c>.</remarks>
    Enforcer,

    /// <summary>A robot that drifts about and drops tanks.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c>.</remarks>
    Quark,

    /// <summary>A robot dropped by a quark, which fires shells at the player.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c>.</remarks>
    Tank,

    /// <summary>A robot that catches the family and turns them into progs.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c>.</remarks>
    Brain,

    /// <summary>A family member that a brain has reprogrammed, which now hunts the player.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c>.</remarks>
    Prog,

    /// <summary>A shot fired by an enforcer at the player.</summary>
    /// <remarks>Original source: <c>RRC11.ASM</c>.</remarks>
    Spark,

    /// <summary>A shot fired by a tank, which bounces off the walls until it fizzles out.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c>.</remarks>
    TankShell,

    /// <summary>A homing missile fired by a brain.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c>.</remarks>
    CruiseMissile,

    /// <summary>A robot of the author's own that moves like a grunt. The arcade has no such robot.</summary>
    BerzerkRobot,

    /// <summary>A robot of the author's own that stands and flaps, and drops grunts. The arcade has no such robot.</summary>
    Gorf,
}
