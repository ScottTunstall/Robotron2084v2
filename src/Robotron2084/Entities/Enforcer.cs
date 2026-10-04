using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>An enforcer is a small, fast robot dropped by spheroids. It grows in place for a moment, then flies around firing sparks at you. It acts on a beat. The <see cref="PlayField"/> calls <see cref="Update"/> on nearly every tick, through <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. <see cref="_beatTimer"/> gathers the ticks until it is time for the next beat (see <see cref="ArcadeClock"/>). It also moves every ROM frame, timed by <see cref="_moveTimer"/>, and <see cref="_growClockUnitsRemaining"/> counts down its growing.</summary>
/// <seealso cref="Spheroid"/>
/// <seealso cref="Spark"/>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRC11.ASM</c>, routine <c>ENFR1</c> (with <c>ENFNV</c>, <c>ENFDRP</c> sub-blocks)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>ENFORCER_AI</c> (<c>$1951</c> area), with spawning in <c>DROP_ENFORCER</c>/<c>CREATE_ENFORCER</c></item>
/// </list>
/// </remarks>
public sealed class Enforcer : IEntity, IExplodable, IRemovable
{
    /// <summary>ROM <c>ENFR1</c>: the aim zone down-right of the player is this many columns wide...</summary>
    private const int AimZoneColumns = 32;

    /// <summary>ROM <c>ENFR1</c>: ...and this many arcade rows tall.</summary>
    private const int AimZoneRows = 32;

    /// <summary>The remaining distance to its aim is divided by this to give how far an enforcer moves each ROM frame. The result sets <see cref="_velocitySubpixels"/>.</summary>
    private const int ApproachDivisor = 2;

    /// <summary>The wave's enforcer fire interval when the caller gives none.</summary>
    private const int DefaultFireIntervalBeats = 24;

    /// <summary>ROM <c>ENFR1</c>: a re-aim countdown is a random 0 to one less than this many beats.</summary>
    private const int ReaimBeatsMaxExclusive = 32;

    /// <summary>The enforcer sprite's own 10x11 arcade px box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.EnforcerCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.EnforcerCollisionSize.Height));

    private readonly int _fireIntervalBeats;
    private readonly Random _random;
    private readonly SpriteSet _sprites;
    private int _beatTimer;
    private int _fireCooldownBeats;
    private int _growClockUnitsRemaining;
    private int _moveTimer;
    private IntVector2 _position;

    // Counts up to the next move: one per ROM frame
    private int _reaimBeatsRemaining;

    private IntVector2 _remainderSubpixels;

    /// <summary>Velocity in 1/256 port units per ROM frame, carried by a remainder.</summary>
    private IntVector2 _velocitySubpixels;

    /// <summary>Creates an enforcer; it is immobile until it has grown.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the enforcer.</param>
    /// <param name="random">The random source for the re-aim destination and the fire timer.</param>
    /// <param name="fireIntervalBeats">The most beats this wave's enforcer waits between shots: the interval is a random 1..this.</param>
    /// <remarks>ROM: <c>ENSTIM</c> — the interval is a random 1..that many AI passes.</remarks>
    public Enforcer(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int fireIntervalBeats = DefaultFireIntervalBeats)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _fireIntervalBeats = fireIntervalBeats;
        _growClockUnitsRemaining = ArcadeClock.ToClockUnits(EnforcerTuning.GrowUpRomFrames);
        // Both countdowns are in beats; the arcade re-aims as soon as the grow-up ends.
        _reaimBeatsRemaining = 0;
        _fireCooldownBeats = 1 + random.Next(0, _fireIntervalBeats);
        // The mover starts on its first active frame; the accumulator is still while growing.
        _moveTimer = ArcadeClock.UnitsPerRomFrame;
    }

    /// <summary>The enforcer sprite's own 10x11 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>The animation frame on screen: a grow-up frame while it grows, else the full animation frame.</summary>
    /// <remarks>The grow frames are the ROM's ENGD1..5, which are frames 2..6 (1-based) of the set.</remarks>
    public Texture2D GetCurrentAnimationFrame()
    {
        if (!this.IsAlive() || _growClockUnitsRemaining <= 0)
        {
            return _sprites.EnforcerSprite;
        }

        int frame = Math.Clamp(GrowAnimationFrameIndex, 0, _sprites.EnforcerAnimationFrames.Length - 2);
        return _sprites.EnforcerAnimationFrames[1 + frame];
    }

    /// <summary>Alive until killed; never Dying — there is no death animation.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Top-left of the enforcer.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>Which of the five grow-up animation frames is showing (0..4), or -1 once grown (test hook).</summary>
    internal int GrowAnimationFrameIndex => _growClockUnitsRemaining > 0
        ? (ArcadeClock.ToClockUnits(EnforcerTuning.GrowUpRomFrames) - _growClockUnitsRemaining)
            / ArcadeClock.ToClockUnits(EnforcerTuning.GrowStepRomFrames)
        : -1;

    /// <summary>Draws the grow-up animation frame while it is growing, and the full animation frame afterwards.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), Bounds, Color.White);
    }

    /// <summary>Kills it at once (ROM <c>ENFKIL</c>); there is no death animation.</summary>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Runs the grow-up, the per-frame mover and the AI beat.</summary>
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

        // Grow-up: immobile and silent; the carry keeps the animation frames on the ROM's 9-frame mark.
        if (_growClockUnitsRemaining > 0)
        {
            _growClockUnitsRemaining -= ArcadeClock.UnitsPerPortTick;
            if (_growClockUnitsRemaining > 0)
            {
                return;
            }
        }

        // One velocity integration per ROM frame.
        _moveTimer += ArcadeClock.UnitsPerPortTick;
        if (_moveTimer >= ArcadeClock.UnitsPerRomFrame)
        {
            _moveTimer -= ArcadeClock.UnitsPerRomFrame;
            AdvancePosition(field);
        }

        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < ArcadeClock.ToClockUnits(EnforcerTuning.BeatIntervalRomFrames))
        {
            return;
        }

        _beatTimer -= ArcadeClock.ToClockUnits(EnforcerTuning.BeatIntervalRomFrames);

        // Both countdowns tick once per beat (ROM: ENFR1).
        if (--_reaimBeatsRemaining <= 0)
        {
            _reaimBeatsRemaining = NextReaimBeats(_random);
            RollVelocity(field);
        }

        if (--_fireCooldownBeats <= 0)
        {
            _fireCooldownBeats = NextFireBeats(_random);
            if (field.GetActiveSparkCount() < SparkTuning.GlobalActiveSparkCap)
            {
                // The ROM aims with the player's position, so velocity follows distance per axis.
                field.SpawnSpark(_position, field.Player.Position);
            }
        }
    }

    /// <summary>Rolls the re-aim countdown: 0..31 beats (0 re-aims again next beat).</summary>
    private static int NextReaimBeats(Random random) => random.Next(0, ReaimBeatsMaxExclusive);

    /// <summary>Moves one frame's worth of velocity; an axis that would leave the field is refused.</summary>
    /// <param name="field">The playfield wall.</param>
    /// <remarks>The ROM's generic mover (RRS22.ASM's <c>OPB80</c>).</remarks>
    private void AdvancePosition(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        _remainderSubpixels += _velocitySubpixels;
        int stepX = _remainderSubpixels.X / ScreenSize.SubpixelsPerPixel;
        int stepY = _remainderSubpixels.Y / ScreenSize.SubpixelsPerPixel;
        _remainderSubpixels -= new IntVector2(stepX * ScreenSize.SubpixelsPerPixel, stepY * ScreenSize.SubpixelsPerPixel);

        int x = _position.X + stepX;
        if (stepX != 0 && x >= bounds.X && x + CollisionSize.Width <= bounds.Right)
        {
            _position = _position with { X = x };
        }

        int y = _position.Y + stepY;
        if (stepY != 0 && y >= bounds.Y && y + CollisionSize.Height <= bounds.Bottom)
        {
            _position = _position with { Y = y };
        }
    }

    /// <summary>The interval until the next shot: 1..the wave's fire delay, in beats.</summary>
    private int NextFireBeats(Random random) => random.Next(1, _fireIntervalBeats + 1);

    /// <summary>Picks the next destination — the player plus a random offset — and sets the velocity.</summary>
    /// <param name="field">The playfield: the player to aim past, and the wall to clamp to.</param>
    private void RollVelocity(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        int targetX = field.PlayerPosition.X + ScreenSize.ToPortPixelsFromColumns(_random.Next(0, AimZoneColumns));
        int targetY = field.PlayerPosition.Y + ScreenSize.ToPortPixels(_random.Next(0, AimZoneRows));
        targetX = Math.Clamp(targetX, bounds.X, bounds.Right - CollisionSize.Width);
        targetY = Math.Clamp(targetY, bounds.Y, bounds.Bottom - CollisionSize.Height);

        IntVector2 delta = new(targetX - _position.X, targetY - _position.Y);

        // Velocity is in 1/256-port-px units per ROM frame.
        _velocitySubpixels = new IntVector2(delta.X / ApproachDivisor, delta.Y / ApproachDivisor);
    }
}
