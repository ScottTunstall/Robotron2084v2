using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level.Spawning;

namespace Robotron2084.Level;

/// <summary>The list of robot kinds, with a row for each. It is the one place that a kind is declared (notes §119).</summary>
/// <remarks>
/// <para>
/// The order of <see cref="All"/> matters. It is the order the arcade's own collision code deals with lasers in, and a laser is used up by the first thing it
/// meets, so it cannot hit two things in one tick. The electrode's row is first for that reason (notes §61).
/// </para>
/// <para>
/// To add a kind of robot: write its class, add its animation frames, add a <see cref="RobotKind"/> value and a row here, add a list for it and a
/// <c>GetList</c> case in <see cref="PlayField"/>, add that list to the field's update and draw orders, and add it to <c>GetNearestLivingRobotPosition</c>.
/// A test fails until the enum, this list, the field's list orders and its own tables all agree. Nothing more is needed, unless the kind does something
/// new: a way of dealing with a laser that no row here does, a meeting with the electrodes that the two existing ones do not have, or a way of killing the
/// player that the player's rules do not already cover.
/// </para>
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
            WaveCount: static parameters => parameters.ElectrodeCount,
            Score: ScoreValues.Electrode,
            LaserHit: static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            LaserHitSound: SoundTables.PostKill,
            Spawn: new ElectrodeWaveSpawner(),
            IsChasedByDemoPlayer: false),

        new(RobotKind.Grunt,
            WaveCount: static parameters => parameters.GruntCount,
            Score: ScoreValues.Grunt,
            LaserHit: static (field, target, direction) =>
            {
                field.KillWithStripExplosion(target, direction);
                // Every grunt death — a laser's or an electrode's — speeds up the survivors (ROM $3A94).
                field.SpeedUpGrunts();
            },
            LaserHitSound: SoundTables.RobotHit,
            KillsPlayerOnContact: true,
            Spawn: new GruntWaveSpawner(),
            RobotListSetUpOrder: 4),

        // A hulk is never killed and never scored: the laser only knocks it back (RRH11 HULKIL).
        new(RobotKind.Hulk,
            WaveCount: static parameters => parameters.HulkCount,
            Score: 0,
            LaserHit: static (field, target, direction) => target.Require<Hulk>().ApplyKnockback(direction.ToIntVector()),
            LaserHitSound: SoundTables.HulkHit,
            KillsPlayerOnContact: true,
            Spawn: new HulkWaveSpawner(),
            RobotListSetUpOrder: 1),

        // A spheroid and a quark play their OWN burst instead of the strip explosion (CIRKP/SQKIL, notes §64).
        new(RobotKind.Spheroid,
            WaveCount: static parameters => parameters.SpheroidCount,
            Score: ScoreValues.Spheroid,
            LaserHit: static (field, target, direction) => field.KillWithScoreBurst(target, ScoreBurst.CreateForSpheroid(field.Sprites, target.GetBounds())),
            LaserHitSound: SoundTables.CircleKill,
            Spawn: new SpheroidWaveSpawner()),

        new(RobotKind.Enforcer,
            WaveCount: null,
            Score: ScoreValues.Enforcer,
            LaserHit: static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            LaserHitSound: SoundTables.EnforcerKill),

        new(RobotKind.Quark,
            WaveCount: static parameters => parameters.QuarkCount,
            Score: ScoreValues.Quark,
            LaserHit: static (field, target, direction) => field.KillWithScoreBurst(target, ScoreBurst.CreateForQuark(field.Sprites, target.GetBounds())),
            LaserHitSound: SoundTables.SquareKill,
            Spawn: new QuarkWaveSpawner()),

        // The wave table brings no tanks, but a death keeps the ones that were alive (notes §134).
        new(RobotKind.Tank,
            WaveCount: static parameters => parameters.TankCount,
            Score: ScoreValues.Tank,
            LaserHit: static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            LaserHitSound: SoundTables.TankKill,
            Spawn: new TankWaveSpawner(),
            RobotListSetUpOrder: 3),

        // A brain killed MID-reprogram releases its victim — the field's own human phase does that (notes §90).
        new(RobotKind.Brain,
            WaveCount: static parameters => parameters.BrainCount,
            Score: ScoreValues.Brain,
            LaserHit: static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            LaserHitSound: SoundTables.BrainKill,
            KillsPlayerOnContact: true,
            Spawn: new BrainWaveSpawner(),
            RobotListSetUpOrder: 2),

        new(RobotKind.Prog,
            WaveCount: null,
            Score: ScoreValues.Prog,
            LaserHit: static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            LaserHitSound: SoundTables.ProgKill,
            KillsPlayerOnContact: true),

        // The shots are removed at once: no explosion and no death of their own, but each has its own sound.
        new(RobotKind.Spark,
            WaveCount: null,
            Score: ScoreValues.Spark,
            LaserHit: static (field, target, direction) => target.Require<IRemovable>().Kill(),
            LaserHitSound: SoundTables.SparkKill,
            KillsPlayerOnContact: true,
            IsChasedByDemoPlayer: false),

        new(RobotKind.TankShell,
            WaveCount: null,
            Score: ScoreValues.TankShell,
            LaserHit: static (field, target, direction) =>
            {
                target.Require<IRemovable>().Kill();
                // Only a laser kill gives one of the wave's shells back (the fizzle bug, notes §53).
                field.CountShellDestroyed();
            },
            LaserHitSound: SoundTables.ShellKill,
            KillsPlayerOnContact: true,
            IsChasedByDemoPlayer: false),

        new(RobotKind.CruiseMissile,
            WaveCount: null,
            Score: ScoreValues.CruiseMissile,
            LaserHit: static (field, target, direction) => target.Require<IRemovable>().Kill(),
            LaserHitSound: SoundTables.CruiseMissileKill,
            KillsPlayerOnContact: true),

        // The author's own robot (notes §138): it dies to a laser as a grunt does, without the grunts' speed-up.
        new(RobotKind.BerzerkRobot,
            WaveCount: static parameters => parameters.BerzerkRobotCount,
            Score: ScoreValues.BerzerkRobot,
            LaserHit: static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            LaserHitSound: SoundTables.RobotHit,
            KillsPlayerOnContact: true,
            Spawn: new BerzerkRobotWaveSpawner(),
            RobotListSetUpOrder: 5),

        // The author's own robot (notes §138.2): it stands and animates, and dies to a laser.
        new(RobotKind.Gorf,
            WaveCount: static parameters => parameters.GorfCount,
            Score: ScoreValues.Gorf,
            LaserHit: static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            LaserHitSound: SoundTables.RobotHit,
            KillsPlayerOnContact: true,
            Spawn: new GorfWaveSpawner()),
    ];

    /// <summary>Finds the row for a kind.</summary>
    /// <param name="kind">The kind to look up.</param>
    public static RobotKindInfo GetInfo(RobotKind kind) => All[(int)kind];
}
