namespace Robotron2084.Level;

/// <summary>Every kind of robot the playfield keeps a list of — the kinds a laser or the player can meet.</summary>
/// <remarks>
/// Not a kind here: the family (the rescue targets, <see cref="Entities.Human"/>) and the port's own markers and
/// effects (skull, rescue score, explosion, score burst) — nothing collides with those.
/// </remarks>
public enum RobotKind
{
    /// <summary>An electric post (ROM: the <c>POSTS</c> list).</summary>
    Electrode,

    /// <summary>The basic robot (RRP8).</summary>
    Grunt,

    /// <summary>The heavy robot that walks through the family (RRH11).</summary>
    Hulk,

    /// <summary>The robot that bursts into enforcers (RRC11).</summary>
    Spheroid,

    /// <summary>The spheroid's child, which fires sparks (RRC11).</summary>
    Enforcer,

    /// <summary>The robot that drops tanks (RRTK4).</summary>
    Quark,

    /// <summary>The quark's child (RRTK4).</summary>
    Tank,

    /// <summary>The robot that reprograms the family (RRB10).</summary>
    Brain,

    /// <summary>A human under a brain's control (RRB10).</summary>
    Prog,

    /// <summary>An enforcer's ballistic shot (RRC11).</summary>
    Spark,

    /// <summary>A tank's shell (RRTK4).</summary>
    TankShell,

    /// <summary>A brain's cruise missile (RRB10).</summary>
    CruiseMissile,
}
