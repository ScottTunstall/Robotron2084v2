using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level.Spawning;

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
/// To add a robot kind: write the entity class, add its animation frames, add a <see cref="RobotKind"/> value and a row here,
/// add a field for its list and a <c>GetList</c> arm in <see cref="PlayField"/>, add that list to the field's
/// update and draw orders, and add it to <c>GetNearestLivingRobotPosition</c>. A guard test fails until the enum,
/// this registry, the field's list orders and its own hand-written tables agree. No further edit is needed for a
/// kind that brings no NEW behaviour — a laser phase that is not one of the shapes
/// below, an interaction with the electrodes the two existing ones do not have, or a contact rule the player
/// phases do not already express.
/// </para>
/// </remarks>
public static class RobotKinds
{
    /// <summary>Every kind, in the order the ROM's collision phases walk them.</summary>
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
            Spawn: new GruntWaveSpawner()),

        // A hulk is never killed and never scored: the laser only knocks it back (RRH11 HULKIL).
        new(RobotKind.Hulk,
            WaveCount: static parameters => parameters.HulkCount,
            Score: 0,
            LaserHit: static (field, target, direction) => target.Require<Hulk>().ApplyKnockback(direction.ToIntVector()),
            LaserHitSound: SoundTables.HulkHit,
            KillsPlayerOnContact: true,
            Spawn: new HulkWaveSpawner()),

        // A spheroid and a quark play their OWN burst instead of the strip explosion (CIRKP/SQKIL, notes §64).
        new(RobotKind.Spheroid,
            WaveCount: static parameters => parameters.SpheroidCount,
            Score: ScoreValues.Spheroid,
            LaserHit: static (field, target, direction) => field.KillWithScoreBurst(target, ScoreBurst.CreateForSpheroid(field.Sprites, target.Bounds)),
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
            LaserHit: static (field, target, direction) => field.KillWithScoreBurst(target, ScoreBurst.CreateForQuark(field.Sprites, target.Bounds)),
            LaserHitSound: SoundTables.SquareKill,
            Spawn: new QuarkWaveSpawner()),

        // The wave table brings no tanks, but a death keeps the ones that were alive (notes §134).
        new(RobotKind.Tank,
            WaveCount: static parameters => parameters.TankCount,
            Score: ScoreValues.Tank,
            LaserHit: static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            LaserHitSound: SoundTables.TankKill,
            Spawn: new TankWaveSpawner()),

        // A brain killed MID-reprogram releases its victim — the field's own human phase does that (notes §90).
        new(RobotKind.Brain,
            WaveCount: static parameters => parameters.BrainCount,
            Score: ScoreValues.Brain,
            LaserHit: static (field, target, direction) => field.KillWithStripExplosion(target, direction),
            LaserHitSound: SoundTables.BrainKill,
            KillsPlayerOnContact: true,
            Spawn: new BrainWaveSpawner()),

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
                // Only a laser kill counts against the wave's twenty shells (the fizzle bug, notes §53).
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
            Spawn: new BerzerkRobotWaveSpawner()),
    ];

    /// <summary>One kind's row.</summary>
    /// <param name="kind">The kind to look up.</param>
    public static RobotKindInfo GetInfo(RobotKind kind) => All[(int)kind];
}
