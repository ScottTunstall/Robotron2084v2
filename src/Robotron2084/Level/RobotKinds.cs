using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>
/// The robot kinds, one row each — the single place a kind is declared (notes §119).
/// </summary>
/// <remarks>
/// <para>
/// THE ORDER OF <see cref="All"/> IS BEHAVIOUR. It is the order the ROM's own collision phases resolve
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
        // The electric posts come first, and a laser that reaches one is spent on it.
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
            LaserHit: static (field, target, direction) => field.Burst(target, ScoreBurst.ForSpheroid(field.Sprites, target.Bounds)),
            Spawn: static (field, playerStart) => field.SpawnSpheroids(playerStart)),

        new(RobotKind.Enforcer,
            WaveCount: null,
            Score: ScoreValues.Enforcer,
            LaserHit: static (field, target, direction) => field.Shatter(target, direction)),

        new(RobotKind.Quark,
            WaveCount: static parameters => parameters.QuarkCount,
            Score: ScoreValues.Quark,
            LaserHit: static (field, target, direction) => field.Burst(target, ScoreBurst.ForQuark(field.Sprites, target.Bounds)),
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
