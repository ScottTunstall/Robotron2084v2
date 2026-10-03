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
/// an <see cref="ICollisionRule"/> that only reports what touched what, and the field responds to each report
/// (<see cref="CollisionResponder"/>). Whether two things are touching is the <see cref="IContactTest"/> the field is given.
/// Each tick, in order: the freeze after the player's death, the grunts' speed-up, the wave-start appear, the wall,
/// the player and the lasers, every list of entities, the collision rules, then the dead are taken out.
/// </remarks>
public sealed class PlayField : ICollisionScene
{
    private readonly GruntSpeedProgression _gruntSpeed;
    private readonly LaserWallFlares _laserWallFlares = new();
    private readonly WaveMaterialisation _materialisation = new();
    private readonly CollisionResponder _collisionResponder;
    private readonly IContactTest _contactTest;
    private readonly MidWaveSpawner _midWave;
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
            GamePalette? palette = null,
            bool playerInvincibleForTesting = true,
            IContactTest? contactTest = null,
            int extraManEveryPoints = GameSettings.FactoryExtraManEveryPoints)
    {
        Sprites = sprites;
        Parameters = parameters;
        _gruntSpeed = new GruntSpeedProgression(parameters.GruntSpeedFloor);
        Input = input;
        Score = new ScoreBoard(startingScore, extraManEveryPoints);
        _random = random;
        _palette = palette;
        _contactTest = contactTest ?? new BoxContactTest();
        _midWave = new MidWaveSpawner(this, Entities, random);
        _collisionResponder = new CollisionResponder(this);
        Wall = new PlayfieldWall(innerBounds, cycle);

        IntVector2 playerStart = new(innerBounds.X + innerBounds.Width / 2, innerBounds.Y + innerBounds.Height / 2);
        Player = new Player(Sprites, playerStart, startingLives) { InvincibleForTesting = playerInvincibleForTesting };
        PlayerLasers = new LaserSlots(Sprites);

        // Each kind's spawner is in its registry row, so a new kind needs no edit here (notes §119).
        var spawning = new WaveSpawnContext(this, Entities, new SpawnPlacement(random, innerBounds), random, playerStart);
        foreach (RobotKindInfo robot in RobotKinds.All)
        {
            robot.Spawn?.Spawn(spawning);
        }

        // The family is put on last, after every robot (ROM HUMSTV).
        new FamilyWaveSpawner().Spawn(spawning);
    }

    /// <summary>Where the player's moves come from.</summary>
    public IPlayerInputSource Input { get; }

    /// <summary>The wave's numbers: how many of each kind, and how fast.</summary>
    public LevelParameters Parameters { get; }

    /// <summary>The player.</summary>
    public Player Player { get; }

    /// <summary>The player's lasers in flight.</summary>
    public LaserSlots PlayerLasers { get; }

    /// <summary>Humans rescued so far on this wave. Every wave starts at none, because the ROM clears <c>SAVCNT</c> as each wave starts (<c>PLINIT</c>).</summary>
    public int RescuesThisLife { get; private set; }

    /// <summary>True while the robots must stand still: in the player's start grace period and during their death animation.</summary>
    public bool RobotsFrozen => Player.IsInStartGracePeriod || Player.IsDying();

    /// <summary>The player's score.</summary>
    public ScoreBoard Score { get; }

    /// <summary>The wall round the playfield.</summary>
    public PlayfieldWall Wall { get; }

    /// <summary>Where the player's top-left corner is.</summary>
    public IntVector2 PlayerPosition => Player.Position;

    /// <summary>The inside of the wall, in port pixels.</summary>
    public Rectangle PlayfieldBounds => Wall.PlayfieldBounds;

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

    /// <summary>Copies the live score and lives to the player's session slot, every tick, so the HUD never lags (notes §97).</summary>
    /// <param name="slot">The player's session slot.</param>
    public void SyncInto(PlayerSlot slot)
    {
        slot.Score = Score.Score;
        slot.Lives = Player.Lives;
    }

    /// <summary>Says whether the wave is won: every enemy that can be killed is gone. Hulks and electrodes do not count.</summary>
    public bool IsLevelCleared() => Entities.AreEnemiesGone();

    /// <summary>Counts the sparks in flight. There may never be more than twenty.</summary>
    public int GetActiveSparkCount() => Entities.GetSparkCount();

    /// <summary>Says whether a spheroid may drop another enforcer: there may be eight at most.</summary>
    /// <remarks>Original source: <c>ENFCNT</c> (notes §11).</remarks>
    public bool CanDropEnforcer() => Entities.GetEnforcerCount() < SpawnTuning.EnforcerCap;

    /// <summary>Says whether a quark may drop another tank: there may be twenty at most.</summary>
    /// <remarks>Original source: <c>TNKCNT</c> (notes §11).</remarks>
    public bool CanDropTank() => Entities.GetTankCount() < SpawnTuning.TankCap;

    /// <summary>Says whether a brain may fire another cruise missile.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNSHT</c>, <c>BCMCNT</c>.</remarks>
    public bool CanFireCruiseMissile() => Entities.GetCruiseMissileCount() < CruiseMissileTuning.Max;

    /// <summary>Says whether a tank may fire another shell this wave.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TNKFIR</c>, <c>SHLCNT</c>.</remarks>
    public bool CanFireShell() => _shellsFiredThisWave < SpawnTuning.ShellsPerWave;

    /// <summary>Says whether the player is alive, and so can move, shoot and be hit.</summary>
    public bool IsPlayerAlive() => Player.IsAlive();

    /// <summary>Says whether the player has finished dying.</summary>
    public bool IsPlayerDead() => Player.IsDead();

    /// <summary>Says whether the player can be killed just now: alive, and not the invincible playtest player.</summary>
    public bool CanPlayerBeHurt() => Player.IsAlive() && !Player.IsInvincible;

    /// <summary>Kills the player.</summary>
    public void KillPlayer() => Player.Kill();

    /// <summary>Says whether the player is touching an entity.</summary>
    /// <param name="entity">The entity to test.</param>
    public bool TouchesPlayer(IEntity entity) => Touches(Player, entity);

    /// <summary>Lists the player's lasers that are in flight.</summary>
    public IEnumerable<PlayerLaser> GetActiveLasers() => PlayerLasers.GetActiveLasers();

    /// <summary>Says whether a box touches the wall.</summary>
    /// <param name="box">The box to test, in port pixels.</param>
    public bool HitsWall(Rectangle box) => Wall.Intersects(box);

    /// <summary>Says whether two entities are touching, by the field's contact test.</summary>
    /// <param name="a">One entity.</param>
    /// <param name="b">The other entity.</param>
    public bool Touches(IEntity a, IEntity b) => _contactTest.Touches(a, b);

    /// <summary>Finds where the nearest living robot to a point is, for the attract demo's player to steer by.</summary>
    /// <param name="from">The point to measure from.</param>
    public IntVector2? GetNearestLivingRobotPosition(IntVector2 from) => Entities.GetNearestLivingRobotPosition(from);

    /// <summary>A brain fires a cruise missile at the player.</summary>
    /// <param name="origin">Where the missile starts.</param>
    public void SpawnCruiseMissile(IntVector2 origin) => _midWave.SpawnCruiseMissile(origin);

    /// <summary>A spheroid drops an enforcer.</summary>
    /// <param name="position">Where the enforcer grows.</param>
    public void SpawnEnforcer(IntVector2 position) => _midWave.SpawnEnforcer(position);

    /// <summary>A brain's touch turns a human into a prog where they stand.</summary>
    /// <param name="position">Where the human stood.</param>
    /// <param name="kind">Which family member it was.</param>
    public void SpawnProg(IntVector2 position, HumanKind kind) => _midWave.SpawnProg(position, kind);

    /// <summary>An enforcer fires a spark at the player.</summary>
    /// <param name="origin">Where the spark starts.</param>
    /// <param name="playerPosition">Where the player is.</param>
    public void SpawnSpark(IntVector2 origin, IntVector2 playerPosition) => _midWave.SpawnSpark(origin, playerPosition);

    /// <summary>A quark drops a tank, which is kept inside the playfield.</summary>
    /// <param name="position">Where the quark is.</param>
    /// <returns>The new tank.</returns>
    public Tank SpawnTank(IntVector2 position) => _midWave.SpawnTank(position);

    /// <summary>A tank fires a shell.</summary>
    /// <param name="origin">The tank's top-left corner.</param>
    public void SpawnTankShell(IntVector2 origin) => _midWave.SpawnTankShell(origin);

    /// <summary>Everything on the field apart from the player, kind by kind.</summary>
    internal FieldEntities Entities { get; } = new();

    /// <summary>The sprite set this field's entities are built and drawn with — the field owns it because it builds them.</summary>
    internal SpriteSet Sprites { get; }

    /// <summary>The live palette, or null in a test. The player's death fade writes to it (notes §66).</summary>
    internal GamePalette? Palette => _palette;

    /// <summary>Current grunt-speed floor (tests; R5 $BE5D).</summary>
    internal int GruntSpeedFloor => _gruntSpeed.Floor;

    /// <summary>Number of live laser-vs-wall flares (test hook).</summary>
    internal int LaserWallFlareCount => _laserWallFlares.Flares.Count;

    /// <summary>Live laser-vs-wall flares (test hook — the ROM's LASCOL pixels).</summary>
    internal IReadOnlyList<LaserWallFlare> LaserWallFlares => _laserWallFlares.Flares;

    /// <summary>Robots still waiting for their appear record (tests).</summary>
    internal int PendingAppearCount => _materialisation.PendingCount;

    /// <summary>Fires one of the player's lasers, if one of their three slots is free.</summary>
    /// <param name="position">Where the laser starts.</param>
    /// <param name="direction">The way it flies.</param>
    /// <returns>True when a laser was fired.</returns>
    internal bool TryFirePlayerLaser(IntVector2 position, Direction8 direction) => PlayerLasers.TryFire(position, direction, out _);

    /// <summary>Lists the electrodes.</summary>
    internal IReadOnlyList<Electrode> GetElectrodes() => Entities.Electrodes;

    /// <summary>Says whether a box touches no electrode, so that something can be put there.</summary>
    /// <param name="box">The box to test, in port pixels.</param>
    internal bool IsClearOfElectrodes(Rectangle box) => Entities.IsClearOfElectrodes(box);

    /// <summary>Finds the family member in a place in the family list, if they are standing and free.</summary>
    /// <param name="slot">The place to look in.</param>
    internal Human? GetFamilyMemberInSlot(int slot) => Entities.GetFamilyMemberInSlot(slot);

    /// <summary>Says whether any family member is standing on the field and free.</summary>
    internal bool AnyFamilyMemberAvailable() => Entities.AnyFamilyMemberAvailable();

    /// <summary>Finds the family list place of the member nearest a point.</summary>
    /// <param name="from">The point to measure from.</param>
    internal int GetNearestFamilySlot(IntVector2 from) => Entities.GetNearestFamilySlot(from);

    /// <summary>Adds to the score, and gives the player a spare man with its sound if the score has earned one.</summary>
    /// <param name="value">The points to add.</param>
    /// <remarks>Disassembly: the score routine at <c>$DBF9</c>.</remarks>
    internal void AwardScore(int value)
    {
        _gruntSpeed.NoteScore();
        if (Score.Add(value))
        {
            Player.AddLife();
            Sound.Play(SoundTables.Replay);
        }
    }

    /// <summary>Adds a rescue's bonus to the score, and gives the player a spare man if it earns one.</summary>
    /// <param name="rescues">How many humans have now been rescued this life.</param>
    internal void AwardRescueBonus(int rescues)
    {
        _gruntSpeed.NoteScore();
        if (Score.Add(ScoreValues.RescueBonus(rescues)))
        {
            Player.AddLife();
        }
    }

    /// <summary>Counts one more human rescued this life.</summary>
    /// <returns>How many have now been rescued this life.</returns>
    /// <remarks>Original source: <c>SAVCNT</c>.</remarks>
    internal int CountRescue() => ++RescuesThisLife;

    /// <summary>Shows the bonus for the latest rescue where the human stood.</summary>
    /// <param name="position">Where the human stood.</param>
    internal void ShowRescueScore(IntVector2 position) => Entities.Add(new RescueScoreMarker(Sprites, position, RescuesThisLife));

    /// <summary>Leaves a skull where a human has been killed.</summary>
    /// <param name="position">Where the human stood.</param>
    internal void LeaveSkull(IntVector2 position) => Entities.Add(new SkullMarker(Sprites, position));

    /// <summary>Counts one more shell fired this wave.</summary>
    internal void CountShellFired() => _shellsFiredThisWave++;

    /// <summary>The wave's shell count, which only a LASER kill decrements (the fizzle bug, notes §53).</summary>
    internal void CountShellDestroyed() => _shellsFiredThisWave--;

    /// <summary>The death of a robot that plays its OWN burst instead of the strip explosion (notes §64).</summary>
    /// <param name="target">The robot being killed.</param>
    /// <param name="burst">The burst its kind's row built from it.</param>
    internal void KillWithScoreBurst(IEntity target, ScoreBurst burst)
    {
        target.Require<IRemovable>().Kill();
        Entities.Add(burst);
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

        Entities.Add(StripEffect.CreateExplosion(dead, direction, StripClip.CreateFromPortPixels(Wall.PlayfieldBounds)));
    }

    /// <summary>Starts the ROM's laser-vs-wall flare where a laser ran off the playfield (notes §63).</summary>
    /// <param name="laserBounds">The laser's box when it hit the wall.</param>
    /// <param name="direction">The laser's direction.</param>
    internal void SpawnLaserWallFlare(Rectangle laserBounds, Direction8 direction) =>
        _laserWallFlares.Spawn(laserBounds, direction, Wall);

    /// <summary>The ROM grunt speedup, applied to every surviving grunt (notes §67).</summary>
    internal void SpeedUpGrunts() => _gruntSpeed.SpeedUp(Entities.Grunts);

    /// <summary>Moves one entity on, unless it is still assembling: the ROM holds the robots off through the appear sequence (notes §62).</summary>
    internal void UpdateEntity(IEntity entity, GameTime gameTime)
    {
        if (!entity.IsDead() && !IsMaterialising(entity))
        {
            entity.Update(gameTime, this);
        }
    }

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

    /// <summary>Says whether an entity is still assembling at a wave start. It does not act and is not drawn; its appear effect is.</summary>
    /// <param name="entity">The entity to test.</param>
    internal bool IsMaterialising(IEntity entity) => _materialisation.IsAssembling(entity);

    /// <summary>Queues a wave-start robot to appear strip by strip.</summary>
    /// <param name="robot">The robot to bring in.</param>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>APPEAR</c>.</remarks>
    internal void QueueMaterialise(IEntity robot) => _materialisation.Queue(robot);

    /// <summary>Asks for a sound that is heard from where its maker is on the playfield: something on the left is heard on the left.</summary>
    /// <param name="sound">The sound's table.</param>
    /// <param name="maker">The box of whatever made the sound.</param>
    internal void PlaySoundFrom(SoundSequence sound, Rectangle maker)
    {
        float pan = StereoPlacement.GetPan(maker, Wall.PlayfieldBounds);
        Sound.Play(sound, pan);
    }

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

        if (Entities.HasGruntStepped())
        {
            Sound.Play(SoundTables.RobotMove);
        }
    }

    /// <summary>Runs every collision rule in the arcade's order, then freezes the game for a moment if the player has just been killed.</summary>
    private void ResolveCollisions()
    {
        bool playerWasAlive = Player.IsAlive();

        foreach (ICollisionRule rule in CollisionRules.InArcadeOrder)
        {
            foreach (CollisionResult result in rule.Detect(this, Entities))
            {
                _collisionResponder.Respond(result);
            }
        }

        if (playerWasAlive && Player.IsDying())
        {
            _hitStopTicksRemaining = PlayerTuning.HitStopTicks;
            // R5 $30EF (KILL_PLAYER): the death sound ($26D9, p238).
            PlaySoundFrom(SoundTables.PlayerDeath, Player.Bounds);
        }
    }
}
