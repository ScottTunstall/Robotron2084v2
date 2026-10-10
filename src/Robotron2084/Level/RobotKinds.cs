using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level.Spawning;

namespace Robotron2084.Level;

/// <summary>The list of robot kinds, with a row for each. It is the one place that a kind is declared (notes §119).</summary>
/// <remarks>
///     <para>
///         The order of <see cref="All" /> matters. It is the order the arcade's own collision code deals with lasers in,
///         and a laser is used up by the first thing it
///         meets, so it cannot hit two things in one tick. The electrode's row is first for that reason (notes §61).
///     </para>
///     <para>
///         To add a kind of robot: write its class, add its animation frames, add a <see cref="RobotKind" /> value and a
///         row here, add a list for it and a
///         <c>GetList</c> case in <see cref="PlayField" />, add that list to the field's update and draw orders, and add
///         it to <c>GetNearestLivingRobotPosition</c>.
///         A test fails until the enum, this list, the field's list orders and its own tables all agree. Nothing more is
///         needed, unless the kind does something
///         new: a way of dealing with a laser that no row here does, a meeting with the electrodes that the two existing
///         ones do not have, or a way of killing the
///         player that the player's rules do not already cover.
///     </para>
/// </remarks>
public static class RobotKinds
{
    // RobotListSetUpOrder below is the order the arcade sets the kinds up in at the start of a wave: the hulks, the brains,
    // the tanks and then the grunts (ROM: RRG23.ASM PLS0A, JSR HULKST / BRNST / TANKST ... RINIT; disassembly $2831 onwards).
    // The author's own BerzerkRobot is put after the grunts, since it is a grunt in all but looks.
    /// <summary>Every kind, in the order the arcade's collision code deals with them.</summary>
    public static readonly RobotKindInfo[] All =
    [
        // The electrodes come first, and a laser that reaches one is spent on it.
        new(RobotKind.Electrode,
            static parameters => parameters.ElectrodeCount,
            ScoreValues.Electrode,
            static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            SoundTables.PostKill,
            Spawn: new ElectrodeWaveSpawner(),
            IsChasedByDemoPlayer: false),

        new(RobotKind.Grunt,
            static parameters => parameters.GruntCount,
            ScoreValues.Grunt,
            static (field, target, direction) =>
            {
                field.KillWithStripExplosion(target, direction);
                // Every grunt death — a laser's or an electrode's — speeds up the survivors (ROM $3A94).
                field.SpeedUpGrunts();
            },
            SoundTables.RobotHit,
            true,
            new GruntWaveSpawner(),
            RobotListSetUpOrder: 4),

        // A hulk is never killed and never scored: the laser only knocks it back (RRH11 HULKIL).
        new(RobotKind.Hulk,
            static parameters => parameters.HulkCount,
            0,
            static (field, target, direction) => target.Require<Hulk>().ApplyKnockback(direction.ToIntVector()),
            SoundTables.HulkHit,
            true,
            new HulkWaveSpawner(),
            RobotListSetUpOrder: 1),

        // A spheroid and a quark play their OWN burst instead of the strip explosion (CIRKP/SQKIL, notes §64).
        new(RobotKind.Spheroid,
            static parameters => parameters.SpheroidCount,
            ScoreValues.Spheroid,
            static (field, target, direction) => field.KillWithScoreBurst(target,
                ScoreBurst.CreateForSpheroid(field.Sprites, target.GetBounds())),
            SoundTables.CircleKill,
            Spawn: new SpheroidWaveSpawner()),

        new(RobotKind.Enforcer,
            null,
            ScoreValues.Enforcer,
            static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            SoundTables.EnforcerKill),

        new(RobotKind.Quark,
            static parameters => parameters.QuarkCount,
            ScoreValues.Quark,
            static (field, target, direction) =>
                field.KillWithScoreBurst(target, ScoreBurst.CreateForQuark(field.Sprites, target.GetBounds())),
            SoundTables.SquareKill,
            Spawn: new QuarkWaveSpawner()),

        // The wave table brings no tanks, but a death keeps the ones that were alive (notes §134).
        new(RobotKind.Tank,
            static parameters => parameters.TankCount,
            ScoreValues.Tank,
            static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            SoundTables.TankKill,
            Spawn: new TankWaveSpawner(),
            RobotListSetUpOrder: 3),

        // A brain killed MID-reprogram releases its victim — the field's own human phase does that (notes §90).
        new(RobotKind.Brain,
            static parameters => parameters.BrainCount,
            ScoreValues.Brain,
            static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            SoundTables.BrainKill,
            true,
            new BrainWaveSpawner(),
            RobotListSetUpOrder: 2),

        new(RobotKind.Prog,
            null,
            ScoreValues.Prog,
            static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            SoundTables.ProgKill,
            true),

        // The shots are removed at once: no explosion and no death of their own, but each has its own sound.
        new(RobotKind.Spark,
            null,
            ScoreValues.Spark,
            static (field, target, direction) => target.Require<IRemovable>().Kill(),
            SoundTables.SparkKill,
            true,
            IsChasedByDemoPlayer: false),

        new(RobotKind.TankShell,
            null,
            ScoreValues.TankShell,
            static (field, target, direction) =>
            {
                target.Require<IRemovable>().Kill();
                // Only a laser kill gives one of the wave's shells back (the fizzle bug, notes §53).
                field.CountShellDestroyed();
            },
            SoundTables.ShellKill,
            true,
            IsChasedByDemoPlayer: false),

        new(RobotKind.CruiseMissile,
            null,
            ScoreValues.CruiseMissile,
            static (field, target, direction) => target.Require<IRemovable>().Kill(),
            SoundTables.CruiseMissileKill,
            true),

        // The author's own robot (notes §138): it dies to a laser as a grunt does, without the grunts' speed-up.
        new(RobotKind.BerzerkRobot,
            static parameters => parameters.BerzerkRobotCount,
            ScoreValues.BerzerkRobot,
            static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            SoundTables.RobotHit,
            true,
            new BerzerkRobotWaveSpawner(),
            RobotListSetUpOrder: 5),

        // The author's own robot (notes §138.2): it stands and animates, and dies to a laser.
        new(RobotKind.Gorf,
            static parameters => parameters.GorfCount,
            ScoreValues.Gorf,
            static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            SoundTables.RobotHit,
            true,
            new GorfWaveSpawner())
    ];

    /// <summary>Finds the row for a kind.</summary>
    /// <param name="kind">The kind to look up.</param>
    public static RobotKindInfo GetInfo(RobotKind kind)
    {
        return All[(int)kind];
    }
}
