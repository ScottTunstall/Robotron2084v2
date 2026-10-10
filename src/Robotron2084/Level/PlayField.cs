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

/// <summary>
/// The playfield is where one wave of the game is played. It holds the player, the robots and the family, and it is in charge of them.
/// The game runs in ticks. A tick is one step of the game's clock, and there are 60 of them every second. MonoGame, the toolkit the game is built with, keeps the clock and runs <see cref="RobotronGame"/> once for each tick.
/// On each tick the playfield calls <see cref="IEntity.Update"/> on every character on the field, and then checks what has touched what.
/// It does not invoke the Update method during the short freeze after the player dies, or one that is still appearing at the start of a wave.
/// A beat is a character's own turn to think and move. The playfield does not decide when a beat happens. It only calls <c>Update</c>, and each character works out for itself whether this tick is one of its beats.
/// So a tick belongs to the playfield, and a beat belongs to a character. Ticks come at a steady rate. Beats come less often, so each kind of character has its own pace.
/// </summary>
/// <remarks>
/// The field coordinates; the rules live elsewhere. What is on the field is in <see cref="FieldEntities"/>, how each
/// kind is put there at the start of a wave is its <see cref="IWaveSpawner"/>, what happens when two things touch is
/// an <see cref="ICollisionRule"/> that only reports what touched what, and the field responds to each report
/// (<see cref="CollisionResponder"/>). Whether two things are touching is the <see cref="IContactTest"/> the field is given.
/// Each tick, in order: the freeze after the player's death, the start of the wave, the grunts' speed-up, the robots' and the player's appear, the wall,
/// the player and the lasers, every list of entities, the collision rules, then the dead are taken out.
/// <para>
/// Where a tick comes from: MonoGame's fixed time step, which <see cref="RobotronGame"/> switches on and leaves at MonoGame's own rate of 60 updates a second.
/// MonoGame calls <c>RobotronGame.Update</c>, which passes the tick to <see cref="States.GameStateManager.Update"/> and so to the current state
/// (<see cref="States.PlayingState"/> or <see cref="States.AttractState"/>), which calls <see cref="Update"/>. The field then calls each entity's <see cref="IEntity.Update"/>,
/// through <see cref="FieldEntities.UpdateAll"/> and <see cref="UpdateEntity"/>. What an entity does with a tick is the entity's own business, and is described on the entity (CMT-16).
/// </para>
/// </remarks>
public sealed class PlayField : ICollisionScene
{
    private readonly GruntSpeedProgression _gruntSpeedProgression;
    private readonly LaserWallFlares _laserWallFlares = new();
    private readonly WaveMaterialisation _materialisation;
    private readonly CollisionResponder _collisionResponder;
    private readonly IContactTest _contactTest;
    private readonly MidWaveSpawner _midWaveSpawner;
    private readonly WaveStartSequence _waveStart;
    private readonly PlayerAppear _playerAppear = new();
    private readonly GamePalette? _palette;

    /// <summary>True when a fizzled shell stays on the wave's shell count, as in the arcade.</summary>
    private readonly bool _tankShellBugEnabled;

    /// <summary>Ticks left of the freeze that follows the player's death.</summary>
    private int _hitStopTicksRemaining;

    /// <summary>How many shells the tanks have fired this wave, less the ones the player has shot. A shell that fizzles out is never taken off, so the tanks stop firing once the limit is reached (notes §53).</summary>
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
    /// <param name="tankShellBugEnabled">True to keep the arcade's bug: a shell that fizzles out stays on the wave's shell count.</param>
    /// <param name="brainsChaseMikeyBugEnabled">True to keep the arcade's bug: every brain starts the wave chasing the first Mikey.</param>
    /// <param name="familySlotsLeftOver">The places in the family list that still held a family member when the last wave or life ended. The hulks pick what to stalk from these. Null means none were left.</param>
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
            int extraManEveryPoints = GameSettings.FactoryExtraManEveryPoints,
            bool tankShellBugEnabled = GameSettings.FactoryTankShellBugEnabled,
            bool brainsChaseMikeyBugEnabled = GameSettings.FactoryBrainsChaseMikeyBugEnabled,
            IReadOnlyList<int>? familySlotsLeftOver = null)
    {
        _tankShellBugEnabled = tankShellBugEnabled;
        Sprites = sprites;
        Parameters = parameters;
        _gruntSpeedProgression = new GruntSpeedProgression(parameters.GruntSpeedFloor);
        Input = input;
        ScoreBoard = new ScoreBoard(startingScore, extraManEveryPoints);
        _materialisation = new WaveMaterialisation(random, parameters.BrainCount > 0);
        _palette = palette;
        _contactTest = contactTest ?? new BoxContactTest();
        _midWaveSpawner = new MidWaveSpawner(this, Entities, random);
        _collisionResponder = new CollisionResponder(this);
        Wall = new PlayfieldWall(innerBounds, cycle);

        IntVector2 playerStart = new(innerBounds.X + innerBounds.Width / 2, innerBounds.Y + innerBounds.Height / 2);
        Player = new Player(Sprites, playerStart, startingLives) { InvincibleForTesting = playerInvincibleForTesting };
        PlayerLasers = new LaserSlots(Sprites);

        // Each kind's spawner is in its registry row, so a new kind needs no edit here (notes §119).
        var spawning = new WaveSpawnContext(this, Entities, new SpawnPlacement(random, innerBounds), random, playerStart, familySlotsLeftOver ?? []);
        foreach (RobotKindInfo robot in RobotKinds.All)
        {
            robot.Spawn?.Spawn(spawning);
        }

        // The family is put on last, after every robot (ROM HUMSTV).
        new FamilyWaveSpawner().Spawn(spawning);

        // The robots on the arcade's robot list are brought on in the order its appear loop meets them (ROM: RRG23.ASM APPEAR).
        IEntity[] robotsInAppearOrder = [.. GetRobotsInAppearOrder()];
        foreach (IEntity robot in robotsInAppearOrder)
        {
            _materialisation.Queue(robot);
        }

        _waveStart = new WaveStartSequence(robotsInAppearOrder.Length, parameters.BrainCount > 0);

        // With the bug switched off, each brain picks again now that there is a family to pick from.
        if (!brainsChaseMikeyBugEnabled)
        {
            foreach (Brain brain in Entities.Brains)
            {
                brain.Retarget(Entities.GetNearestFamilySlot(brain.Position));
            }
        }
    }

    /// <summary>Where the player's moves come from.</summary>
    public IPlayerInputSource Input { get; }

    /// <summary>The wave's numbers: how many of each kind, and how fast.</summary>
    public LevelParameters Parameters { get; }

    /// <summary>The player.</summary>
    public Player Player { get; }

    /// <summary>The player's lasers in flight.</summary>
    public LaserSlots PlayerLasers { get; }

    /// <summary>How many humans the player has rescued so far on this wave. Every wave starts at none.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>PLINIT</c>, which clears <c>SAVCNT</c> as each wave
    /// starts.</item>
    /// <item>Disassembly: not separately labelled.</item>
    /// </list>
    /// </remarks>
    public int RescuesThisLife { get; private set; }

    /// <summary>True while the robots must stand still: until the game goes live at the start of the wave, and while the player is dying.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: each robot's routine waits for <c>STATUS</c> to clear (<c>RRP8.ASM</c>
    /// <c>ROBOT</c>, <c>RRH11.ASM</c> <c>HULK</c>).</item>
    /// <item>Disassembly: the status byte at <c>$59</c>.</item>
    /// </list>
    /// </remarks>
    public bool RobotsFrozen() => !IsLive() || Player.IsDying();

    /// <summary>The player's score.</summary>
    public ScoreBoard ScoreBoard { get; }

    /// <summary>The wall round the playfield.</summary>
    public PlayfieldWall Wall { get; }

    /// <summary>Where the player's top-left corner is.</summary>
    public IntVector2 GetPlayerPosition() => Player.Position;

    /// <summary>The inside of the wall, in port pixels.</summary>
    public Rectangle GetPlayfieldBounds() => Wall.PlayfieldBounds;

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

        _waveStart.Update();

        // The arcade times the grunt speed checks from when the game goes live (ROM: RRG23.ASM PLS2, CLR STATUS).
        if (IsLive())
        {
            _gruntSpeedProgression.Update(Entities.Grunts, _waveStart.GetLiveClockUnitsThisTick());
        }

        if (_waveStart.HasJustGoneLive())
        {
            TellRobotsTheGameIsLive();
        }

        _materialisation.Advance(Entities, Wall.PlayfieldBounds);
        _playerAppear.Update(_waveStart, Player, Entities, StripClip.CreateFromPortPixels(Wall.PlayfieldBounds));

        _laserWallFlares.Update();

        Wall.Update(gameTime);

        Player.Update(gameTime, this);
        // RRG23 LSPROC asks for LASSND as each laser starts (R5 $3221).
        if (Player.FiredLaserThisUpdate)
        {
            PlaySoundFrom(SoundTables.Laser, Player.GetBounds());
        }
        PlayerLasers.Update(gameTime, this);

        Entities.UpdateAll(gameTime, this);
        PlayShellAndGruntMovementSounds();
        ResolveCollisions();

        // Prune: remove Dead entries from every list (pruning = count decrement, spec).
        Entities.PruneDead();
    }

    /// <summary>Draws the field: the wall, then everything on it from the back to the front, with the player last.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        // 1. Wall.
        DrawWall(spriteBatch);

        // 1b. Laser-vs-wall flares (RRG23 LASDIE): painted OVER the wall, exactly as the ROM writes those pixels.
        _laserWallFlares.Draw(spriteBatch, Sprites, Parameters.LevelNumber);

        // 2-4. The electrodes, the family and their markers, then the robots — one loop over the field's draw order.
        Entities.DrawBehindShots(spriteBatch, this);
        _materialisation.DrawTransport(spriteBatch, Sprites);

        // 5. Player lasers.
        PlayerLasers.Draw(spriteBatch);

        // 6-7b. The enemy shots, then the explosions and the bursts — over the shots, under the player.
        Entities.DrawInFrontOfShots(spriteBatch, this);

        // 8. Player — ALWAYS last (spec states this explicitly twice). Its own sprite is not drawn until the game is live;
        //    before that only the strips of its appear effect are (ROM: STATUS bit 4, "PLAYER OUTPUT", held until PLS2; RRS22.ASM PLA0).
        if (IsPlayerDrawn())
        {
            Player.Draw(spriteBatch);
        }
    }

    /// <summary>Draws the wall round the playfield and nothing else. It is what is on the screen, with the scores, while the arcade shows whose turn it is before it sets a wave up.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>GTWCOL</c>, which picks the wall's colour for the wave
    /// (<c>WALCOL</c>), and <c>BORDER</c>, which <c>TDISP</c> calls.</item>
    /// <item>Disassembly: not separately labelled. The placeholder <see cref="WallColorCycle"/> is only
    /// used when there is no live palette, which is in a test.</item>
    /// </list>
    /// </remarks>
    public void DrawWall(SpriteBatch spriteBatch) =>
        Wall.Draw(spriteBatch, Sprites.WallPixelSprite, _palette?.GetColour(WavePaletteTables.GetWallSlot(Parameters.LevelNumber)));

    /// <summary>Copies the live score and lives to the player's session slot, every tick, so the HUD never lags (notes §97).</summary>
    /// <param name="slot">The player's session slot.</param>
    public void SyncInto(PlayerSlot slot)
    {
        slot.Score = ScoreBoard.Score;
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
    /// <remarks>
    /// Original source: <c>RRTK4.ASM</c> <c>TNKFIR</c>, <c>SHLCNT</c>. The ROM stops only when the count is higher than the limit (<c>LBHI</c>), so a count equal to it still fires.
    /// With the tank shell bug switched off, the count is the shells on the field, which is what the ROM's count was meant to be.
    /// </remarks>
    public bool CanFireShell() => (_tankShellBugEnabled ? _shellsFiredThisWave : Entities.TankShells.GetLiveCount()) <= SpawnTuning.ShellCountLimit;

    /// <summary>Says whether the player is alive, and so can move, shoot and be hit.</summary>
    public bool IsPlayerAlive() => Player.IsAlive();

    /// <summary>Says whether the game is live: the start of the wave is over, so the player can move and fire, things can touch the player, and the robots act.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>PLS2</c>, <c>CLR STATUS</c>.</item>
    /// <item>Disassembly: <c>$289A</c>.</item>
    /// </list>
    /// </remarks>
    public bool IsLive() => _waveStart.IsLive();

    /// <summary>Says whether the player has finished dying.</summary>
    public bool IsPlayerDead() => Player.IsDead();

    /// <summary>Says whether the player can be killed just now: alive, and not the invincible playtest player.</summary>
    public bool CanPlayerBeHurt() => Player.IsAlive() && !Player.IsInvincible();

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
    public void SpawnCruiseMissile(IntVector2 origin) => _midWaveSpawner.SpawnCruiseMissile(origin);

    /// <summary>A spheroid drops an enforcer.</summary>
    /// <param name="position">Where the enforcer grows.</param>
    public void SpawnEnforcer(IntVector2 position) => _midWaveSpawner.SpawnEnforcer(position);

    /// <summary>A brain's touch turns a human into a prog where they stand.</summary>
    /// <param name="position">Where the human stood.</param>
    /// <param name="kind">Which family member it was.</param>
    public void SpawnProg(IntVector2 position, HumanKind kind) => _midWaveSpawner.SpawnProg(position, kind);

    /// <summary>An enforcer fires a spark at the player.</summary>
    /// <param name="origin">Where the spark starts.</param>
    /// <param name="playerPosition">Where the player is.</param>
    public void SpawnSpark(IntVector2 origin, IntVector2 playerPosition) => _midWaveSpawner.SpawnSpark(origin, playerPosition);

    /// <summary>A quark drops a tank, which is kept inside the playfield.</summary>
    /// <param name="position">Where the quark is.</param>
    /// <returns>The new tank.</returns>
    public Tank SpawnTank(IntVector2 position) => _midWaveSpawner.SpawnTank(position);

    /// <summary>Gorf drops a grunt, which falls to the ground.</summary>
    /// <param name="from">Where the grunt starts.</param>
    /// <param name="landing">Where it ends up standing.</param>
    public void SpawnGrunt(IntVector2 from, IntVector2 landing) => _midWaveSpawner.SpawnGrunt(from, landing);

    /// <summary>A tank fires a shell.</summary>
    /// <param name="origin">The tank's top-left corner.</param>
    public void SpawnTankShell(IntVector2 origin) => _midWaveSpawner.SpawnTankShell(origin);

    /// <summary>Everything on the field apart from the player, kind by kind.</summary>
    internal FieldEntities Entities { get; } = new();

    /// <summary>The sprite set this field's entities are built and drawn with — the field owns it because it builds them.</summary>
    internal SpriteSet Sprites { get; }

    /// <summary>The live palette, or null in a test. The player's death fade writes to it (notes §66).</summary>
    internal GamePalette? Palette => _palette;

    /// <summary>The shortest wait between moves that the grunts' speed-ups may bring a grunt to (test hook).</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>RMXSPD</c>.</item>
    /// <item>Disassembly: <c>$BE5D</c>.</item>
    /// </list>
    /// </remarks>
    internal int GetGruntSpeedFloor() => _gruntSpeedProgression.Floor;

    /// <summary>How many flashes of colour are showing where lasers hit the wall (test hook).</summary>
    internal int GetLaserWallFlareCount() => _laserWallFlares.Flares.Count;

    /// <summary>The flashes of colour that are showing where lasers hit the wall (test hook).</summary>
    internal IReadOnlyList<LaserWallFlare> GetLaserWallFlares() => _laserWallFlares.Flares;

    /// <summary>How many robots are still waiting for their turn to appear at the start of the wave (test hook).</summary>
    internal int GetPendingAppearCount() => _materialisation.GetPendingCount();

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

    /// <summary>Lists the places in the family list that still hold a family member who is standing on the field and free, lowest place first. When this field ends, the next field's hulks pick what to stalk from them.</summary>
    public IReadOnlyList<int> GetOccupiedFamilySlots() => Entities.GetOccupiedFamilySlots();

    /// <summary>Finds the family list place of the member nearest a point.</summary>
    /// <param name="from">The point to measure from.</param>
    internal int GetNearestFamilySlot(IntVector2 from) => Entities.GetNearestFamilySlot(from);

    /// <summary>Adds to the score, and gives the player a spare man with its sound if the score has earned one.</summary>
    /// <param name="value">The points to add.</param>
    /// <remarks>Disassembly: the score routine at <c>$DBF9</c>.</remarks>
    internal void AwardScore(int value)
    {
        _gruntSpeedProgression.NoteScore();
        if (ScoreBoard.Add(value))
        {
            Player.AddLife();
            Sound.Play(SoundTables.Replay);
        }
    }

    /// <summary>Adds a rescue's bonus to the score, and gives the player a spare man if it earns one.</summary>
    /// <param name="rescues">How many humans have now been rescued this life.</param>
    internal void AwardRescueBonus(int rescues)
    {
        _gruntSpeedProgression.NoteScore();
        if (ScoreBoard.Add(ScoreValues.RescueBonus(rescues)))
        {
            Player.AddLife();
        }
    }

    /// <summary>Counts one more human rescued this life.</summary>
    /// <returns>How many have now been rescued this life.</returns>
    /// <remarks>Original source: <c>SAVCNT</c>.</remarks>
    internal int CountRescuedFamilyMembers() => ++RescuesThisLife;

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

    /// <summary>Kills a robot and shatters its sprite into strips. Each kind's row calls this, and each row has its own sound.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRDX2.ASM</c> <c>NWCENT</c>, which puts the explosion at the sprite's
    /// middle (notes §73).</item>
    /// <item>Disassembly: not separately labelled.</item>
    /// </list>
    /// </remarks>
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

    /// <summary>Starts an explosion for something that has died. If the strip routine that would run it has no record free there is no explosion, but the thing is still dead.</summary>
    /// <param name="dead">The thing that died.</param>
    /// <param name="direction">The way the laser that killed it was going, or null when it was not a laser.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>GETBLK</c> in <c>RRX7.ASM</c>, <c>RRHX4.ASM</c> and <c>RRDX2.ASM</c>. Each
    /// of the three strip routines has its own records, which its explosions and its appear effects
    /// share (<see cref="StripExplosionTuning.GetPoolSize"/>).</item>
    /// <item>Disassembly: <c>$5B6C</c>, <c>$F03A</c> and <c>$46B2</c>.</item>
    /// </list>
    /// </remarks>
    internal void SpawnExplosion(IExplodable dead, Direction8? direction)
    {
        StripEffect explosion = StripEffect.CreateExplosion(dead, direction, StripClip.CreateFromPortPixels(Wall.PlayfieldBounds));
        if (!Entities.HasRoomForStripEffect(explosion.GetEngine()))
        {
            return; // ROM: no record free → no explosion
        }

        Entities.Add(explosion);
    }

    /// <summary>Starts a flash of colour where a laser ran into the wall (notes §63).</summary>
    /// <param name="laserBounds">The laser's box when it hit the wall.</param>
    /// <param name="direction">The way the laser was going.</param>
    internal void SpawnLaserWallFlare(Rectangle laserBounds, Direction8 direction) =>
        _laserWallFlares.Spawn(laserBounds, direction, Wall);

    /// <summary>Speeds up every grunt that is still alive, as each grunt's death does (notes §67).</summary>
    internal void SpeedUpGrunts() => _gruntSpeedProgression.SpeedUp(Entities.Grunts);

    /// <summary>Moves one entity on by a tick, unless it is still appearing. The arcade keeps the robots still until the whole appear sequence is done (notes §62).</summary>
    /// <param name="entity">The entity to move on.</param>
    /// <param name="gameTime">The time for this tick.</param>
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

    /// <summary>Asks for a sound that is heard from where its maker is on the playfield: something on the left is heard on the left.</summary>
    /// <param name="sound">The sound's table.</param>
    /// <param name="maker">The box of whatever made the sound.</param>
    internal void PlaySoundFrom(SoundSequence sound, Rectangle maker)
    {
        float pan = StereoPlacement.GetPan(maker, Wall.PlayfieldBounds);
        Sound.Play(sound, pan);
    }

    /// <summary>Plays the sounds that things make just by moving: a bounce for each tank shell that bounced, and one step for all the grunts together if any of them moved.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRTK4.ASM</c> for the shell's bounce (<c>SRBSND</c>) and <c>RRP8.ASM</c>
    /// <c>ROBX</c> for the grunts' step (<c>RMVSND</c>)</item>
    /// <item>Disassembly: <c>$4FCD</c> for the bounce</item>
    /// </list>
    /// </remarks>
    private void PlayShellAndGruntMovementSounds()
    {
        foreach (TankShell shell in Entities.TankShells)
        {
            if (shell.BouncedThisUpdate)
            {
                PlaySoundFrom(SoundTables.ShellRebound, shell.GetBounds());
            }
        }

        if (Entities.HasGruntStepped())
        {
            Sound.Play(SoundTables.RobotMove);
        }
    }

    /// <summary>Gets how many ROM frames after the wave is set up the game goes live (test hook).</summary>
    /// <returns>The ROM frames.</returns>
    internal int GetLiveRomFrames() => _waveStart.LiveRomFrames;

    /// <summary>Gets how many ROM frames after the wave is set up the player appears (test hook).</summary>
    /// <returns>The ROM frames.</returns>
    internal int GetPlayerAppearRomFrames() => _waveStart.PlayerAppearRomFrames;

    /// <summary>Says whether the player has started to appear, so that the strips of its appear effect are on the screen (test hook).</summary>
    internal bool HasPlayerAppeared() => _waveStart.HasPlayerAppeared();

    /// <summary>Says whether the player's own sprite is drawn. It is not until the game is live.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRS22.ASM</c> <c>PLA0</c>, <c>BITA #$10</c>, with <c>STATUS</c> held at
    /// <c>$19</c> until <c>PLS2</c>.</item>
    /// <item>Disassembly: the status byte at <c>$59</c>.</item>
    /// </list>
    /// </remarks>
    internal bool IsPlayerDrawn() => IsLive();

    /// <summary>Makes the game live at once and leaves the player's appear effect out, for a test of something that happens in play and not at the start of a wave (test hook).</summary>
    internal void SkipWaveStart()
    {
        _waveStart.SkipToLive();
        _playerAppear.Skip();
    }

    /// <summary>Works out how long a robot waits before its first move, counted from the start of the tick on which the game goes live.</summary>
    /// <param name="pollRomFrames">How many ROM frames the robot sleeps between one look at whether the game is live and the next.</param>
    /// <param name="napRomFrames">How many ROM frames the robot sleeps after the look that finds the game live, before its first move.</param>
    /// <returns>The clock units from the start of this tick to the robot's first move.</returns>
    internal int GetClockUnitsToFirstBeat(int pollRomFrames, int napRomFrames) => _waveStart.GetClockUnitsToFirstBeat(pollRomFrames, napRomFrames);

    /// <summary>Lists the robots that the arcade keeps on its robot list, in the order its appear loop meets them. The arcade puts each new robot at the head of the list, so the loop meets the kind that was set up last first, and within a kind the robot that was made last first.</summary>
    /// <returns>The robots, from the first the loop meets to the last.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRS22.ASM</c> <c>GETRBV</c> (<c>LDD RPTR / STX RPTR</c>), and
    /// <c>RRG23.ASM</c> <c>APPEAR</c>, which walks <c>RPTR</c>.</item>
    /// <item>Disassembly: the list at <c>$9821</c>.</item>
    /// </list>
    /// </remarks>
    private IEnumerable<IEntity> GetRobotsInAppearOrder() =>
        RobotKinds.All
            .Where(kind => kind.IsOnRobotList())
            .OrderByDescending(kind => kind.RobotListSetUpOrder)
            .SelectMany(kind => Entities.GetEntities(kind.Kind).Reverse());

    /// <summary>Tells every robot that was on the field at the start of the wave that the game has gone live, so that each sets the time of its first move.</summary>
    private void TellRobotsTheGameIsLive()
    {
        foreach (IWaveStartRobot robot in GetRobotsInAppearOrder().OfType<IWaveStartRobot>())
        {
            robot.BeginPlay(this);
        }
    }

    /// <summary>Runs every collision rule in the arcade's order, then freezes the game for a moment if the player has just been killed. Nothing collides until the game is live.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>PLS2</c>, which makes the collision process (<c>MAKP
    /// COLCHK</c>) only as the game goes live.</item>
    /// <item>Disassembly: <c>$2895</c>.</item>
    /// </list>
    /// </remarks>
    private void ResolveCollisions()
    {
        if (!IsLive())
        {
            return;
        }

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
            PlaySoundFrom(SoundTables.PlayerDeath, Player.GetBounds());
        }
    }
}
