using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Input;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>
/// The central update/collision/draw hub. Individual entities
/// deliberately don't know about each other — the spec's per-frame draw order
/// and every cross-entity collision rule live here.
///
/// Per-tick update order: hit-stop gate -> wall -> player velocity
/// estimate (before the player moves) -> player -> player lasers -> every
/// entity list -> ResolveCollisions -> prune Dead entities.
/// </summary>
public sealed class PlayField
{
    private static readonly int EntitySize = ScreenSize.Scaled(GameplayConstants.EntitySizeSpecPixels);

    private readonly EntityList<Electrode> _electrodes = new();
    private readonly EntityList<Grunt> _grunts = new();
    private readonly EntityList<Hulk> _hulks = new();
    private readonly EntityList<Spheroid> _spheroids = new();
    private readonly EntityList<Enforcer> _enforcers = new();
    private readonly EntityList<Quark> _quarks = new();
    private readonly EntityList<Tank> _tanks = new();
    private readonly EntityList<Spark> _sparks = new();
    private readonly EntityList<TankShell> _tankShells = new();
    private readonly EntityList<Human> _humans = new();
    private readonly EntityList<SkullMarker> _skulls = new();
    private readonly EntityList<RescueScoreMarker> _rescueScores = new();
    private readonly EntityList<Brain> _brains = new();
    private readonly EntityList<Prog> _progs = new();
    private readonly EntityList<CruiseMissile> _missiles = new();
    private readonly EntityList<Explosion> _explosions = new(); // RRX7 (notes 35): max 7, like the ROM slots

    /// <summary>
    /// Every list, in the ROM's own update order — the field walks this ONE loop to advance its
    /// entities and to drop the dead, so a new kind joins those passes by being in this array.
    /// </summary>
    private readonly IEntityList[] _updateOrder;

    /// <summary>
    /// Every list drawn BEHIND the player's lasers, in the spec's render order: the posts, the family and their
    /// markers, then the robots.
    /// </summary>
    private readonly IEntityList[] _drawOrderBehindShots;

    /// <summary>
    /// Every list drawn IN FRONT of the player's lasers: the enemy shots, the explosions, then the bursts.
    /// Between the two arrays the player's own lasers draw, and the player draws last (spec states that twice).
    /// </summary>
    private readonly IEntityList[] _drawOrderInFrontOfShots;

    // The spheroid's `CIRKP` and the quark's `CIRKV` bursts (notes §64): those two
    // enemies do NOT use the strip explosion — their own pictures play as a solid
    // silhouette and then their "1000" picture appears. The ROM's kill processes
    // take a free object slot, not one of the ten shared EX records, so these are
    // not capped.
    private readonly EntityList<ScoreBurst> _scoreBursts = new();

    // RRG23 `LASDIE` -> `LASDIH`/`LASDIV`: where a laser runs off the playfield the
    // ROM paints the end pixel(s) in the wave's LASCOL slot (RRF.ASM calls it
    // "LASER WALL COLLIDE COLOR") for 2 frames, then in WALCOL for 1 frame, then
    // leaves the wall colour. Two bytes = 2 columns x 4 rows of arcade pixels, and
    // the horizontal-wall case (`LASDIV`) mixes WALCOL's high nibble with LASCOL's
    // low nibble — a dither, so the wall shows through alternate rows. Notes §63.
    private readonly List<LaserWallFlare> _laserWallFlares = new();

    // RRG23's APPEAR: the wave's robots MATERIALISE. The ROM walks the robot list
    // and creates ONE appear record per frame (`LDA #1 / PSHS A ... NAP 1,APL`),
    // every fourth one with the HORIZONTAL (column) fan (`LDA PD,U / ANDA #3 /
    // CMPA #3 / BNE AP1 / JSR HAPST`), while the robots themselves are OFF
    // (`ROBOFF`) until the sequence finishes. Notes §61.4.
    private readonly Queue<IEntity> _pendingAppear = new();
    private readonly Dictionary<IEntity, Explosion?> _assembling = new();
    private int _appearSequence;
    private readonly Random _random;
    private readonly GamePalette? _wallPalette;
    private readonly IPixelCollision? _pixelCollision;
    private IntVector2 _previousPlayerPosition;
    private int _hitStopTicksRemaining;

    // R5 $BE5D: the grunt-speed FLOOR. Wave-table value (RMXSPD) at wave
    // start, then DESCENDED by the level-progress task (−2 per tick while
    // 30+ grunts are alive) down to 1 — 1 arcade px/frame, the arcade
    // player's own speed ($3031 deltas are ±1). Notes §31.
    private int _gruntSpeedFloor;
    private int _gruntSpeedFloorStep = 2; // the ROM's $F0 toggle: −2/−4 then −1/−2
    private int _gruntProgressTimer;

    /// <summary>The artwork this field's entities are built and drawn with — the field owns it because it builds them.</summary>
    internal SpriteSet Sprites { get; }

    public PlayField(
        SpriteSet sprites,
        LevelParameters parameters,
        IPlayerInputSource input,
        Rectangle innerBounds,
        WallColorCycle cycle,
        Random random,
        int startingLives,
        int startingScore = 0,
        int startingRescues = 0,
        GamePalette? wallPalette = null,
        bool playerInvincibleForTesting = true,
        IPixelCollision? pixelCollision = null)
    {
        Sprites = sprites;
        Parameters = parameters;
        _gruntSpeedFloor = parameters.GruntSpeedFloor;
        _updateOrder =
        [
            _electrodes, _grunts, _hulks, _spheroids, _enforcers, _quarks, _tanks, _brains, _progs,
            _sparks, _tankShells, _missiles, _humans, _skulls, _rescueScores, _explosions, _scoreBursts,
        ];
        _drawOrderBehindShots =
        [
            _electrodes, _skulls, _rescueScores, _humans, _grunts, _hulks, _spheroids, _enforcers,
            _quarks, _tanks, _brains, _progs,
        ];
        _drawOrderInFrontOfShots =
        [
            _sparks, _tankShells, _missiles, _explosions, _scoreBursts,
        ];
        // R5 $2A85-2B08: the wave-progress process self-reschedules every
        // 15 vblanks and an internal counter (18 on entry, then 15) gates
        // the speed update → first tick 18×15 = 270 vblanks in, then every
        // 15×15 = 225 vblanks.
        _gruntProgressTimer = GameplayConstants.PortTicks(270);
        RescuesThisLife = startingRescues;
        Input = input;
        Score = new ScoreBoard(startingScore);
        _random = random;
        _wallPalette = wallPalette;
        _pixelCollision = pixelCollision;
        Wall = new PlayfieldWall(innerBounds, cycle);

        IntVector2 playerStart = new(innerBounds.X + innerBounds.Width / 2, innerBounds.Y + innerBounds.Height / 2);
        Player = new Player(Sprites, playerStart, startingLives) { InvincibleForTesting = playerInvincibleForTesting };
        PlayerLasers = new LaserSlots(Sprites);
        _previousPlayerPosition = playerStart;

        // Spawn order: the posts first, then the robots the wave table counts, then the family.
        // Each kind's own spawn is its registry row, so a new kind needs no edit here (notes §119).
        foreach (RobotKindInfo robot in RobotKinds.All)
        {
            robot.Spawn?.Invoke(this, playerStart);
        }

        // The human family spawns last (ROM HUMSTV — kids first, then moms, then dads,
        // random field positions, staggered starts).
        SpawnHumans();
    }

    public LevelParameters Parameters { get; }

    public IPlayerInputSource Input { get; }

    public PlayfieldWall Wall { get; }

    public Player Player { get; }

    public LaserSlots PlayerLasers { get; }

    public ScoreBoard Score { get; }

    /// <summary>
    /// Hands the live counters back to the player's session slot. The ROM keeps
    /// the score, the men and SAVCNT in the player's own data block and the HUD
    /// reads them from there every time it draws, so the port must copy them
    /// across every TICK, not only at a wave clear or a death — otherwise the
    /// displayed score (and the spare-men icons) lag behind the field, which is
    /// what the rescue bonus looked like in the attract demo (notes §97).
    /// </summary>
    public void SyncInto(PlayerSlot slot)
    {
        slot.Score = Score.Score;
        slot.Lives = Player.Lives;
        slot.Rescues = RescuesThisLife;
    }

    /// <summary>
    /// True during the player's 2-second start grace period AND during the
    /// player's death animation (spec: "ALL ROBOTS ARE IMMOBILE" in both
    /// cases) — robots still tick their death timers, they just don't move,
    /// and remain killable by lasers.
    /// </summary>
    public bool RobotsFrozen => Player.IsInStartGracePeriod || Player.LifeState == EntityLifeState.Dying;

    /// <summary>
    /// The player's position delta over the last tick (plain integer
    /// difference — one tick is the unit of time). Set in Update, before the
    /// player moves, so it reflects last frame's motion.
    /// </summary>
    public IntVector2 PlayerVelocityEstimate { get; private set; }

    /// <summary>Spec: the total of SPARKS on screen must not exceed 20.</summary>
    public int ActiveSparkCount => _sparks.Count(s => s.LifeState == EntityLifeState.Alive);

    // Arcade caps (notes §11): ENFCNT < 8 spheroid-enforcers, TNKCNT < 20 tanks,
    // and the 20-shells-per-wave counter (fizzle bug: only a laser KILL decrements
    // it — a fizzled/expired shell never does, so late in the wave tanks stop firing).
    public bool CanDropEnforcer => _enforcers.Count(e => e.LifeState != EntityLifeState.Dead) < 8;

    public bool CanDropTank => _tanks.Count(t => t.LifeState != EntityLifeState.Dead) < 20;

    public bool CanFireCruiseMissile => _missiles.Count(m => m.LifeState != EntityLifeState.Dead) < GameplayConstants.CruiseMissileMax;

    private int _shellsFiredThisWave;

    public bool CanFireShell => _shellsFiredThisWave < 20;

    // Read-only counts for HUD/testing (live = not yet pruned).
    public int ElectrodeCount => _electrodes.Count(e => e.LifeState != EntityLifeState.Dead);

    public int GruntCount => _grunts.Count(e => e.LifeState != EntityLifeState.Dead);

    public int HulkCount => _hulks.Count(e => e.LifeState != EntityLifeState.Dead);

    public int SpheroidCount => _spheroids.Count(e => e.LifeState != EntityLifeState.Dead);

    public int EnforcerCount => _enforcers.Count(e => e.LifeState != EntityLifeState.Dead);

    public int QuarkCount => _quarks.Count(e => e.LifeState != EntityLifeState.Dead);

    public int TankCount => _tanks.Count(e => e.LifeState != EntityLifeState.Dead);

    public int BrainCount => _brains.Count(e => e.LifeState != EntityLifeState.Dead);

    public int ProgCount => _progs.Count(e => e.LifeState != EntityLifeState.Dead);

    public int MissileCount => _missiles.Count(e => e.LifeState != EntityLifeState.Dead);

    /// <summary>
    /// Wave-clear condition: every grunt, spheroid, enforcer,
    /// quark, tank, brain, and prog is gone, and no cruise missile is in
    /// flight (missiles never expire — they bounce until shot). Deliberately
    /// EXCLUDES hulks (indestructible — a level can never require killing
    /// one) and electrodes (static obstacles, not "enemies" in the clear
    /// sense).
    /// </summary>
    public bool IsLevelCleared =>
        GruntCount == 0 && SpheroidCount == 0 && EnforcerCount == 0 && QuarkCount == 0 && TankCount == 0
        && BrainCount == 0 && ProgCount == 0 && MissileCount == 0;

    /// <summary>True while the hit-stop freeze-frame is running.</summary>
    public bool IsFrozen => _hitStopTicksRemaining > 0;

    /// <summary>
    /// Humans rescued (player touch) during this player's life. ROM
    /// SAVCNT — reset on player death (PLINIT), carried across waves; each
    /// rescue pays ScoreValues.RescueBonus(count) (1000-5000, capped).
    /// </summary>
    public int RescuesThisLife { get; private set; }

    // ---- Spawn hooks called by entities during their own Update ----

    public void SpawnEnforcer(IntVector2 position) => _enforcers.Add(new Enforcer(Sprites, position, _random, Parameters.EnforcerFireDelay));

    public Tank SpawnTank(IntVector2 position)
    {
        // Keep the birth inside the playfield (the quark can be hugging a wall).
        Rectangle bounds = Wall.PlayfieldBounds;
        position = new IntVector2(
            Math.Clamp(position.X, bounds.X, bounds.Right - Tank.CollisionWidth),
            Math.Clamp(position.Y, bounds.Y, bounds.Bottom - Tank.CollisionHeight));
        Tank tank = new(Sprites, position, _random, Parameters.TankFireDelay);
        _tanks.Add(tank);
        return tank;
    }

    public void SpawnSpark(IntVector2 origin, IntVector2 playerPosition) =>
        // Wall bounds are passed through for the ROM's left-wall jitter rule
        // (RRC11.ASM ENFSHT: no X jitter within 16 columns of the wall).
        _sparks.Add(new Spark(Sprites, origin, playerPosition, _random, Wall.PlayfieldBounds));

    public void SpawnTankShell(IntVector2 origin, IntVector2 towardPlayerDirection)
    {
        _shellsFiredThisWave++; // ROM INC on fire; only a laser kill decrements (fizzle bug)
        _tankShells.Add(new TankShell(Sprites, origin, towardPlayerDirection, _random));
        // R5 $4F8C: shell creation requests the fire sound ($4B11, p200).
        Sound.Play(SoundTables.ShellFire);
    }

    public void SpawnCruiseMissile(IntVector2 origin) => _missiles.Add(new CruiseMissile(Sprites, origin, Player.Position, _random));

    /// <summary>ROM BMUT: a brain's touch turns the human into a PROG at its spot.</summary>
    public void SpawnProg(IntVector2 position, HumanKind kind) => _progs.Add(new Prog(Sprites, position, kind, _random));

    /// <summary>
    /// ROM BRNL1's catch reach: the brain and human TOP-LEFT CORNERS must be
    /// within ±3 arcade px on both axes for a reprogramming to start.
    /// </summary>
    private static readonly int BrainCatchReach = ScreenSize.Scaled(3);

    /// <summary>
    /// The nearest living human's position to <paramref name="from"/>
    /// (ROM GETHTG), or null when the family is gone (brains fall back to the
    /// player). GETHTG measures |dx| + |dy| — MANHATTAN, not Euclidean (the
    /// source sums the two absolute differences before comparing) — and from
    /// the BRAIN, not from the player; both matter to which human a brain
    /// picks when two are in different directions.
    /// </summary>
    public IntVector2? NearestHumanPositionTo(IntVector2 from)
    {
        Human? nearest = null;
        int nearestDistance = int.MaxValue;
        foreach (Human human in _humans)
        {
            // A human mid-reprogram is off the human list in the ROM (it has
            // been moved to the object list), so it is no longer a target.
            if (human.LifeState != EntityLifeState.Alive || human.IsBeingReprogrammed)
            {
                continue;
            }

            int distance = Math.Abs(human.Position.X - from.X) + Math.Abs(human.Position.Y - from.Y);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = human;
            }
        }

        return nearest?.Position;
    }

    /// <summary>
    /// The nearest ALIVE robot's position to <paramref name="from"/> for the
    /// attract demo (notes §94), measured the same Manhattan way as
    /// <see cref="NearestHumanPositionTo"/>, or null when the field is clear.
    /// Every robot kind counts — a brain falls back to hunting the player once
    /// the family is gone (GETHTG), so none are safe to ignore. Used only by
    /// the attract demo's phony player; the ROM's own AI lives in the OS ROM.
    /// </summary>
    public IntVector2? NearestLivingRobotPositionTo(IntVector2 from)
    {
        IntVector2? nearest = null;
        int nearestDistance = int.MaxValue;

        void Consider(IEntity entity)
        {
            if (entity.LifeState != EntityLifeState.Alive)
            {
                return;
            }

            int distance = Math.Abs(entity.Position.X - from.X) + Math.Abs(entity.Position.Y - from.Y);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = entity.Position;
            }
        }

        foreach (Grunt g in _grunts) Consider(g);
        foreach (Hulk h in _hulks) Consider(h);
        foreach (Spheroid s in _spheroids) Consider(s);
        foreach (Enforcer e in _enforcers) Consider(e);
        foreach (Quark q in _quarks) Consider(q);
        foreach (Tank t in _tanks) Consider(t);
        foreach (Brain b in _brains) Consider(b);
        foreach (Prog p in _progs) Consider(p);
        foreach (CruiseMissile m in _missiles) Consider(m);

        return nearest;
    }

    // ---- Per-tick update ----

    public void Update(GameTime gameTime)
    {
        // Hit-stop: everything else pauses this tick; Draw still
        // runs against the frozen state (brief freeze-frame on player death).
        if (_hitStopTicksRemaining > 0)
        {
            _hitStopTicksRemaining--;
            return;
        }

        UpdateGruntSpeedProgress();

        AdvanceMaterialisation();

        // Laser-vs-wall flares tick DOWN here, at the top: a flare spawned by a
        // laser later in this same tick must still get its full two frames.
        for (int i = _laserWallFlares.Count - 1; i >= 0; i--)
        {
            LaserWallFlare flare = _laserWallFlares[i];
            flare.FifthsRemaining -= ArcadeClock.UnitsPerPortTick;
            if (flare.FifthsRemaining <= 0)
            {
                _laserWallFlares.RemoveAt(i);
            }
        }

        Wall.Update(gameTime);

        // Player velocity estimate from last tick's motion — computed BEFORE
        // the player moves this frame.
        PlayerVelocityEstimate = Player.Position - _previousPlayerPosition;
        _previousPlayerPosition = Player.Position;

        Player.Update(gameTime, this);
        // R5 $273A: the fire path requests the laser sound ($26E6, p240).
        if (Player.LasersFiredThisUpdate)
        {
            Sound.Play(SoundTables.PlayerLaser);
        }
        PlayerLasers.Update(gameTime, this);

        UpdateEntities(gameTime);
        // R5 $4FCD: each wall bounce requests the bounce sound ($4B16, p200).
        foreach (TankShell shell in _tankShells)
        {
            if (shell.BouncedThisUpdate)
            {
                Sound.Play(SoundTables.ShellBounce);
            }
        }

        ResolveCollisions();

        // Prune: remove Dead entries from every list (pruning = count decrement, spec).
        foreach (IEntityList list in _updateOrder)
        {
            list.PruneDead();
        }
    }

    // ---- Collision resolution (order matters: a laser is consumed
    //      by the FIRST thing it hits, not multiple things in one frame) ----

    private void ResolveCollisions()
    {
        bool playerWasAlive = Player.LifeState == EntityLifeState.Alive;

        // 2-6. Player lasers, in the ROM's order (1 — lasers vs wall — is inside
        //      PlayerLaser.Update). A laser is consumed by the FIRST thing it hits.
        ResolveLaserCollisions();

        // 7. A grunt or a hulk walking onto an electrode.
        ResolveRobotVsElectrodeCollisions();

        // 8. The player vs an electrode.
        ResolvePlayerVsElectrodeCollision();

        // 9-10. The player vs everything that is fatal to touch (the grunts, hulks,
        //       brains and progs of phase 9 and the shots of phase 10).
        ResolvePlayerVsContactKills();

        // No "Player vs spheroid/enforcer/quark/tank" contact kill — the spec
        // never states contact with these robots kills the player (they harm
        // only via dropped units/missiles).

        // 11. Human collisions: hulk contact kills (the R5 ROM's only robot
        //     that checks the human list — RRH11 HULK COL0 on HPTR); player
        //     contact RESCUES (RRG23 COLCHK: the human path leaves PCFLG set
        //     for the human's kill vector → bonus, no skull, player unharmed).
        ResolveHumanCollisions();

        // The instant the player went Alive -> Dying this frame: hit-stop.
        if (playerWasAlive && Player.LifeState == EntityLifeState.Dying)
        {
            _hitStopTicksRemaining = GameplayConstants.HitStopTicks;
            // R5 $30EF (KILL_PLAYER): the death sound ($26D9, p238).
            Sound.Play(SoundTables.PlayerDeath);
        }
    }

    /// <summary>
    /// Phases 2-6: the player's lasers against every robot kind, in <see cref="RobotKinds.All"/>'s
    /// order — the order IS the behaviour, because a laser is spent on the first thing it meets and cannot hit
    /// two things in one frame.
    /// </summary>
    private void ResolveLaserCollisions()
    {
        foreach (RobotKindInfo robot in RobotKinds.All)
        {
            ResolveLaserPhase(robot);
        }
    }

    /// <summary>
    /// One kind's laser phase: the first laser touching one of them is spent on it, the kind's row says what
    /// happens to it, and the kill is scored (and may earn a spare man).
    /// </summary>
    /// <param name="robot">The kind's registry row.</param>
    private void ResolveLaserPhase(RobotKindInfo robot)
    {
        foreach (PlayerLaser laser in PlayerLasers.ActiveLasers)
        {
            foreach (IEntity target in ListOf(robot.Kind).Entities)
            {
                if (target.LifeState != EntityLifeState.Alive || !Touches(laser, target))
                {
                    continue;
                }

                robot.LaserHit(this, target, laser.Direction);
                AwardLaserScore(robot.Score);
                laser.Deactivate();
                break;
            }
        }
    }

    /// <summary>A laser kill's score, and the spare man it may earn (R5 $DBF9).</summary>
    /// <param name="value">What the kind is worth.</param>
    private void AwardLaserScore(int value)
    {
        if (Score.Add(value))
        {
            Player.AddLife();
            Sound.Play(SoundTables.BonusLife);
        }
    }

    /// <summary>
    /// The death of a robot whose picture shatters: the kill, the strip explosion (anchored at the picture's
    /// middle — the ROM's <c>NWCENT</c> path, notes §73) and the ROM's robot-death sound (R5 $1F5B). The kinds'
    /// rows call this one.
    /// </summary>
    /// <param name="target">The robot being killed.</param>
    /// <param name="direction">The laser's direction, which picks the explosion's axis and lean.</param>
    internal void Shatter(IEntity target, Direction8 direction)
    {
        ((IRemovable)target).Kill();
        if (target is IExplodable explodable)
        {
            SpawnExplosion(explodable, direction);
            Sound.Play(SoundTables.RobotDeath);
        }
    }

    /// <summary>The death of a robot that plays its OWN burst instead of the strip explosion (notes §64).</summary>
    /// <param name="target">The robot being killed.</param>
    /// <param name="burst">The burst its kind's row built from it.</param>
    internal void Burst(IEntity target, ScoreBurst burst)
    {
        ((IRemovable)target).Kill();
        SpawnScoreBurst(burst);
    }

    /// <summary>The wave's shell count, which only a LASER kill decrements (the fizzle bug, notes §53).</summary>
    internal void CountShellDestroyed() => _shellsFiredThisWave--;

    /// <summary>The list a robot kind lives in — the one mapping from the registry to the field's lists.</summary>
    /// <param name="kind">The kind to look up.</param>
    /// <exception cref="ArgumentOutOfRangeException">The kind has no list — a new kind needs one here.</exception>
    internal IEntityList ListOf(RobotKind kind) => kind switch
    {
        RobotKind.Electrode => _electrodes,
        RobotKind.Grunt => _grunts,
        RobotKind.Hulk => _hulks,
        RobotKind.Spheroid => _spheroids,
        RobotKind.Enforcer => _enforcers,
        RobotKind.Quark => _quarks,
        RobotKind.Tank => _tanks,
        RobotKind.Brain => _brains,
        RobotKind.Prog => _progs,
        RobotKind.Spark => _sparks,
        RobotKind.TankShell => _tankShells,
        RobotKind.CruiseMissile => _missiles,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No list holds this robot kind."),
    };

    /// <summary>Phase 7: a grunt or a hulk walking onto an electrode (RRP8 PSTKIL).</summary>
    private void ResolveRobotVsElectrodeCollisions()
    {
        foreach (Grunt grunt in _grunts)
        {
            if (grunt.LifeState != EntityLifeState.Alive)
            {
                continue;
            }

            foreach (Electrode electrode in _electrodes)
            {
                if (electrode.LifeState != EntityLifeState.Alive)
                {
                    continue;
                }

                if (Touches(grunt, electrode))
                {
                    grunt.Kill();     // "both the grunt and the electrode die"
                    SpawnExplosion(grunt, null); // non-directional shatter (notes 35)
                    // The post SHRIVELS and never bursts (RRP8 PSTKIL has no EXST).
                    electrode.Kill();
                    SpeedUpGrunts();
                    break;
                }
            }
        }

        foreach (Hulk hulk in _hulks)
        {
            foreach (Electrode electrode in _electrodes)
            {
                if (electrode.LifeState != EntityLifeState.Alive)
                {
                    continue;
                }

                if (Touches(hulk, electrode))
                {
                    electrode.Kill(); // the electrode is DESTROYED; the hulk is unaffected
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Phase 8: the player vs an electrode — "KILL THE PLAYER AND THE ELECTRODE".
    /// While invincible the player passes through unharmed.
    /// </summary>
    private void ResolvePlayerVsElectrodeCollision()
    {
        if (Player.LifeState != EntityLifeState.Alive || Player.IsInvincible)
        {
            return;
        }

        foreach (Electrode electrode in _electrodes)
        {
            if (electrode.LifeState != EntityLifeState.Alive)
            {
                continue;
            }

            if (Touches(Player, electrode))
            {
                Player.Kill();
                electrode.Kill();
                return;
            }
        }
    }

    /// <summary>
    /// Phases 9 and 10: the player vs every robot kind that is fatal to touch — the walkers of phase
    /// 9 and the shots of phase 10, in <see cref="RobotKinds.All"/>'s order. Only the player dies (a missile is
    /// not removed: only a laser removes those).
    /// </summary>
    private void ResolvePlayerVsContactKills()
    {
        if (Player.LifeState != EntityLifeState.Alive || Player.IsInvincible)
        {
            return;
        }

        foreach (RobotKindInfo robot in RobotKinds.All)
        {
            if (robot.KillsPlayerOnContact)
            {
                KillPlayerOnContact(ListOf(robot.Kind));
            }
        }
    }

    /// <summary>
    /// The player dies on contact with any of these — and only the player does. The state is
    /// re-checked before every target because an earlier target in the same pass may already have
    /// started the death (the ROM tests PCFLG per collision, and a second <c>Kill</c> would
    /// restart the death animation).
    /// </summary>
    /// <param name="robots">The kind's list.</param>
    private void KillPlayerOnContact(IEntityList robots)
    {
        foreach (IEntity entity in robots.Entities)
        {
            if (Player.LifeState != EntityLifeState.Alive)
            {
                return;
            }

            if (entity.LifeState == EntityLifeState.Alive && Touches(Player, entity))
            {
                Player.Kill();
                return;
            }
        }
    }

    /// <summary>
    /// Human collisions — see the call site in ResolveCollisions. The ROM's own four sub-phases, in
    /// order: the victim a dying brain lets go, a brain's catch, a hulk's kill, and the player's rescue.
    /// </summary>
    /// <remarks>
    /// A frozen game: the robots wait for STATUS, and the ROM only creates its collision process AFTER the
    /// wave-start appear (<c>JSR ROBON / MAKP LSPROC / MAKP COLCHK / CLR STATUS</c>), so no robot may touch a
    /// human while it is held. The family itself keeps walking (notes §88), which is why the gate exists.
    /// </remarks>
    private void ResolveHumanCollisions()
    {
        bool robotsHeld = RobotsFrozen;

        ReleaseVictimsOfDeadBrains();
        ResolveBrainCatches(robotsHeld);
        ResolveHulkVsHumanCollisions(robotsHeld);
        ResolvePlayerRescues();
    }

    /// <summary>A brain killed MID-reprogram releases its victim — the conversion never completes, no prog
    /// appears, and a skull is left where she stands.</summary>
    /// <remarks>ROM: <c>BRNKIL</c> checks its own address against <c>BMUT3</c> and, past it, frees the human
    /// and drops a SKULL.</remarks>
    private void ReleaseVictimsOfDeadBrains()
    {
        foreach (Brain brain in _brains)
        {
            if (brain.LifeState == EntityLifeState.Alive)
            {
                continue;
            }

            if (brain.ReleaseVictim() is { } released)
            {
                released.FinishReprogramming();
                _skulls.Add(new SkullMarker(Sprites, released.Position));
            }
        }
    }

    /// <summary>
    /// A brain that catches a human REPROGRAMS her: she goes off the human list and the pair runs the ROM's
    /// 20-iteration animation (the brain stops, the human flashes two-colour and jiggles), ending in a prog
    /// at the human's last position with no skull.
    /// </summary>
    /// <param name="robotsHeld">True while the wave-start appear holds the robots.</param>
    /// <remarks>The CATCH TEST is <c>BRNL1</c>'s tail comparing the two TOP-LEFT CORNERS, |dX| &lt;= 3 AND
    /// |dY| &lt;= 3 (<c>ADDB #3 / CMPB #$6 / BHI</c> then <c>ADDA #3 / CMPA #6 / BLS</c>) — not a picture
    /// overlap. A box test fires as soon as the 14x16 brain box touches anything, so brains would grab humans
    /// the source would not.</remarks>
    private void ResolveBrainCatches(bool robotsHeld)
    {
        Rectangle playfieldBounds = Wall.PlayfieldBounds;
        foreach (Brain brain in _brains)
        {
            if (robotsHeld)
            {
                break;
            }

            if (brain.LifeState != EntityLifeState.Alive || brain.IsReprogramming)
            {
                continue;
            }

            foreach (Human human in _humans)
            {
                if (!IsGraspable(human))
                {
                    continue;
                }

                if (Math.Abs(brain.Position.X - human.Position.X) > BrainCatchReach
                    || Math.Abs(brain.Position.Y - human.Position.Y) > BrainCatchReach)
                {
                    continue;
                }

                brain.BeginReprogramming(human, playfieldBounds);
                break;
            }
        }
    }

    /// <summary>A hulk walking onto a human kills her: instantly off (ROM: <c>DMAOFF</c>), leaving a skull.</summary>
    /// <param name="robotsHeld">True while the wave-start appear holds the robots.</param>
    /// <remarks>ROM: <c>RRH11</c>'s <c>HULK</c>, the only robot whose collision phase walks the human list.</remarks>
    private void ResolveHulkVsHumanCollisions(bool robotsHeld)
    {
        foreach (Human human in _humans)
        {
            if (!IsGraspable(human))
            {
                continue;
            }

            foreach (Hulk hulk in _hulks)
            {
                if (robotsHeld)
                {
                    break; // ROM: `HULK LDA STATUS WAIT FOR STATUS TO GO`
                }

                if (hulk.LifeState == EntityLifeState.Alive && Touches(hulk, human))
                {
                    human.Kill();
                    _skulls.Add(new SkullMarker(Sprites, human.Position));
                    break;
                }
            }
        }
    }

    /// <summary>
    /// The player touching a human RESCUES her: the bonus score, a running save count, and the score marker
    /// that prints it.
    /// </summary>
    /// <remarks>ROM: RRG23 <c>COLCHK</c>. The human path leaves <c>PCFLG</c> set for the human's kill vector,
    /// so the touch is a bonus and no skull — the player is unharmed.</remarks>
    private void ResolvePlayerRescues()
    {
        foreach (Human human in _humans)
        {
            if (!IsGraspable(human)
                || Player.LifeState != EntityLifeState.Alive
                || !Touches(Player, human))
            {
                continue;
            }

            human.Rescue();
            RescuesThisLife++;
            // ROM HUMKIL PCFLG path: 60-tick score display at the rescue
            // spot showing min(SAVCNT,5) thousand (SAVCNT itself uncapped).
            _rescueScores.Add(new RescueScoreMarker(Sprites, human.Position, RescuesThisLife));
            if (Score.Add(ScoreValues.RescueBonus(RescuesThisLife)))
            {
                Player.AddLife(); // extra-life thresholds apply to rescue score too (ROM SCORE routine)
            }
        }
    }

    /// <summary>True when a human is standing on the field and no brain has hold of her.</summary>
    /// <param name="human">The human to test.</param>
    private static bool IsGraspable(Human human) =>
        human.LifeState == EntityLifeState.Alive && !human.IsBeingReprogrammed;

    /// <summary>
    /// ROM grunt speedup (3A94-3A9F): `LDB #$E0 / MUL` → the delay × 224/256
    /// (TRUNCATED), and applied ONLY while the result is still ≥ the current
    /// $BE5D floor (which descends over the wave — see
    /// <see cref="UpdateGruntSpeedProgress"/>). Applied to every surviving grunt,
    /// whose in-flight countdown is deliberately NOT touched (notes §67).
    /// </summary>
    internal void SpeedUpGrunts()
    {
        int floor = _gruntSpeedFloor;
        foreach (Grunt grunt in _grunts)
        {
            if (grunt.LifeState == EntityLifeState.Alive)
            {
                grunt.SpeedUp(floor);
            }
        }
    }

    /// <summary>
    /// R5 $2AC7-2AF1 "update grunt speed as level progresses": every 225 vblanks
    /// (first tick 270) and ONLY while 30+ grunts are alive. The ROM's two
    /// immediate-operand constants are $FEFC and $FFFE — i.e. the floor ($BE5D)
    /// drops by 2 and the limit ($BE5C) by 4 on one pass, and by 1 and 2 on the
    /// next ($F0 toggles between them), then the limit is clamped to the floor.
    /// The floor reaches 1 = the arcade player's speed, so late in big waves the
    /// grunts are at least as fast as the player (notes §31).
    /// </summary>
    private void UpdateGruntSpeedProgress()
    {
        if (--_gruntProgressTimer > 0)
        {
            return;
        }

        _gruntProgressTimer = GameplayConstants.PortTicks(225);

        if (GruntCount < 30)
        {
            return;
        }

        // The $F0 toggle starts on the −2/−4 pass and alternates with −1/−2.
        _gruntSpeedFloor = Math.Max(1, _gruntSpeedFloor - _gruntSpeedFloorStep);
        foreach (Grunt grunt in _grunts)
        {
            if (grunt.LifeState == EntityLifeState.Alive)
            {
                grunt.WaveSpeedTick(_gruntSpeedFloor, 2 * _gruntSpeedFloorStep);
            }
        }

        _gruntSpeedFloorStep = _gruntSpeedFloorStep == 2 ? 1 : 2;
    }

    /// <summary>Current grunt-speed floor (tests; R5 $BE5D).</summary>
    internal int GruntSpeedFloor => _gruntSpeedFloor;

    /// <summary>
    /// The arcade's contact test between two entities (notes §118): their pictures' opaque pixels where
    /// the two are drawn, or their collision boxes when the field has no art to compare (a headless test)
    /// or one of them shows no picture of its own (the cruise missile).
    /// </summary>
    private bool Touches(IEntity a, IEntity b)
    {
        if (_pixelCollision is { } collision && collision.ShapeOf(a) is { } shapeA && collision.ShapeOf(b) is { } shapeB)
        {
            return collision.Overlaps(shapeA, shapeB);
        }

        return a.Bounds.Overlaps(b.Bounds);
    }

    // ---- Explosions (RRDX2/RRX7/RRHX4 — notes §35.5, §61) ----

    /// <summary>
    /// Spawns an explosion for a dying entity. The ROM's explosion and appear
    /// records share ONE pool of 10 <c>EX</c> blocks (RRDX2.ASM's `EX` struct,
    /// `RMB ((10-1)*EXSIZE)`); GETBLK/GETAP both take from the same free list, so
    /// a full list means no explosion at all (the caller's kill still stands).
    /// </summary>
    private void SpawnExplosion(IExplodable dead, Direction8? direction)
    {
        if (_explosions.Count >= GameplayConstants.StripMaxConcurrent)
        {
            return; // ROM: list full → no explosion
        }

        _explosions.Add(Explosion.StartExplosion(dead, direction, StripClipBounds));
    }

    /// <summary>
    /// Starts the bespoke death burst for a spheroid or a quark (notes §64). The
    /// ROM's kill processes take a free object slot rather than one of the ten
    /// shared strip records, so there is no cap — and the laser-kill sound is
    /// played here because the strip-explosion branch (which normally plays it)
    /// does not run for these two enemies.
    /// </summary>
    private void SpawnScoreBurst(ScoreBurst burst)
    {
        _scoreBursts.Add(burst);
        Sound.Play(SoundTables.RobotDeath);
    }

    /// <summary>
    /// The live 16-slot palette (null in unit tests that pass no palette).
    /// Entities use it for the ROM's direct PCRAM writes — the player death's
    /// slot-12 fade (notes §66) is the one caller.
    /// </summary>
    internal GamePalette? Palette => _wallPalette;

    /// <summary>Live spheroid/quark death bursts (test hook).</summary>
    internal IReadOnlyList<ScoreBurst> ScoreBursts => _scoreBursts;

    /// <summary>
    /// Paints the ROM's laser-vs-wall flare (RRG23 `LASDIE` → `LASDIH`/`LASDIV`,
    /// notes §63): where a laser runs off the playfield its end pixels take the
    /// wave's <see cref="GameplayConstants.LaserWallSlotForWave"/> colour for two
    /// frames (the ROM then repaints them in WALCOL, which is invisible because
    /// they are wall pixels anyway). Two bytes of video memory = 2 columns x 4
    /// rows of arcade pixels.
    /// The LEFT/RIGHT walls use `LASDIH` (a SOLID fill); the TOP/BOTTOM walls use
    /// `LASDIV`, which ANDs WALCOL's high nibble onto LASCOL's low nibble — a
    /// dither, so the wall shows through alternate rows.
    /// </summary>
    internal void SpawnLaserWallFlare(Rectangle laserBounds, Direction8 direction)
    {
        Rectangle inner = Wall.PlayfieldBounds;
        int thickness = ScreenSize.Scaled(4); // 2 arcade columns (or rows) of flare
        int length = ScreenSize.Scaled(4);    // 4 arcade rows (or columns)

        // Which wall did it cross? A laser whose bounds are outside the TOP/BOTTOM
        // edges died against a horizontal wall (the ROM's LASDIV, dithered); one
        // outside the LEFT/RIGHT edges died against a vertical wall (LASDIH).
        bool horizontalWall = laserBounds.Top < inner.Top || laserBounds.Bottom > inner.Bottom;
        bool verticalWall = laserBounds.Left < inner.Left || laserBounds.Right > inner.Right;
        if (!horizontalWall && !verticalWall)
        {
            // Defensive: only called when the wall really was hit. Fall back to the
            // travel axis so a caller mistake cannot paint a flare in mid-air.
            horizontalWall = direction is Direction8.Up or Direction8.Down;
            verticalWall = !horizontalWall;
        }

        Rectangle bounds;
        if (horizontalWall)
        {
            int y = laserBounds.Top < inner.Top
                ? Wall.OuterBounds.Y
                : Wall.OuterBounds.Bottom - thickness;
            int x = Math.Clamp(laserBounds.Center.X - thickness / 2, inner.Left, inner.Right - thickness);
            bounds = new Rectangle(x, y, thickness, length);
        }
        else
        {
            int x = laserBounds.Left < inner.Left
                ? Wall.OuterBounds.X
                : Wall.OuterBounds.Right - thickness;
            int y = Math.Clamp(laserBounds.Center.Y - thickness / 2, inner.Top, inner.Bottom - length);
            bounds = new Rectangle(x, y, thickness, length);
        }

        _laserWallFlares.Add(new LaserWallFlare(bounds, dithered: horizontalWall));
    }

    /// <summary>
    /// One laser-vs-wall flare record. The ROM's flare lives 2 frames in LASCOL
    /// (`NAP 2,LDH1`), so 2 port ticks at the 6/5 tick ratio.
    /// </summary>
    internal sealed class LaserWallFlare(Rectangle bounds, bool dithered)
    {
        public Rectangle Bounds { get; } = bounds;

        /// <summary>True for `LASDIV` (a top/bottom wall): LASCOL dithered with WALCOL.</summary>
        public bool Dithered { get; } = dithered;

        /// <summary>How long the flare lasts: <c>NAP 2</c>, two ROM frames, counted in the port's clock units (notes §52).</summary>
        public int FifthsRemaining { get; set; } = 2 * ArcadeClock.UnitsPerRomFrame;
    }

    /// <summary>Live laser-vs-wall flares (test hook — the ROM's LASCOL pixels).</summary>
    internal IReadOnlyList<LaserWallFlare> LaserWallFlares => _laserWallFlares;

    /// <summary>Number of live laser-vs-wall flares (test hook).</summary>
    internal int LaserWallFlareCount => _laserWallFlares.Count;

    /// <summary>The strip engine's clip rectangle, in art pixels (X) and rows (Y).</summary>
    private StripClip StripClipBounds
    {
        get
        {
            Rectangle bounds = Wall.PlayfieldBounds;
            return new StripClip(
                bounds.Left / ScreenSize.SpecScale,
                bounds.Right / ScreenSize.SpecScale,
                bounds.Top / ScreenSize.SpecScale,
                bounds.Bottom / ScreenSize.SpecScale);
        }
    }

    // ---- Draw order (merged from the spec's two lists — a single
    //      ordered pass satisfies both) ----

    public void Draw(SpriteBatch spriteBatch)
    {
        // 1. Wall — arcade-faithful: the ROM's per-wave WALL colour slot (RRG23
        //    `GTWCOL` -> `WALCOL`, solid fill); the placeholder WallColorCycle
        //    has no palette to read, and is only used when no live palette is
        //    wired in (unit tests).
        Wall.Draw(spriteBatch, Sprites.WallPixel,
            _wallPalette is { } p ? p.Color(GameplayConstants.WallSlotForWave(Parameters.LevelNumber)) : null);

        // 1b. Laser-vs-wall flares (RRG23 LASDIE): painted OVER the wall, in the
        //     wave's LASCOL slot, exactly as the ROM writes those pixels.
        if (_laserWallFlares.Count > 0)
        {
            Color flareColor = Sprites.SlotColor(GameplayConstants.LaserWallSlotForWave(Parameters.LevelNumber));
            int arcadeRow = ScreenSize.Scaled(2); // one arcade pixel row = 2 port px
            foreach (LaserWallFlare flare in _laserWallFlares)
            {
                if (flare.Dithered)
                {
                    // LASDIV: the ROM's mixed nibble — one row in LASCOL, the
                    // next left as WALCOL. Draw only the LASCOL rows.
                    for (int y = flare.Bounds.Y; y < flare.Bounds.Bottom; y += arcadeRow * 2)
                    {
                        Sprites.DrawSolidRectangle(
                            spriteBatch,
                            new Rectangle(flare.Bounds.X, y, flare.Bounds.Width, arcadeRow),
                            flareColor);
                    }
                }
                else
                {
                    Sprites.DrawSolidRectangle(spriteBatch, flare.Bounds, flareColor);
                }
            }
        }

        // 2-4. The posts, the family and their markers, then the robots — one loop over the field's draw order.
        foreach (IEntityList list in _drawOrderBehindShots)
        {
            list.DrawAll(spriteBatch, this);
        }

        // 5. Player lasers.
        foreach (PlayerLaser? laser in PlayerLasers.Slots)
        {
            laser?.Draw(spriteBatch);
        }

        // 6-7b. The enemy shots, then the explosions and the bursts — over the shots, under the player.
        foreach (IEntityList list in _drawOrderInFrontOfShots)
        {
            list.DrawAll(spriteBatch, this);
        }

        // 8. Player — ALWAYS last (spec states this explicitly twice).
        Player.Draw(spriteBatch);
    }

    // ---- Start-of-level spawning ----

    internal void SpawnElectrodes(IntVector2 playerStart)
    {
        for (int i = 0; i < Parameters.ElectrodeCount; i++)
        {
            // No overlap with other electrodes; not too close to the player start.
            IntVector2 position = FindSpawnPoint(
                static _ => default,
                rect => new IntVector2(rect.X, rect.Y).IsFartherThan(playerStart, ScreenSize.Scaled(GameplayConstants.ElectrodeMinDistanceFromPlayer))
                         && _electrodes.All(e => !e.Bounds.Overlaps(rect)));
            _electrodes.Add(new Electrode(Sprites, position, Parameters.LevelNumber));
        }
    }

    internal void SpawnGrunts(IntVector2 playerStart)
    {
        for (int i = 0; i < Parameters.GruntCount; i++)
        {
            // Cannot overlap electrodes or the wall; >= 20 spec-px from the player start (spec-stated).
            // Grunts MAY overlap each other — no check for that.
            IntVector2 position = FindSpawnPoint(
                static _ => default,
                rect => new IntVector2(rect.X, rect.Y).IsFartherThan(playerStart, ScreenSize.Scaled(GameplayConstants.GruntMinDistanceFromPlayer))
                         && _electrodes.All(e => !e.Bounds.Overlaps(rect)));
            // ROM: stagger — step countdown re-rolled RND(1..ROBSPD) bodies
            // every 4-vblank body; survivors' limit drops ×7/8 (floored at
            // RMXSPD) each time a grunt dies (notes §29).
            var grunt = new Grunt(Sprites, position, Parameters.GruntMoveDelay, random: _random);
            _grunts.Add(grunt);
            QueueMaterialise(grunt);
        }
    }

    internal void SpawnHulks(IntVector2 playerStart)
    {
        for (int i = 0; i < Parameters.HulkCount; i++)
        {
            // May overlap electrodes/other robots; just not the wall or too close to the player
            // (spec: "30,40 pixels away minimum" — 35 spec-px midpoint, tunable).
            IntVector2 position = FindSpawnPoint(
                static _ => default,
                rect => new IntVector2(rect.X, rect.Y).IsFartherThan(playerStart, ScreenSize.Scaled(GameplayConstants.HulkMinDistanceFromPlayer)));
            // ROM RRH11: step period from the wave table (HLKSPD).
            //
            // ROM (R5 $017C HULK_INITIALISE) target roll, per hulk at spawn:
            //   50% -> "stalk a family member": scan the family list (round
            //          robin cursor $49). BEGIN_WAVE spawns hulks BEFORE the
            //          family ($2831 vs $283A), so the list is still empty and
            //          the scan returns NULL. (R5 then does LDY ,0 -> Y=$7E01
            //          and chases a phantom object in ROM code — a documented
            //          bug that leaves those hulks wandering into corners. We
            //          take the intended NULL -> player fallback instead.)
            //   50% -> target = the LAST family-list slot, which HUMSTV fills
            //          with the last-spawned member (mikeys, then moms, then
            //          dads — so usually the last dad).
            // A stalked member that dies or is rescued clears its slot (NULL)
            // and the hulk falls back to the player (R5 $010D/$0113).
            Func<IntVector2> target =
                _random.Next(2) == 0 ? LastHumanOrPlayer : () => Player.Position;
            var hulk = new Hulk(Sprites, position, _random, Parameters.HulkSpeed, target);
            _hulks.Add(hulk);
            QueueMaterialise(hulk);
        }
    }

    /// <summary>
    /// ROM HULK "last slot" target: the last-spawned family member (HUMSTV
    /// order: mikeys, then moms, then dads — so <c>_humans[^1]</c>), or the
    /// player once that member is gone (NULL target -> player, R5 $0113).
    /// Humans spawn after hulks, so this resolves lazily at re-aim time.
    /// </summary>
    private Func<IntVector2> LastHumanOrPlayer => () =>
        _humans.Count > 0 && _humans.Last.LifeState == EntityLifeState.Alive
            ? _humans.Last.Position
            : Player.Position;

    internal void SpawnSpheroids(IntVector2 playerStart)
    {
        for (int i = 0; i < Parameters.SpheroidCount; i++)
        {
            // May overlap everything; just not the wall or within 100 spec-px of the player start.
            // Bias: spheroids "do like to start near walls" — 70% of the time a candidate
            // within Scaled(30) of one of the four inner edges.
            IntVector2 position = FindSpawnPoint(
                _ => RandomSpheroidCandidate(),
                rect => new IntVector2(rect.X, rect.Y).IsFartherThan(playerStart, ScreenSize.Scaled(GameplayConstants.SpheroidMinDistanceFromPlayer)));
            // ROM (notes §11.2): the spheroid rolls its own drop count from ENFNUM
            // (MaxDropsX2) at spawn; CDPTIM sets its drop tempo.
            var spheroid = new Spheroid(Sprites, position, _random, Parameters.MaxDropsX2, Parameters.SpheroidDropDelay);
            _spheroids.Add(spheroid);
            QueueMaterialise(spheroid);
        }
    }

    internal void SpawnQuarks(IntVector2 playerStart)
    {
        Rectangle bounds = Wall.PlayfieldBounds;
        for (int i = 0; i < Parameters.QuarkCount; i++)
        {
            // ROM $4B48-4B5A: X uniform across the field; Y = 26 (top) or 220 (bottom)
            // arcade px with a coin flip — quarks spawn ON the top or bottom wall.
            int x = _random.Next(bounds.X, bounds.Right - ScreenSize.Scaled(GameplayConstants.QuarkCollisionSize.Width) + 1);
            bool top = _random.Next(2) == 0;
            IntVector2 position = new(
                x,
                top ? bounds.Y : bounds.Bottom - ScreenSize.Scaled(GameplayConstants.QuarkCollisionSize.Height));
            // ROM: same ENFNUM roll as the spheroid; TDPTIM sets the tank-drop tempo.
            var quark = new Quark(Sprites, position, _random, Parameters.MaxDropsX2, Parameters.QuarkDropDelay, Parameters.QuarkMove);
            _quarks.Add(quark);
            QueueMaterialise(quark);
        }
    }

    /// <summary>
    /// Picks a candidate point (fully contained in the play area — that IS the
    /// "no wall overlap" check), up to <see cref="GameplayConstants.SpawnPlacementMaxAttempts"/>
    /// attempts; <paramref name="candidateOverride"/> = <see cref="IntVector2.Zero"/>
    /// means a uniform random point. Falls back to a grid scan, then gives up
    /// (degenerate case that can't happen at real field sizes).
    /// </summary>
    private IntVector2 FindSpawnPoint(Func<int, IntVector2> candidateOverride, Func<Rectangle, bool> isAcceptable)
        => FindSpawnPoint(candidateOverride, isAcceptable, EntitySize);

    /// <summary>
    /// A random spot whose <paramref name="size"/>-square box passes <paramref name="isAcceptable"/>.
    ///
    /// The LAST-RESORT scan is a FINE grid and, like the random attempts, it only ever returns a
    /// spot the predicate accepts — an unchecked fallback could hand back a point inside an
    /// electrode, which is what put humans on top of one every so often (notes §88).
    /// </summary>
    private IntVector2 FindSpawnPoint(Func<int, IntVector2> candidateOverride, Func<Rectangle, bool> isAcceptable, int size)
    {
        for (int attempt = 0; attempt < GameplayConstants.SpawnPlacementMaxAttempts; attempt++)
        {
            IntVector2 candidate = candidateOverride(attempt);
            if (candidate == IntVector2.Zero)
            {
                candidate = RandomPointInside(size);
            }

            if (isAcceptable(new Rectangle(candidate.X, candidate.Y, size, size)))
            {
                return candidate;
            }
        }

        // Last-resort grid scan for any valid spot, one pixel at a time so a gap between two
        // obstacles cannot be stepped over.
        Rectangle inner = Wall.PlayfieldBounds;
        for (int y = inner.Y; y + size <= inner.Bottom; y += GridScanStep)
        {
            for (int x = inner.X; x + size <= inner.Right; x += GridScanStep)
            {
                if (isAcceptable(new Rectangle(x, y, size, size)))
                {
                    return new IntVector2(x, y);
                }
            }
        }

        // Nothing fits (a degenerate field): keep the caller's own guarantee rather than
        // silently breaking it — a predicate failure here must still return SOMETHING.
        return RandomPointInside(size);
    }

    /// <summary>Step of the last-resort placement scan — fine enough to find a gap between obstacles.</summary>
    private const int GridScanStep = 4;

    private IntVector2 RandomPointInside() => RandomPointInside(EntitySize);

    private IntVector2 RandomPointInside(int size)
    {
        Rectangle inner = Wall.PlayfieldBounds;
        return new IntVector2(
            _random.Next(inner.X, inner.Right - size),
            _random.Next(inner.Y, inner.Bottom - size));
    }

    internal void SpawnBrains(IntVector2 playerStart)
    {
        // Brains spawn with the wave (ROM $1AC0), like the hulks —
        // anywhere in the field, not the wall and not on top of the player.
        for (int i = 0; i < Parameters.BrainCount; i++)
        {
            IntVector2 position = FindSpawnPoint(
                static _ => default,
                rect => new IntVector2(rect.X, rect.Y).IsFartherThan(playerStart, ScreenSize.Scaled(GameplayConstants.HulkMinDistanceFromPlayer)));
            var brain = new Brain(Sprites, position, _random, Parameters.BrainSpeed, Parameters.BrainFireDelay);
            _brains.Add(brain);
            QueueMaterialise(brain);
            // R5 $4607 (PLAY_BRAIN_WAVE_WARP_IN_SOUNDS): $4143, priority 255.
            Sound.Play(SoundTables.BrainWarpIn);
        }
    }

    // ---- Human spawning (ROM HUMSTV: kids, moms, dads; plain RANDXY —
    //      no overlap or spacing constraints in the ROM) ----

    private void SpawnHumans()
    {
        SpawnHumanKind(HumanKind.Mikey, Parameters.MikeyCount);
        SpawnHumanKind(HumanKind.Mom, Parameters.MomCount);
        SpawnHumanKind(HumanKind.Dad, Parameters.DadCount);
    }

    private void SpawnHumanKind(HumanKind kind, int count)
    {
        for (int i = 0; i < count; i++)
        {
            // A human refuses to step into a live electrode (Human.Update mirrors the
            // ROM's walk), so one scattered ON TOP of an electrode would be stuck there for
            // the rest of the wave. Keep humans off electrodes
            // the same way the electrodes and grunts are placed (notes §77), and clear
            // the member's OWN box, not the generic entity square (notes §88).
            IntVector2 position = FindSpawnPoint(
                static _ => default,
                rect => _electrodes.All(e => !e.Bounds.Overlaps(rect)),
                Human.SpawnSquarePortPixels(kind));
            _humans.Add(new Human(Sprites, position, kind, _random));
        }
    }

    /// <summary>70% of the time: a point within SpheroidNearWallBiasDistance of a random inner edge; else uniform.</summary>
    private IntVector2 RandomSpheroidCandidate()
    {
        Rectangle inner = Wall.PlayfieldBounds;
        if (_random.Next(100) >= GameplayConstants.SpheroidNearWallBiasPercent)
        {
            return RandomPointInside();
        }

        int bias = ScreenSize.Scaled(GameplayConstants.SpheroidNearWallBiasDistance);
        int x = _random.Next(inner.X, inner.Right - EntitySize);
        int y = _random.Next(inner.Y, inner.Bottom - EntitySize);
        switch (_random.Next(4))
        {
            case 0:
                y = _random.Next(inner.Y, inner.Y + bias); break; // top edge
            case 1:
                y = _random.Next(inner.Bottom - bias - EntitySize, inner.Bottom - EntitySize); break; // bottom edge
            case 2:
                x = _random.Next(inner.X, inner.X + bias); break; // left edge
            default:
                x = _random.Next(inner.Right - bias - EntitySize, inner.Right - EntitySize); break; // right edge
        }

        return new IntVector2(x, y);
    }

    /// <summary>Advances every list the field holds, in the ROM's own update order.</summary>
    private void UpdateEntities(GameTime gameTime)
    {
        foreach (IEntityList list in _updateOrder)
        {
            list.UpdateAll(gameTime, this);
        }
    }

    /// <summary>
    /// Advances ONE entity, unless it is still assembling — the ROM holds the robots OFF through the appear
    /// sequence, and that guard lives here rather than in every kind's list (notes §62).
    /// </summary>
    internal void UpdateEntity(IEntity entity, GameTime gameTime)
    {
        if (entity.LifeState != EntityLifeState.Dead && !IsMaterialising(entity))
        {
            entity.Update(gameTime, this);
        }
    }

    /// <summary>
    /// RRG23's `APPEAR`, one frame at a time: create ONE appear record for the
    /// next queued robot (its strips converge onto the robot's centre — the ROM's
    /// `APCENT`), then retire the robots whose record has finished converging.
    /// The record pool is the shared ten, so a full pool simply delays the next one.
    /// </summary>
    private void AdvanceMaterialisation()
    {
        if (_pendingAppear.Count > 0 && _explosions.Count < GameplayConstants.StripMaxConcurrent)
        {
            IEntity robot = _pendingAppear.Dequeue();

            // `ANDA #3 / CMPA #3`: every FOURTH robot of the sequence uses the
            // horizontal (column) fan, the rest the row fan. The ROM does not set A
            // before the APST call in this loop, so the row fan's slope is taken as
            // 0 (no lean) rather than guessed.
            StripFanAxis axis = (_appearSequence & 3) == 3 ? StripFanAxis.Columns : StripFanAxis.Rows;
            _appearSequence++;

            if (robot is IAnimationFrameSource frameSource)
            {
                Rectangle bounds = robot.Bounds;
                Explosion appear = Explosion.StartAppear(frameSource, bounds, axis, slope: 0, StripClipBounds);
                _explosions.Add(appear);
                _assembling[robot] = appear;
            }
        }

        if (_assembling.Count > 0)
        {
            List<IEntity>? done = null;
            foreach (KeyValuePair<IEntity, Explosion?> pair in _assembling)
            {
                if (pair.Value is { LifeState: not EntityLifeState.Alive })
                {
                    (done ??= new List<IEntity>()).Add(pair.Key);
                }
            }

            if (done is not null)
            {
                foreach (IEntity robot in done)
                {
                    _assembling.Remove(robot);
                }
            }
        }
    }

    /// <summary>
    /// Queues a wave-start robot for the APPEAR materialisation (RRG23). It counts
    /// as assembling from THIS moment — the ROM holds the robots OFF for the whole
    /// sequence, not just once their own record starts.
    /// </summary>
    private void QueueMaterialise(IEntity robot)
    {
        _pendingAppear.Enqueue(robot);
        _assembling[robot] = null;
    }

    /// <summary>
    /// True while this entity is still assembling at a wave start: it does not
    /// act and is NOT drawn — its appear records are drawing it (the ROM holds the
    /// robots OFF through the appear sequence).
    /// </summary>
    internal bool IsMaterialising(IEntity entity) => _assembling.ContainsKey(entity);

    /// <summary>Robots still waiting for their appear record (tests).</summary>
    internal int PendingAppearCount => _pendingAppear.Count;

    /// <summary>Draws an entity unless it is still materialising.</summary>
    /// <param name="entity">The entity to draw.</param>
    /// <param name="spriteBatch">The batch to draw into.</param>
    internal void DrawEntity(IEntity entity, SpriteBatch spriteBatch)
    {
        if (!IsMaterialising(entity))
        {
            entity.Draw(spriteBatch);
        }
    }

    // Test-only hooks (InternalsVisibleTo the test assembly).
    internal void AddElectrode(Electrode electrode) => _electrodes.Add(electrode);

    internal void AddGrunt(Grunt grunt) => _grunts.Add(grunt);

    internal void AddHulk(Hulk hulk) => _hulks.Add(hulk);

    internal void AddTankShell(TankShell shell) => _tankShells.Add(shell);

    internal IReadOnlyList<Electrode> Electrodes => _electrodes;

    internal IReadOnlyList<Grunt> Grunts => _grunts;

    internal IReadOnlyList<Hulk> Hulks => _hulks;

    internal IReadOnlyList<TankShell> TankShells => _tankShells;

    internal IReadOnlyList<Spheroid> Spheroids => _spheroids;

    internal IReadOnlyList<Quark> Quarks => _quarks;
    internal IReadOnlyList<Tank> Tanks => _tanks;

    internal IReadOnlyList<Human> Humans => _humans;

    internal IReadOnlyList<SkullMarker> Skulls => _skulls;

    internal IReadOnlyList<RescueScoreMarker> RescueScores => _rescueScores;

    internal IReadOnlyList<Brain> Brains => _brains;

    internal IReadOnlyList<Prog> Progs => _progs;

    internal IReadOnlyList<CruiseMissile> CruiseMissiles => _missiles;

    internal void AddHuman(Human human) => _humans.Add(human);
    internal void AddQuark(Quark quark) => _quarks.Add(quark);

    internal void AddBrain(Brain brain) => _brains.Add(brain);

    internal void AddProg(Prog prog) => _progs.Add(prog);

    internal void AddCruiseMissile(CruiseMissile missile) => _missiles.Add(missile);
    internal IReadOnlyList<Enforcer> Enforcers => _enforcers;
    internal IReadOnlyList<Spark> Sparks => _sparks;
    internal IReadOnlyList<Explosion> Explosions => _explosions;

    /// <summary>Every list the field advances and prunes, in order.</summary>
    internal IReadOnlyList<IEntityList> UpdateOrder => _updateOrder;

    /// <summary>The lists drawn behind the player's lasers, in order.</summary>
    internal IReadOnlyList<IEntityList> DrawOrderBehindShots => _drawOrderBehindShots;

    /// <summary>The lists drawn in front of the player's lasers, in order.</summary>
    internal IReadOnlyList<IEntityList> DrawOrderInFrontOfShots => _drawOrderInFrontOfShots;
}
