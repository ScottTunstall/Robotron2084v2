using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A tank is a slow, heavily armoured robot dropped by quarks. It rolls around and fires shells at you. It acts on a beat. The <see cref="PlayField"/> calls <see cref="Update"/> on nearly every tick, through <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. <see cref="_beatTimer"/> gathers the ticks until it is time for the next beat (see <see cref="ArcadeClock"/>). <see cref="_growTimer"/> times the stages of its birth.</summary>
/// <seealso cref="Quark"/>
/// <seealso cref="TankShell"/>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRTK4.ASM</c>, routine <c>TANK</c></item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>ANIMATE_TANK</c> (<c>$4D10</c>-ish, near <c>$4D55</c>'s fire-delay check)</item>
/// </list>
/// </remarks>
public sealed class Tank : IExplodable, IRemovable, IWaveStartRobot
{
    /// <summary>How many ROM frames the arcade's routine sleeps between one look at whether the game is live and the next. It sets how long after the game goes live the first beat comes (<see cref="BeginPlay"/>).</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TANK</c>, <c>BITA #$7F / BEQ TANKL / NAP 15,TANK</c>, which runs on into its first beat with no more sleep. Disassembly: <c>$4D8B</c>.</remarks>
    private const int LivePollRomFrames = 15;

    /// <summary>ROM <c>ANIMATE_TANK</c>: ...and a roll at or below this aims at the player (about 38%).</summary>
    private const int AimAtPlayerRollAtMost = 96;

    /// <summary>ROM <c>TANKND</c>: ...and before this many (exclusive bound of the random roll).</summary>
    private const int AimIntervalMaxExclusiveBeats = 32;

    /// <summary>ROM <c>TANKND</c>: a re-aim comes after at least this many beats.</summary>
    private const int AimIntervalMinBeats = 1;

    /// <summary>The wave's tank fire interval when the caller gives none.</summary>
    private const int DefaultFireIntervalBeats = 32;

    /// <summary>ROM <c>ANIMATE_TANK</c>: the destination roll has this many sides...</summary>
    private const int DestinationRollSides = 256;

    /// <summary>ROM <c>TNKSHT</c>: the first shot waits the interval plus a random count of beats below this.</summary>
    private const int FirstShotExtraBeatsMaxExclusive = 32;

    /// <summary>
    /// A tank moves vertically only when its target is more than this many arcade pixels off.
    /// </summary>
    /// <remarks>ROM $4E11.</remarks>
    private const int VerticalMoveThresholdArcadePixels = 16;

    /// <summary>Collision box = the ROM sprite dimensions (14x16 arcade px), top-left anchored at <see cref="Position"/>.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.TankCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.TankCollisionSize.Height));

    private static readonly int VerticalMoveThreshold = ScreenSize.ToPortPixels(VerticalMoveThresholdArcadePixels);
    private readonly int _fireIntervalBeats;
    private readonly Random _random;
    private readonly SpriteSet _sprites;
    private int _aimBeatsRemaining;

    /// <summary>Counts up to the next beat.</summary>
    private int _beatTimer;

    private IntVector2 _destination;
    private int _fireCooldownBeats;

    /// <summary>Which grow (birth) animation frame is showing, 0..TankGrowSteps.</summary>
    /// <remarks>ROM: <c>MTANK</c> birth.</remarks>
    private int _growStep;

    /// <summary>Counts up to the next grow (birth) animation frame.</summary>
    private int _growTimer;

    private IntVector2 _position;
    private IntVector2 _stepDirection;

    // current 8-way/0 step vector (±1/0 components)
    /// <summary>Counts the tread animation frames shown, one per beat.</summary>
    /// <remarks>ROM: <c>TANK3</c> advances the animation frame once per beat.</remarks>
    private int _treadAnimationFrameCounter;

    /// <summary>Creates a tank at <paramref name="position"/>; it must be born before it can move or fire.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the tank.</param>
    /// <param name="random">The random source: the aim rolls, the destinations and the first-fire delay.</param>
    /// <param name="fireIntervalBeats">The beats between this wave's tank shots.</param>
    /// <param name="startFullyGrown">True for a tank put on at the start of a life, which does not grow first.</param>
    /// <remarks>ROM: <c>TNKSHT</c> — this wave's firing interval. <c>TNKSTV</c> puts a tank on at full size; only a dropped one grows (<c>MTANK</c>).</remarks>
    public Tank(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int fireIntervalBeats = DefaultFireIntervalBeats,
        bool startFullyGrown = false)
    {
        _growStep = startFullyGrown ? TankTuning.GrowSteps : 0;
        _sprites = sprites;
        _position = position;
        _random = random;
        _fireIntervalBeats = fireIntervalBeats;
        _destination = position;
        // The first beat fires and moves before the re-aim check (ROM: TANKL).
        _aimBeatsRemaining = 0;
        // The first shot waits the interval plus a random 0..31 beats.
        _fireCooldownBeats = fireIntervalBeats + random.Next(0, FirstShotExtraBeatsMaxExclusive);
    }

    /// <summary>The collision box: the current birth animation frame's size while being born, else the tank's.</summary>
    public Rectangle Bounds
    {
        get
        {
            if (_growStep < TankTuning.GrowSteps)
            {
                (int w, int h) = TankTuning.GrowSizes[_growStep];
                return new Rectangle(_position.X, _position.Y, ScreenSize.ToPortPixels(w), ScreenSize.ToPortPixels(h));
            }

            return new Rectangle(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);
        }
    }

    /// <summary>The frame an explosion would copy (see <see cref="IAnimationFrameSource"/>): the birth animation frame while being born, else the tread frame.</summary>
    /// <remarks>The walk frame advances once per beat and plays backwards while moving left
    /// (ROM: TANK3 takes the direction from the X step's sign).</remarks>
    public Texture2D GetCurrentAnimationFrame()
    {
        if (_growStep < TankTuning.GrowSteps)
        {
            return _sprites.TankGrowAnimationFrames[_growStep];
        }

        return _sprites.TankAnimationFrames[TreadFrameIndex];
    }

    /// <summary>Alive until shot or until it walks into an electrode; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Top-left of the tank (the ROM's OBJX/OBJY).</summary>
    public IntVector2 Position => _position;

    /// <summary>Moves a spot so that a tank standing on it is wholly inside the playfield.</summary>
    /// <param name="playfieldBounds">The edges of the playfield.</param>
    /// <param name="position">Where the tank would stand, which may be against or past a wall.</param>
    /// <returns>The nearest spot at which the tank's box fits.</returns>
    internal static IntVector2 GetPositionInside(Rectangle playfieldBounds, IntVector2 position) => new(
        Math.Clamp(position.X, playfieldBounds.X, playfieldBounds.Right - CollisionSize.Width),
        Math.Clamp(position.Y, playfieldBounds.Y, playfieldBounds.Bottom - CollisionSize.Height));

    /// <summary>True while the ROM birth sequence is still playing (test hook).</summary>
    internal bool IsBeingBorn => _growStep < TankTuning.GrowSteps;

    /// <summary>Which tread animation frame is showing: the index into <see cref="SpriteSet.TankAnimationFrames"/>.</summary>
    /// <remarks>Playing backwards while the tank moves left is the ROM's own rule: TANK3 takes the
    /// direction from the X step's sign.</remarks>
    internal int TreadFrameIndex
    {
        get
        {
            int animationFrameCount = _sprites.TankAnimationFrames.Length;
            int forward = _treadAnimationFrameCounter % animationFrameCount;
            return _stepDirection.X < 0 ? animationFrameCount - 1 - forward : forward;
        }
    }

    /// <summary>How many timer units one grow step takes (a tick adds 5; an arcade frame is 6 units).</summary>
    private static readonly int GrowIntervalClockUnits = ArcadeClock.ToClockUnits(TankTuning.GrowIntervalRomFrames);

    /// <summary>Draws the birth animation frames while being born, else the tread frame.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        // While being born it draws the ROM's own mini-tank sprites, each at its own size
        // anchored at the object's position (ROM: MTNKP1..4) — not a scaled copy of the tank.
        if (_growStep < TankTuning.GrowSteps)
        {
            (int growWidth, int growHeight) = TankTuning.GrowSizes[_growStep];
            Rectangle growBounds = new(
                _position.X,
                _position.Y,
                ScreenSize.ToPortPixels(growWidth),
                ScreenSize.ToPortPixels(growHeight));
            _sprites.Blitter.DrawSprite(spriteBatch, _sprites.TankGrowAnimationFrames[_growStep], growBounds, Color.White);
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), Bounds, Color.White);
    }

    /// <summary>Kills the tank outright: no death animation.</summary>
    /// <remarks>ROM: RRTK4.ASM's <c>TNKIL</c>.</remarks>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Sets the time of the first beat of a tank that was on the field when the wave started, on the tick the game goes live. The beat timer is set so that it comes due when that time has gone by.</summary>
    /// <param name="field">The playfield, which works out how long the wait is.</param>
    public void BeginPlay(PlayField field) =>
        _beatTimer = ArcadeClock.ToClockUnits(TankTuning.BeatIntervalRomFrames) - field.GetClockUnitsToFirstBeat(LivePollRomFrames, napRomFrames: 0);

    /// <summary>Runs the birth animation, then the beat: fire, move, animate and re-aim.</summary>
    /// <param name="gameTime">Unused — the clocks are counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive())
        {
            return;
        }

        if (field.RobotsFrozen)
        {
            return;
        }

        if (IsGrowing())
        {
            return;
        }

        // This wave's tank speed (2 vblanks) plus the 1 frame the process takes (ROM: TNKSPD).
        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < ArcadeClock.ToClockUnits(TankTuning.BeatIntervalRomFrames))
        {
            return;
        }

        _beatTimer -= ArcadeClock.ToClockUnits(TankTuning.BeatIntervalRomFrames);

        // One beat, always in this order (ROM: the TANK process).
        FireIfDue(field);
        StepOrBounce(field);

        _treadAnimationFrameCounter++; // one walk frame per beat (ROM: `TANK3` advances the animation frame once)

        // The re-aim timer counts down in beats; a blocked step already turned the tank (ROM: TANKND).
        if (--_aimBeatsRemaining <= 0)
        {
            _aimBeatsRemaining = NextAimInterval(_random);
            PickDestination(field);
        }
    }

    /// <summary>The next re-aim interval: a random count of beats.</summary>
    /// <remarks>ROM: <c>TANKND</c>.</remarks>
    private static int NextAimInterval(Random random) => random.Next(AimIntervalMinBeats, AimIntervalMaxExclusiveBeats);

    /// <summary>Runs the mini-tank grow-up; true while the tank is still growing and must not act.</summary>
    /// <remarks>ROM <c>MTANK</c> ("MINI TANK GROW"): four mini-tank animation frames, one per 12 ROM frames.</remarks>
    private bool IsGrowing()
    {
        if (_growStep >= TankTuning.GrowSteps)
        {
            return false;
        }

        _growTimer += ArcadeClock.UnitsPerPortTick;
        if (_growTimer < GrowIntervalClockUnits)
        {
            return true;
        }

        _growTimer -= GrowIntervalClockUnits;

        // MTANK applies the current animation frame's (dx,dy) before advancing, so the mini tank
        // walks up-left and the full tank lands centred on the drop point (notes §53).
        (int columns, int rows) = TankTuning.GrowDeltas[_growStep];
        _position += new IntVector2(ScreenSize.ToPortPixelsFromColumns(columns), ScreenSize.ToPortPixels(rows));

        return ++_growStep < TankTuning.GrowSteps;
    }

    /// <summary>Counts the fire cooldown down one beat and fires a shell at the player when it runs out.</summary>
    /// <param name="field">The playfield.</param>
    private void FireIfDue(PlayField field)
    {
        if (--_fireCooldownBeats > 0)
        {
            return;
        }

        if (field.CanFireShell())
        {
            field.SpawnTankShell(_position);
        }

        // Later shots reload to exactly this wave's interval — no random padding.
        _fireCooldownBeats = _fireIntervalBeats;
    }

    /// <summary>Takes one step, or mirrors the blocked axis when the step would hit the wall.</summary>
    /// <param name="field">The playfield.</param>
    /// <remarks>ROM: the <c>TANK1</c> step, one arcade pixel on each active axis per beat.</remarks>
    private void StepOrBounce(PlayField field)
    {
        int stepPixels = ScreenSize.ToPortPixels(TankTuning.StepArcadePixels);
        IntVector2 step = new(_stepDirection.X * stepPixels, _stepDirection.Y * stepPixels);
        IntVector2 next = _position + step;
        if (!field.HitsWall(new Rectangle(next.X, next.Y, CollisionSize.Width, CollisionSize.Height)))
        {
            _position = next;
            return;
        }

        // Bounce off the wall (never crosses): mirror the axis that is blocked.
        if (field.HitsWall(new Rectangle(_position.X + step.X, _position.Y, CollisionSize.Width, CollisionSize.Height)))
        {
            _stepDirection = new IntVector2(-_stepDirection.X, _stepDirection.Y);
            step = new IntVector2(-step.X, step.Y);
        }

        if (field.HitsWall(new Rectangle(_position.X, _position.Y + step.Y, CollisionSize.Width, CollisionSize.Height)))
        {
            _stepDirection = new IntVector2(_stepDirection.X, -_stepDirection.Y);
        }
    }

    /// <summary>Picks the next destination: the player about 38% of the time, else a random point.</summary>
    /// <remarks>ROM: <c>ANIMATE_TANK</c>.</remarks>
    private void PickDestination(PlayField field)
    {
        _destination = _random.Next(DestinationRollSides) <= AimAtPlayerRollAtMost
            ? field.Player.Position
            : PickRandomPointIn(field);

        int dx = Math.Sign(_destination.X - _position.X);
        int dy = Math.Abs(_destination.Y - _position.Y) > VerticalMoveThreshold
            ? Math.Sign(_destination.Y - _position.Y)
            : 0;
        _stepDirection = new IntVector2(dx, dy);
    }

    /// <summary>A random point inside the playfield, inset by the tank's own box.</summary>
    private IntVector2 PickRandomPointIn(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        return new IntVector2(
            _random.Next(bounds.X + CollisionSize.Width, bounds.Right - CollisionSize.Width + 1),
            _random.Next(bounds.Y + CollisionSize.Height, bounds.Bottom - CollisionSize.Height + 1));
    }
}
