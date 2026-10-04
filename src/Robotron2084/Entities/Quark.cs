using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A quark is a drifting robot that wanders the screen dropping tanks, then flees off the edge once it has dropped enough. It acts on a beat. The <see cref="PlayField"/> calls <see cref="Update"/> on nearly every tick, through <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. <see cref="_beatTimer"/> gathers the ticks until it is time for the next beat (see <see cref="ArcadeClock"/>). It also moves every ROM frame, timed by <see cref="_moveTimer"/>.</summary>
/// <seealso cref="Tank"/>
/// <seealso cref="StripEffect"/>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRTK4.ASM</c>, routine <c>SQUARE</c> (with <c>SQVEL</c>/<c>SQ3</c>)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$4BFB</c> (<c>ANIMATE_QUARK</c>)</item>
/// </list>
/// </remarks>
public sealed class Quark : IEntity, IAnimationFrameSource, IRemovable
{
    /// <summary>Sides of the ROM's coin flips (the drift signs and the flee direction).</summary>
    private const int CoinFlipSides = 2;

    /// <summary>The wave's tank-drop delay when the caller gives none.</summary>
    private const int DefaultDropDelayBeats = 12;

    /// <summary>The wave's tank-allotment bound when the caller gives none.</summary>
    private const int DefaultMaxDropsX2 = 10;

    /// <summary>The wave's drift-speed cap when the caller gives none.</summary>
    private const int DefaultSpeedCap = 50;

    /// <summary>After a quark has dropped a tank, its drop delay is divided by this to give the most beats it waits before the next drop. A random number up to that is stored in <see cref="_dropBeatsRemaining"/>.</summary>
    private const int RepeatDropDelayDivisor = 2;

    /// <summary>Collision box = the ROM sprite dimensions (16x15 arcade px), top-left anchored at <see cref="Position"/>.</summary>
    private static readonly (int Width, int Height) CollisionSize = (ScreenSize.ToPortPixels(CollisionSizes.QuarkCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.QuarkCollisionSize.Height));

    private readonly int _dropDelayBeats;
    private readonly Random _random;
    private readonly int _speedCap;
    private readonly SpriteSet _sprites;

    /// <summary>Which rotation animation frame is showing.</summary>
    /// <remarks>ROM: <c>OPICT</c>, animation frames <c>SQP0</c> to <c>SQP8</c>.</remarks>
    private int _animationFrameIndex;

    /// <summary>Counts up to the next beat.</summary>
    private int _beatTimer;

    /// <summary>Beats left before the next tank drop is due.</summary>
    /// <remarks>ROM: <c>PD2</c>.</remarks>
    private int _dropBeatsRemaining;

    /// <summary>True once the quark has started dropping tanks. It stays true until every tank has been dropped.</summary>
    private bool _isDroppingTanks;

    /// <summary>True once the quark is running for the nearest edge of the playfield, where it vanishes.</summary>
    private bool _isFleeing;

    /// <summary>Counts up to the next move: one move per ROM frame.</summary>
    private int _moveTimer;

    private IntVector2 _position;

    /// <summary>Beats left before the quark picks a new direction to drift in.</summary>
    /// <remarks>ROM: <c>PD7</c>.</remarks>
    private int _reaimBeatsRemaining;

    /// <summary>Sub-pixel carry, so a slow drift still accumulates into movement.</summary>
    private IntVector2 _remainderSubpixels;

    /// <summary>How many tanks this quark still has left to drop.</summary>
    private int _tanksRemaining;

    /// <summary>Velocity in 1/256 port px per ROM frame, integrated once per frame by the shared mover.</summary>
    private IntVector2 _velocitySubpixels;

    /// <summary>Drops a quark at <paramref name="position"/> with its tank allotment and first drift already rolled.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the quark.</param>
    /// <param name="random">The random source: the allotment, the drift rolls and the flee direction.</param>
    /// <param name="maxDropsX2">This wave's tank-allotment bound; the roll happens here.</param>
    /// <param name="dropDelayBeats">The most beats this wave's quark waits between tank drops.</param>
    /// <param name="speedCap">This wave's drift-speed cap, the velocity roll's maximum (ROM <c>SQSPD</c>).</param>
    /// <remarks>ROM: <c>ENFNUM</c>, <c>TDPTIM</c> and <c>SQSPD</c> — this wave's allotment bound,
    /// drop delay and drift-speed cap.</remarks>
    public Quark(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int maxDropsX2 = DefaultMaxDropsX2,
        int dropDelayBeats = DefaultDropDelayBeats,
        int speedCap = DefaultSpeedCap)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _dropDelayBeats = dropDelayBeats;
        _speedCap = speedCap;
        // The tank allotment: half a random roll up to the wave's cap, rounded up (ROM: PD3).
        int roll = random.Next(maxDropsX2 + 1);
        _tanksRemaining = (roll + 1) / 2;
        // The first drop's delay, counted in animation cycles rather than beats (ROM: PD2).
        _dropBeatsRemaining = 1 + random.Next(dropDelayBeats);
        // A quark drifts and animates from the instant it exists, so both clocks start pre-loaded.
        _beatTimer = BeatIntervalClockUnits;
        _moveTimer = ArcadeClock.UnitsPerRomFrame;
    }

    /// <summary>Picks where a quark starts a wave: anywhere along the top wall or the bottom wall.</summary>
    /// <param name="playfieldBounds">The edges of the playfield.</param>
    /// <param name="random">Where the sideways spot and the top-or-bottom choice come from.</param>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SQST1</c> and <c>SQST2</c> (<c>YMIN+2</c> or <c>YMAX-14</c>, then a random X). Disassembly: <c>$4B48</c> to <c>$4B5A</c>.</remarks>
    internal static IntVector2 GetStartPosition(Rectangle playfieldBounds, Random random)
    {
        int x = random.Next(playfieldBounds.X, playfieldBounds.Right - CollisionSize.Width + 1);
        bool startsAtTop = random.Next(2) == 0;
        return new IntVector2(x, startsAtTop ? playfieldBounds.Y : playfieldBounds.Bottom - CollisionSize.Height);
    }

    /// <summary>The quark sprite's own 16x15 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>The current rotation frame, for the death burst (see <see cref="IAnimationFrameSource"/>).</summary>
    /// <returns>The texture for the current rotation frame.</returns>
    public Texture2D GetCurrentAnimationFrame() => _sprites.QuarkAnimationFrames[_animationFrameIndex];

    /// <summary>Alive until it is hit or flees off the field; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Top-left of the quark (the ROM's OBJX/OBJY).</summary>
    public IntVector2 Position => _position;

    /// <summary>How many clock units pass between one beat and the next. A quark's <see cref="_beatTimer"/> goes up by one port tick's worth of clock units each tick, and when it reaches this, a beat happens and this is subtracted from it.</summary>
    private static readonly int BeatIntervalClockUnits = ArcadeClock.ToClockUnits(QuarkTuning.BeatIntervalRomFrames);

    /// <summary>Draws the current rotation frame.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), Bounds, Color.White);
    }

    /// <summary>Kills the quark outright; a laser hit plays its own burst instead of the strip explosion.</summary>
    /// <remarks>ROM: RRTK4.ASM's <c>SQKIL</c> plays a bespoke shrink-and-burst, not a blink. <see cref="RobotKinds"/>
    /// wires <see cref="ScoreBurst.CreateForQuark"/> to the laser phase.</remarks>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Runs one beat: moves on the mover's clock, advances the rotation, re-rolls and drops.</summary>
    /// <param name="gameTime">Unused — the clocks are counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive())
        {
            return;
        }

        // Movement shares the per-frame mover clock, not the AI beat (see the remarks).
        _moveTimer += ArcadeClock.UnitsPerPortTick;
        if (_moveTimer >= ArcadeClock.UnitsPerRomFrame)
        {
            _moveTimer -= ArcadeClock.UnitsPerRomFrame;
            AdvancePosition(field);
        }

        // Counts up to the next beat: 5 per tick, 6 per arcade frame.
        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < BeatIntervalClockUnits)
        {
            return;
        }

        _beatTimer -= BeatIntervalClockUnits;
        AdvanceAnimation();

        if (_isFleeing)
        {
            LeaveWhenClearOfTheField(field);
            return;
        }

        if (--_reaimBeatsRemaining <= 0)
        {
            RollVelocity(field.PlayfieldBounds);
        }

        // Frozen: the quark still animates and re-rolls, but the tank-drop countdown is paused.
        if (field.RobotsFrozen)
        {
            return;
        }

        AdvanceTankDrop(field);
    }

    /// <summary>Advances the animation one animation frame per beat; the range depends on the phase.</summary>
    /// <remarks>ROM: SQP0..SQP4 while wandering, SQP0..SQP8 while dropping tanks, and SQP8..SQP0
    /// during the exit.</remarks>
    private void AdvanceAnimation()
    {
        if (_isFleeing)
        {
            _animationFrameIndex = _animationFrameIndex <= 0 ? QuarkTuning.TotalAnimationFrames - 1 : _animationFrameIndex - 1;
            return;
        }

        int last = _isDroppingTanks ? QuarkTuning.TotalAnimationFrames - 1 : QuarkTuning.TravelAnimationFrames - 1;
        _animationFrameIndex = _animationFrameIndex >= last ? 0 : _animationFrameIndex + 1;
    }

    /// <summary>Moves by the whole-pixel part of the velocity, carrying the fraction.</summary>
    private void AdvancePosition(PlayField field)
    {
        Rectangle bounds = field.PlayfieldBounds;

        _remainderSubpixels += _velocitySubpixels;
        int stepX = _remainderSubpixels.X / ScreenSize.SubpixelsPerPixel;
        int stepY = _remainderSubpixels.Y / ScreenSize.SubpixelsPerPixel;
        _remainderSubpixels -= new IntVector2(
            stepX * ScreenSize.SubpixelsPerPixel,
            stepY * ScreenSize.SubpixelsPerPixel);

        if (stepX != 0 && IsInsideX(bounds, _position.X + stepX))
        {
            _position = _position with { X = _position.X + stepX };
        }

        if (stepY != 0 && IsInsideY(bounds, _position.Y + stepY))
        {
            _position = _position with { Y = _position.Y + stepY };
        }
    }

    /// <summary>Counts the drop beat down, and drops a tank when it is due and the field allows another.</summary>
    /// <param name="field">The playfield, which owns the tank cap and the new tank.</param>
    /// <remarks>ROM: <c>SQ2</c>/<c>TNKDRP</c>. Before the first drop the countdown ticks once per animation
    /// cycle, after it once per beat. The drop phase is never left: when the allotment runs out the quark
    /// flees.</remarks>
    private void AdvanceTankDrop(PlayField field)
    {
        if (!_isDroppingTanks && _animationFrameIndex != 0)
        {
            return;
        }

        if (--_dropBeatsRemaining > 0)
        {
            return;
        }

        if (_tanksRemaining > 0 && field.CanDropTank())
        {
            _isDroppingTanks = true;
            _tanksRemaining--;
            // A new tank appears 2 columns right and 5-6 rows down — 6 when the quark is on the
            // top wall, where the ROM skips its `DECB` (ROM: TNKDRP).
            int rowOffset = _position.Y == field.PlayfieldBounds.Y
                ? TankTuning.BirthOffsetRowsOnTopWall
                : TankTuning.BirthOffsetRowsOffTopWall;
            field.SpawnTank(_position + new IntVector2(
                ScreenSize.ToPortPixelsFromColumns(TankTuning.BirthOffsetColumns),
                ScreenSize.ToPortPixels(rowOffset)));
            if (_tanksRemaining == 0)
            {
                StartFlee();
                return;
            }
        }

        // The next drop's delay is roughly half the first; it also runs when the cap blocked a drop.
        _dropBeatsRemaining = 1 + _random.Next((_dropDelayBeats / RepeatDropDelayDivisor) + 1);
    }

    /// <summary>One axis's magnitude: the roll (1..this wave's cap) times the axis scale, in subpixels.</summary>
    /// <remarks>The arcade addresses video memory as <c>column*256 + row</c>, so X counts 2-pixel
    /// columns and Y counts 1-pixel rows: the ROM's X scale of 4 and Y scale of 8 give the same
    /// on-screen speed once columns are halved. The mover integrates this once per ROM frame.</remarks>
    private int ComputeAxisVelocitySubpixels(int scale, bool isPositive, int coordinateUnitArcadePixels)
    {
        int roll = 1 + _random.Next(_speedCap);
        int subpixels = roll * scale * ScreenSize.ToPortPixels(coordinateUnitArcadePixels);
        return isPositive ? subpixels : -subpixels;
    }

    /// <summary>True when an X coordinate keeps the quark's whole box inside the playfield.</summary>
    private bool IsInsideX(Rectangle bounds, int x) =>
        x >= bounds.X && x + CollisionSize.Width <= bounds.Right;

    /// <summary>True when a Y coordinate keeps the quark's whole box inside the playfield.</summary>
    private bool IsInsideY(Rectangle bounds, int y) =>
        y >= bounds.Y && y + CollisionSize.Height <= bounds.Bottom;

    /// <summary>Leaves for good the moment the quark is fully off the top or bottom edge.</summary>
    /// <param name="field">The playfield, whose bounds the exit is measured against.</param>
    /// <remarks>ROM: <c>SQ3L</c>.</remarks>
    private void LeaveWhenClearOfTheField(PlayField field)
    {
        int topExitY = field.PlayfieldBounds.Y + ScreenSize.ToPortPixels(QuarkTuning.FleeExitLowRows);
        int bottomExitY = field.PlayfieldBounds.Bottom - ScreenSize.ToPortPixels(QuarkTuning.FleeExitHighRows);
        if (_position.Y <= topExitY || _position.Y >= bottomExitY)
        {
            LifeState = EntityLifeState.Dead;
        }
    }

    /// <summary>Rolls a fresh random speed per axis, biased away from the walls.</summary>
    private void RollVelocity(Rectangle bounds)
    {
        int lowX = bounds.X + ScreenSize.ToPortPixelsFromColumns(QuarkTuning.WallMarginLeftColumns);
        int highX = bounds.Right - ScreenSize.ToPortPixelsFromColumns(QuarkTuning.WallMarginRightColumns);
        int lowY = bounds.Y + ScreenSize.ToPortPixels(QuarkTuning.WallMarginTopRows);
        int highY = bounds.Bottom - ScreenSize.ToPortPixels(QuarkTuning.WallMarginBottomRows);

        bool xPositive = _position.X <= lowX || (_position.X < highX && _random.Next(CoinFlipSides) == 0);
        bool yPositive = _position.Y <= lowY || (_position.Y < highY && _random.Next(CoinFlipSides) != 0);

        _velocitySubpixels = new IntVector2(
            ComputeAxisVelocitySubpixels(QuarkTuning.VelocityXScale, xPositive, coordinateUnitArcadePixels: ScreenSize.ArcadePixelsPerColumn),
            ComputeAxisVelocitySubpixels(QuarkTuning.VelocityYScale, yPositive, coordinateUnitArcadePixels: 1));

        _reaimBeatsRemaining = 1 + _random.Next(QuarkTuning.ReaimMaxBeats);
    }

    /// <summary>Starts the exit run: X stops dead, Y becomes a fixed 2 arcade px per frame, from a coin flip up or down.</summary>
    /// <remarks>ROM: RRTK4.ASM's <c>SQ3</c>.</remarks>
    private void StartFlee()
    {
        _isFleeing = true;
        int subpixels = QuarkTuning.FleeVelocityRom * ScreenSize.ToPortPixels(1);
        _velocitySubpixels = new IntVector2(0, _random.Next(CoinFlipSides) == 0 ? subpixels : -subpixels);
        _remainderSubpixels = IntVector2.Zero;
    }
}
