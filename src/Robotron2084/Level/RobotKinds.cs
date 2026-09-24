using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>Every kind of robot the playfield keeps a list of — the kinds a laser or the player can meet.</summary>
/// <remarks>
/// Not a kind here: the family (the rescue targets, <see cref="Human"/>) and the port's own markers and
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

/// <summary>
/// Everything the playfield needs to know about ONE robot kind: how many the wave brings, what it is worth, how
/// it is spawned, what a laser does to it and whether touching it is fatal.
/// </summary>
/// <remarks>
/// This is the port's registry of the arcade's robot routines: the wave counts come from <see cref="LevelParameters"/>
/// (the ROM's own wave tables), the scores from <see cref="ScoreValues"/> (notes §11.3), the spawn from the kind's
/// own initialise routine (plan 9.1), and <see cref="LaserHit"/> from the collision phase that kind's routine runs
/// (plan 9.2, notes §61, §64).
/// </remarks>
/// <param name="Kind">Which kind this row describes.</param>
/// <param name="WaveCount">How many of them the wave table brings; null when only another robot makes them.</param>
/// <param name="Score">Points a laser kill is worth (0 for what cannot be killed).</param>
/// <param name="LaserHit">What one laser does to one of them — the kind's whole phase, from the kill to its own burst.</param>
/// <param name="KillsPlayerOnContact">True when touching it kills the player.</param>
/// <param name="Spawn">Builds the wave's own at a chosen spot; null when only another robot makes them.</param>
public sealed record RobotKindInfo(
    RobotKind Kind,
    Func<LevelParameters, int>? WaveCount,
    int Score,
    Action<PlayField, IEntity, Direction8> LaserHit,
    bool KillsPlayerOnContact = false,
    Action<PlayField, IntVector2>? Spawn = null);

/// <summary>
/// The robot kinds, one row each — the single place a kind is declared (notes §119).
/// </summary>
/// <remarks>
/// <para>
/// THE ORDER OF <see cref="All"/> IS BEHAVIOUR. It is the order plan 9.2 (and the ROM's own phase list) resolves
/// laser hits in, which matters because a laser is consumed by the first thing it meets — it cannot hit two
/// things in one frame. The electrode's row is first for exactly that reason (notes §61).
/// </para>
/// <para>
/// To add a robot kind: write the entity class, add its art, add a <see cref="RobotKind"/> value and a row here.
/// Nothing else needs an edit unless the kind brings a NEW behaviour — a laser phase that is not one of the shapes
/// below, an interaction with the electrodes the two existing ones do not have, or a contact rule the player
/// phases do not already express. The update, draw and prune passes follow from
/// <see cref="PlayField"/>'s list orders, which a guard test checks against this enum.
/// </para>
/// </remarks>
public static class RobotKinds
{
    /// <summary>Every kind, in the order the ROM's collision phases walk them.</summary>
    public static readonly RobotKindInfo[] All =
    [
        // The electric posts come first, and a laser that reaches one is spent on it (plan 9.2 step 2).
        new(RobotKind.Electrode,
            WaveCount: static parameters => parameters.ElectrodeCount,
            Score: ScoreValues.Electrode,
            LaserHit: static (field, target, direction) => field.Shatter(target, direction),
            Spawn: static (field, playerStart) => field.SpawnElectrodes(playerStart)),

        new(RobotKind.Grunt,
            WaveCount: static parameters => parameters.GruntCount,
            Score: ScoreValues.Grunt,
            LaserHit: static (field, target, direction) =>
            {
                field.Shatter(target, direction);
                // Every grunt death — a laser's or an electrode's — speeds up the survivors (ROM $3A94).
                field.SpeedUpGrunts();
            },
            KillsPlayerOnContact: true,
            Spawn: static (field, playerStart) => field.SpawnGrunts(playerStart)),

        // A hulk is never killed and never scored: the laser only knocks it back (RRH11 HULKIL).
        new(RobotKind.Hulk,
            WaveCount: static parameters => parameters.HulkCount,
            Score: 0,
            LaserHit: static (field, target, direction) => ((Hulk)target).ApplyKnockback(direction.ToIntVector()),
            KillsPlayerOnContact: true,
            Spawn: static (field, playerStart) => field.SpawnHulks(playerStart)),

        // A spheroid and a quark play their OWN burst instead of the strip explosion (CIRKP/SQKIL, notes §64).
        new(RobotKind.Spheroid,
            WaveCount: static parameters => parameters.SpheroidCount,
            Score: ScoreValues.Spheroid,
            LaserHit: static (field, target, direction) => field.Burst(target, ScoreBurst.ForSpheroid(target.Bounds)),
            Spawn: static (field, playerStart) => field.SpawnSpheroids(playerStart)),

        new(RobotKind.Enforcer,
            WaveCount: null,
            Score: ScoreValues.Enforcer,
            LaserHit: static (field, target, direction) => field.Shatter(target, direction)),

        new(RobotKind.Quark,
            WaveCount: static parameters => parameters.QuarkCount,
            Score: ScoreValues.Quark,
            LaserHit: static (field, target, direction) => field.Burst(target, ScoreBurst.ForQuark(target.Bounds)),
            Spawn: static (field, playerStart) => field.SpawnQuarks(playerStart)),

        new(RobotKind.Tank,
            WaveCount: null,
            Score: ScoreValues.Tank,
            LaserHit: static (field, target, direction) => field.Shatter(target, direction)),

        // A brain killed MID-reprogram releases its victim — the field's own human phase does that (notes §90).
        new(RobotKind.Brain,
            WaveCount: static parameters => parameters.BrainCount,
            Score: ScoreValues.Brain,
            LaserHit: static (field, target, direction) => field.Shatter(target, direction),
            KillsPlayerOnContact: true,
            Spawn: static (field, playerStart) => field.SpawnBrains(playerStart)),

        new(RobotKind.Prog,
            WaveCount: null,
            Score: ScoreValues.Prog,
            LaserHit: static (field, target, direction) => field.Shatter(target, direction),
            KillsPlayerOnContact: true),

        // The shots are removed at once: no explosion, no sound, no death of their own.
        new(RobotKind.Spark,
            WaveCount: null,
            Score: ScoreValues.Spark,
            LaserHit: static (field, target, direction) => ((IRemovable)target).Kill(),
            KillsPlayerOnContact: true),

        new(RobotKind.TankShell,
            WaveCount: null,
            Score: ScoreValues.TankShell,
            LaserHit: static (field, target, direction) =>
            {
                ((IRemovable)target).Kill();
                // Only a laser kill counts against the wave's twenty shells (the fizzle bug, notes §53).
                field.CountShellDestroyed();
            },
            KillsPlayerOnContact: true),

        new(RobotKind.CruiseMissile,
            WaveCount: null,
            Score: ScoreValues.CruiseMissile,
            LaserHit: static (field, target, direction) => ((IRemovable)target).Kill(),
            KillsPlayerOnContact: true),
    ];

    /// <summary>One kind's row.</summary>
    /// <param name="kind">The kind to look up.</param>
    public static RobotKindInfo Of(RobotKind kind) => All[(int)kind];
}
