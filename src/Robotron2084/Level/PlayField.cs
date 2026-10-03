using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Graphics;
using Robotron2084.Input;
using Robotron2084.Level.Collisions;
using Robotron2084.Level.Spawning;
using Robotron2084.Palette;
using Robotron2084.Persistence;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>The playfield for one wave: the player, the robots and the family, moved and drawn once a tick in the arcade's order.</summary>
/// <remarks>
/// The field coordinates; the rules live elsewhere. What is on the field is in <see cref="FieldEntities"/>, how each
/// kind is put there at the start of a wave is its <see cref="IWaveSpawner"/>, what happens when two things touch is
/// an <see cref="ICollisionPhase"/>, and whether they are touching is the <see cref="IContactTest"/> the field is given.
/// Each tick, in order: the freeze after the player's death, the grunts' speed-up, the wave-start appear, the wall,
/// the player and the lasers, every list of entities, the collision rules, then the dead are taken out.
/// </remarks>
public sealed class PlayField
{
    private readonly GruntSpeedProgression _gruntSpeed;
    private readonly LaserWallFlares _laserWallFlares = new();
    private readonly WaveMaterialisation _materialisation = new();
    private readonly IContactTest _contactTest;
    private readonly GamePalette? _palette;
    private readonly Random _random;
    /// <summary>Ticks left of the freeze that follows the player's death.</summary>
    private int _hitStopTicksRemaining;

    /// <summary>Shells fired this wave. Only a laser hit takes one off, so a wave stops firing after twenty have fizzled (notes §53).</summary>
    private int _shellsFiredThisWave;

    /// <summary>Makes the field for one wave and puts everything on it.</summary>
    /// <param name="sprites">The sprites everything is drawn with.</param>
    /// <param name="parameters">The wave's numbers: how many of each kind, and how fast.</param>
    /// <param name="input">Where the player's moves come from.</param>
    /// <param name="innerBounds">The inside of the wall, in port pixels.</param>
    /// <param name="cycle">The wall's colours when no live palette is given.</param>
    /// <param name="random">The field's random source.</param>
    /// <param name="startingLives">How many lives the player has.</param>
    /// <param name="startingScore">The score carried in from the last wave.</param>
    /// <param name="startingRescues">The rescues this life carried in from the last wave.</param>
    /// <param name="palette">The live palette, or null in a test.</param>
    /// <param name="playerInvincibleForTesting">True for the playtest player, who cannot be killed.</param>
    /// <param name="contactTest">How two things are tested for touching; null compares their boxes.</param>
    /// <param name="extraManEveryPoints">How many points earn a spare man.</param>
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
            GamePalette? palette = null,
            bool playerInvincibleForTesting = true,
            IContactTest? contactTest = null,
            int extraManEveryPoints = GameSettings.FactoryExtraManEveryPoints)
    {
        Sprites = sprites;
        Parameters = parameters;
        _gruntSpeed = new GruntSpeedProgression(parameters.GruntSpeedFloor);
        RescuesThisLife = startingRescues;
        Input = input;
        Score = new ScoreBoard(startingScore, extraManEveryPoints);
        _random = random;
        _palette = palette;
        _contactTest = contactTest ?? new BoxContactTest();
        Wall = new PlayfieldWall(innerBounds, cycle);

        IntVector2 playerStart = new(innerBounds.X + innerBounds.Width / 2, innerBounds.Y + innerBounds.Height / 2);
        Player = new Player(Sprites, playerStart, startingLives) { InvincibleForTesting = playerInvincibleForTesting };
        PlayerLasers = new LaserSlots(Sprites);

        // Each kind's spawner is in its registry row, so a new kind needs no edit here (notes §119).
        var spawning = new WaveSpawnContext(this, new SpawnPlacement(random, innerBounds), random, playerStart);
        foreach (RobotKindInfo robot in RobotKinds.All)
        {
            robot.Spawn?.Spawn(spawning);
        }

        // The family is put on last, after every robot (ROM HUMSTV).
        new FamilyWaveSpawner().Spawn(spawning);
    }

    /// <summary>Counts the sparks in flight. There may never be more than twenty.</summary>
    public int GetActiveSparkCount() => Entities.Sparks.Count(s => s.LifeState == EntityLifeState.Alive);

    /// <summary>Says whether a spheroid may drop another enforcer: there may be eight at most.</summary>
    /// <remarks>Original source: <c>ENFCNT</c> (notes §11).</remarks>
    public bool CanDropEnforcer() => Entities.Enforcers.GetLiveCount() < SpawnTuning.EnforcerCap;

    /// <summary>Says whether a quark may drop another tank: there may be twenty at most.</summary>
    /// <remarks>Original source: <c>TNKCNT</c> (notes §11).</remarks>
    public bool CanDropTank() => Entities.Tanks.GetLiveCount() < SpawnTuning.TankCap;

    /// <summary>Says whether a brain may fire another cruise missile.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNSHT</c>, <c>BCMCNT</c>.</remarks>
    public bool CanFireCruiseMissile() => Entities.CruiseMissiles.GetLiveCount() < CruiseMissileTuning.Max;

    /// <summary>Says whether a tank may fire another shell this wave.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TNKFIR</c>, <c>SHLCNT</c>.</remarks>
    public bool CanFireShell() => _shellsFiredThisWave < SpawnTuning.ShellsPerWave;

    /// <summary>Everything on the field apart from the player, kind by kind.</summary>
    internal FieldEntities Entities { get; } = new();

    /// <summary>Where the player's moves come from.</summary>
    public IPlayerInputSource Input { get; }

    /// <summary>Says whether the wave is won: every enemy that can be killed is gone. Hulks and electrodes do not count.</summary>
    public bool IsLevelCleared() => Entities.AreEnemiesGone();

    /// <summary>The wave's numbers: how many of each kind, and how fast.</summary>
    public LevelParameters Parameters { get; }

    /// <summary>The player.</summary>
    public Player Player { get; }

    /// <summary>The player's lasers in flight.</summary>
    public LaserSlots PlayerLasers { get; }

    /// <summary>
    /// Humans rescued (player touch) during this player's life. ROM
    /// SAVCNT — reset on player death (PLINIT), carried across waves; each
    /// rescue pays ScoreValues.RescueBonus(count) (1000-5000, capped).
    /// </summary>
    public int RescuesThisLife { get; private set; }

    /// <summary>
    /// True during the player's 2-second start grace period AND during the
    /// player's death animation (spec: "ALL ROBOTS ARE IMMOBILE" in both
    /// cases) — robots still tick their death timers, they just don't move,
    /// and remain killable by lasers.
    /// </summary>
    public bool RobotsFrozen => Player.IsInStartGracePeriod || Player.LifeState == EntityLifeState.Dying;

    /// <summary>The player's score.</summary>
    public ScoreBoard Score { get; }

    /// <summary>The wall round the playfield.</summary>
    public PlayfieldWall Wall { get; }

    /// <summary>Current grunt-speed floor (tests; R5 $BE5D).</summary>
    internal int GruntSpeedFloor => _gruntSpeed.Floor;

    /// <summary>Number of live laser-vs-wall flares (test hook).</summary>
    internal int LaserWallFlareCount => _laserWallFlares.Flares.Count;

    /// <summary>Live laser-vs-wall flares (test hook — the ROM's LASCOL pixels).</summary>
    internal IReadOnlyList<LaserWallFlare> LaserWallFlares => _laserWallFlares.Flares;

    /// <summary>
    /// The live 16-slot palette (null in unit tests that pass no palette).
    /// Entities use it for the ROM's direct PCRAM writes — the player death's
    /// slot-12 fade (notes §66) is the one caller.
    /// </summary>
    internal GamePalette? Palette => _palette;

    /// <summary>Robots still waiting for their appear record (tests).</summary>
    internal int PendingAppearCount => _materialisation.PendingCount;

    /// <summary>The sprite set this field's entities are built and drawn with — the field owns it because it builds them.</summary>
    internal SpriteSet Sprites { get; }

    /// <summary>Draws the field: the wall, then everything on it from the back to the front, with the player last.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        // 1. Wall — arcade-faithful: the ROM's per-wave WALL colour slot (RRG23
        //    `GTWCOL` -> `WALCOL`, solid fill); the placeholder WallColorCycle
        //    has no palette to read, and is only used when no live palette is
        //    wired in (unit tests).
        Wall.Draw(spriteBatch, Sprites.WallPixel,
            _palette is { } p ? p.Color(WavePaletteTables.GetWallSlot(Parameters.LevelNumber)) : null);

        // 1b. Laser-vs-wall flares (RRG23 LASDIE): painted OVER the wall, exactly as the ROM writes those pixels.
        _laserWallFlares.Draw(spriteBatch, Sprites, Parameters.LevelNumber);

        // 2-4. The electrodes, the family and their markers, then the robots — one loop over the field's draw order.
        Entities.DrawBehindShots(spriteBatch, this);

        // 5. Player lasers.
        PlayerLasers.Draw(spriteBatch);

        // 6-7b. The enemy shots, then the explosions and the bursts — over the shots, under the player.
        Entities.DrawInFrontOfShots(spriteBatch, this);

        // 8. Player — ALWAYS last (spec states this explicitly twice).
        Player.Draw(spriteBatch);
    }

    /// <summary>A brain fires a cruise missile at the player (RRB10 <c>BRSHT</c>, which asks for <c>BSHSND</c>).</summary>
    /// <param name="origin">Where the missile starts.</param>
    public void SpawnCruiseMissile(IntVector2 origin)
    {
        var missile = new CruiseMissile(Sprites, origin, Player.Position, _random);
        Entities.CruiseMissiles.Add(missile);
        PlaySoundFrom(SoundTables.BrainShoot, missile.Bounds);
    }

    /// <summary>A spheroid drops an enforcer (RRC11, which asks for <c>ENDSND</c>).</summary>
    /// <param name="position">Where the enforcer grows.</param>
    public void SpawnEnforcer(IntVector2 position)
    {
        var enforcer = new Enforcer(Sprites, position, _random, Parameters.EnforcerFireDelay);
        Entities.Enforcers.Add(enforcer);
        PlaySoundFrom(SoundTables.EnforcerDropOff, enforcer.Bounds);
    }

    /// <summary>ROM BMUT: a brain's touch turns the human into a PROG at its spot.</summary>
    public void SpawnProg(IntVector2 position, HumanKind kind) => Entities.Progs.Add(new Prog(Sprites, position, kind, _random));

    /// <summary>An enforcer fires a spark (RRC11 <c>ENFSHT</c>, which asks for <c>ENFSND</c>).</summary>
    /// <param name="origin">Where the spark starts.</param>
    /// <param name="playerPosition">Where the player is, which the spark is aimed at.</param>
    public void SpawnSpark(IntVector2 origin, IntVector2 playerPosition)
    {
        // Wall bounds are passed through for the ROM's left-wall jitter rule
        // (RRC11.ASM ENFSHT: no X jitter within 16 columns of the wall).
        var spark = new Spark(Sprites, origin, playerPosition, _random, Wall.PlayfieldBounds);
        Entities.Sparks.Add(spark);
        PlaySoundFrom(SoundTables.EnforcerShoot, spark.Bounds);
    }

    /// <summary>A quark drops a tank, which is kept inside the playfield.</summary>
    /// <param name="position">Where the quark is.</param>
    /// <returns>The new tank.</returns>
    public Tank SpawnTank(IntVector2 position)
    {
        // Keep the birth inside the playfield (the quark can be hugging a wall).
        position = Tank.GetPositionInside(Wall.PlayfieldBounds, position);
        Tank tank = new(Sprites, position, _random, Parameters.TankFireDelay);
        Entities.Tanks.Add(tank);
        PlaySoundFrom(SoundTables.TankDrop, tank.Bounds); // RRTK4: a quark's drop asks for TKDSND
        return tank;
    }

    /// <summary>A tank fires a shell.</summary>
    /// <param name="origin">The tank's top-left corner.</param>
    public void SpawnTankShell(IntVector2 origin)
    {
        _shellsFiredThisWave++; // ROM INC on fire; only a laser kill decrements (fizzle bug)
        var shell = new TankShell(Sprites, origin, Player.Position, Parameters.ShellSpeed, Wall.PlayfieldBounds, _random);
        Entities.TankShells.Add(shell);
        PlaySoundFrom(SoundTables.TankFire, shell.Bounds);
    }

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

    /// <summary>Moves the whole field on by one tick.</summary>
    /// <param name="gameTime">The time for this tick.</param>
    public void Update(GameTime gameTime)
    {
        // Hit-stop: everything else pauses this tick; Draw still
        // runs against the frozen state (brief freeze-frame on player death).
        if (_hitStopTicksRemaining > 0)
        {
            _hitStopTicksRemaining--;
            return;
        }

        _gruntSpeed.Update(Entities.Grunts);

        _materialisation.Advance(Entities.Explosions, StripClip.CreateFromPortPixels(Wall.PlayfieldBounds));

        _laserWallFlares.Update();

        Wall.Update(gameTime);

        Player.Update(gameTime, this);
        // RRG23 LSPROC asks for LASSND as each laser starts (R5 $3221).
        if (Player.LasersFiredThisUpdate)
        {
            PlaySoundFrom(SoundTables.Laser, Player.Bounds);
        }
        PlayerLasers.Update(gameTime, this);

        Entities.UpdateAll(gameTime, this);
        PlayMovementSounds();
        ResolveCollisions();

        // Prune: remove Dead entries from every list (pruning = count decrement, spec).
        Entities.PruneDead();
    }

    /// <summary>Counts one more human rescued this life.</summary>
    /// <returns>How many have now been rescued this life.</returns>
    /// <remarks>Original source: <c>SAVCNT</c>.</remarks>
    internal int CountRescue() => ++RescuesThisLife;

    /// <summary>Leaves a skull where a human has been killed.</summary>
    /// <param name="position">Where the human stood.</param>
    internal void LeaveSkull(IntVector2 position) => Entities.Skulls.Add(new SkullMarker(Sprites, position));

    /// <summary>Shows the bonus for the latest rescue where the human stood.</summary>
    /// <param name="position">Where the human stood.</param>
    internal void ShowRescueScore(IntVector2 position) => Entities.RescueScores.Add(new RescueScoreMarker(Sprites, position, RescuesThisLife));

    /// <summary>The wave's shell count, which only a LASER kill decrements (the fizzle bug, notes §53).</summary>
    internal void CountShellDestroyed() => _shellsFiredThisWave--;

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

    /// <summary>
    /// True while this entity is still assembling at a wave start: it does not act and is NOT drawn — its appear
    /// records are drawing it (the ROM holds the robots OFF through the appear sequence).
    /// </summary>
    /// <param name="entity">The entity to test.</param>
    internal bool IsMaterialising(IEntity entity) => _materialisation.IsAssembling(entity);

    /// <summary>Asks for a sound that is heard from where its maker is on the playfield: something on the left is heard on the left.</summary>
    /// <param name="sound">The sound's table.</param>
    /// <param name="maker">The box of whatever made the sound.</param>
    internal void PlaySoundFrom(SoundSequence sound, Rectangle maker)
    {
        float pan = StereoPlacement.GetPan(maker, Wall.PlayfieldBounds);
        Sound.Play(sound, pan);
    }

    /// <summary>The death of a robot that plays its OWN burst instead of the strip explosion (notes §64).</summary>
    /// <param name="target">The robot being killed.</param>
    /// <param name="burst">The burst its kind's row built from it.</param>
    internal void KillWithScoreBurst(IEntity target, ScoreBurst burst)
    {
        target.Require<IRemovable>().Kill();
        Entities.ScoreBursts.Add(burst);
    }

    /// <summary>
    /// The death of a robot whose sprite shatters: the kill and the strip explosion (anchored at the sprite's
    /// middle — the ROM's <c>NWCENT</c> path, notes §73). The kinds' rows call this one; each row carries its
    /// own sound.
    /// </summary>
    /// <param name="target">The robot being killed.</param>
    /// <param name="direction">The laser's direction, which picks the explosion's axis and lean.</param>
    internal void KillWithStripExplosion(IEntity target, Direction8 direction)
    {
        target.Require<IRemovable>().Kill();
        if (target is IExplodable explodable)
        {
            SpawnExplosion(explodable, direction);
        }
    }

    /// <summary>Starts the ROM's laser-vs-wall flare where a laser ran off the playfield (notes §63).</summary>
    /// <param name="laserBounds">The laser's box when it hit the wall.</param>
    /// <param name="direction">The laser's direction.</param>
    internal void SpawnLaserWallFlare(Rectangle laserBounds, Direction8 direction) =>
        _laserWallFlares.Spawn(laserBounds, direction, Wall);

    /// <summary>The ROM grunt speedup, applied to every surviving grunt (notes §67).</summary>
    internal void SpeedUpGrunts() => _gruntSpeed.SpeedUp(Entities.Grunts);

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

    /// <summary>Adds to the score, and gives the player a spare man with its sound if the score has earned one.</summary>
    /// <param name="value">The points to add.</param>
    /// <remarks>Disassembly: the score routine at <c>$DBF9</c>.</remarks>
    internal void AwardScore(int value)
    {
        if (Score.Add(value))
        {
            Player.AddLife();
            Sound.Play(SoundTables.Replay);
        }
    }

    /// <summary>Says whether a box touches no electrode, so that something can be put there.</summary>
    /// <param name="box">The box to test, in port pixels.</param>
    internal bool IsClearOfElectrodes(Rectangle box) => Entities.Electrodes.All(electrode => !electrode.Bounds.Intersects(box));

    /// <summary>Queues a wave-start robot to appear strip by strip.</summary>
    /// <param name="robot">The robot to bring in.</param>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>APPEAR</c>.</remarks>
    internal void QueueMaterialise(IEntity robot) => _materialisation.Queue(robot);

    /// <summary>
    /// The sounds things make just by moving: each tank shell that bounced asks for <c>SRBSND</c> (RRTK4,
    /// R5 $4FCD), and the grunts ask for <c>RMVSND</c> once when any of them stepped (RRP8 <c>ROBX</c>).
    /// </summary>
    private void PlayMovementSounds()
    {
        foreach (TankShell shell in Entities.TankShells)
        {
            if (shell.BouncedThisUpdate)
            {
                PlaySoundFrom(SoundTables.ShellRebound, shell.Bounds);
            }
        }

        if (Entities.Grunts.Any(grunt => grunt.SteppedThisUpdate))
        {
            Sound.Play(SoundTables.RobotMove);
        }
    }

    /// <summary>Runs every collision rule in the arcade's order, then freezes the game for a moment if the player has just been killed.</summary>
    private void ResolveCollisions()
    {
        bool playerWasAlive = Player.LifeState == EntityLifeState.Alive;

        foreach (ICollisionPhase phase in CollisionPhases.InArcadeOrder)
        {
            phase.Resolve(this);
        }

        if (playerWasAlive && Player.LifeState == EntityLifeState.Dying)
        {
            _hitStopTicksRemaining = PlayerTuning.HitStopTicks;
            // R5 $30EF (KILL_PLAYER): the death sound ($26D9, p238).
            PlaySoundFrom(SoundTables.PlayerDeath, Player.Bounds);
        }
    }

    /// <summary>
    /// Spawns an explosion for a dying entity. The ROM's explosion and appear
    /// records share ONE pool of 10 <c>EX</c> blocks (RRDX2.ASM's `EX` struct,
    /// `RMB ((10-1)*EXSIZE)`); GETBLK/GETAP both take from the same free list, so
    /// a full list means no explosion at all (the caller's kill still stands).
    /// </summary>
    internal void SpawnExplosion(IExplodable dead, Direction8? direction)
    {
        if (Entities.Explosions.Count >= StripExplosionTuning.MaxConcurrent)
        {
            return; // ROM: list full → no explosion
        }

        Entities.Explosions.Add(StripEffect.CreateExplosion(dead, direction, StripClip.CreateFromPortPixels(Wall.PlayfieldBounds)));
    }

    /// <summary>Says whether two entities are touching, by the field's contact test.</summary>
    /// <param name="a">One entity.</param>
    /// <param name="b">The other entity.</param>
    internal bool Touches(IEntity a, IEntity b) => _contactTest.Touches(a, b);
}
