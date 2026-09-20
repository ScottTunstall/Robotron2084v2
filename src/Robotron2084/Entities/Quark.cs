using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A quark — the drifting robot that drops tanks, then flees off the nearest edge.</summary>
/// <seealso cref="Tank"/>
/// <seealso cref="Explosion"/>
/// <remarks>ROM: RRTK4.ASM's <c>SQUARE</c> process — <c>SQVEL</c> rolls the drift each beat,
/// <c>SQ3</c> handles the flee-and-vanish exit (notes §51). It never seeks the player: every beat it
/// rolls a fresh random speed per axis, biased away from the nearest wall, and the shared per-frame
/// mover integrates it every ROM frame, so it glides rather than jumps. It flies straight over
/// electrodes and never bounces — reaching a wall just makes the next roll point inward. At spawn it
/// rolls its tank allotment (half a random roll, rounded up); the first drop waits a random delay and
/// each later one a shorter re-arm, and drops stop while 20 tanks are already in play. Once the
/// allotment is gone it flees off the nearest edge at a fixed speed and disappears; being hit instead
/// bursts it on the spot. Timers count 5 per tick and 6 per arcade frame, so an interval of N frames
/// is due at 6 x N.</remarks>
public sealed class Quark : IEntity, IArtSource
{
    /// <summary>Collision box = the ROM picture dimensions (16x15 arcade px), top-left anchored at <see cref="Position"/>.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.QuarkCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.QuarkCollisionSize.Height));
    private readonly Random _random;
    private readonly int _dropDelayRomTicks;
    private readonly int _quarkSpeedRom;
    private IntVector2 _position;

    /// <summary>Velocity in 1/256 port px per ROM frame, integrated once per frame by the shared mover.</summary>
    private IntVector2 _velocitySubpixels;

    /// <summary>Sub-pixel carry, so a slow drift still accumulates into movement.</summary>
    private IntVector2 _remainderSubpixels;

    private int _beatTimer;             // Counts up to the next beat.
    private int _moveTimer;            // Counts up to the next movement step: one per ROM frame.
    private int _reaimBeatsRemaining;   // Beats left before the quark rolls a fresh drift direction (ROM: PD7).
    private int _animationFrame;         // Current rotation picture index (ROM: OPICT, pictures SQP0..SQP8).
    private int _tanksRemaining;         // How many tanks this quark still has left to drop.
    private int _dropBeatsRemaining;    // Beats left before the next tank drop is due (ROM: PD2).
    private bool _droppingTanks;         // True once the quark has entered its drop phase; it never leaves it until its allotment is gone.
    private bool _fleeing;               // True once the quark is running for the nearest edge to vanish.

    /// <summary>Drops a quark at <paramref name="position"/> with its tank allotment and first drift already rolled.</summary>
    /// <param name="position">Top-left of the quark.</param>
    /// <param name="random">The random source: the allotment, the drift rolls and the flee direction.</param>
    /// <param name="maxDropsX2">This wave's tank-allotment bound; the roll happens here.</param>
    /// <param name="dropDelayRomTicks">This wave's drop delay, in ROM frames.</param>
    /// <param name="quarkSpeedRom">This wave's drift-speed upper bound (the velocity roll's maximum).</param>
    /// <remarks>ROM: <c>ENFNUM</c>, <c>TDPTIM</c> and <c>SQSPD</c> — this wave's allotment bound,
    /// drop delay and drift-speed cap.</remarks>
    public Quark(IntVector2 position, Random random, int maxDropsX2 = 10, int dropDelayRomTicks = 12, int quarkSpeedRom = 50)
    {
        _position = position;
        _random = random;
        _dropDelayRomTicks = dropDelayRomTicks;
        _quarkSpeedRom = quarkSpeedRom;
        // The tank allotment: half a random roll up to the wave's cap, rounded up (ROM: PD3).
        int roll = random.Next(maxDropsX2 + 1);
        _tanksRemaining = (roll + 1) / 2;
        // The first drop's delay, counted in animation cycles rather than beats (ROM: PD2).
        _dropBeatsRemaining = 1 + random.Next(dropDelayRomTicks);
        // A quark drifts and animates from the instant it exists, so both clocks start pre-loaded.
        _beatTimer = BeatPeriod;
        _moveTimer = 6;
    }

    /// <summary>How many timer units between beats (a tick adds 5; an arcade frame is 6 units).</summary>
    private static int BeatPeriod => GameplayConstants.QuarkBeatRomTicks * 6;

    /// <summary>Top-left of the quark (the ROM's OBJX/OBJY).</summary>
    public IntVector2 Position => _position;

    /// <summary>The quark picture's own 16x15 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until it is hit or flees off the field; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Kills the quark outright; the explosion is the whole visual.</summary>
    /// <remarks>ROM: RRTK4.ASM's <c>SQKIL</c> plays a bespoke shrink-and-burst, not a blink. That
    /// animation is not built yet, so the shared strip explosion stands in for it.</remarks>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>Runs one beat: moves on the mover's clock, advances the rotation, re-rolls and drops.</summary>
    /// <param name="gameTime">Unused — the clocks are counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        // Movement shares the per-frame mover clock, not the AI beat (see the remarks).
        _moveTimer += 5;
        if (_moveTimer >= 6)
        {
            _moveTimer -= 6;
            AdvancePosition(field);
        }

        // Counts up to the next beat: 5 per tick, 6 per arcade frame.
        _beatTimer += 5;
        if (_beatTimer < BeatPeriod)
        {
            return;
        }

        _beatTimer -= BeatPeriod;
        AdvanceAnimation();

        if (_fleeing)
        {
            // Once fleeing, it dies the moment it is fully off the top or bottom edge (ROM: SQ3L).
            int low = field.Wall.PlayfieldBounds.Y + ScreenSize.Scaled(GameplayConstants.QuarkFleeExitLowArcadePixels);
            int high = field.Wall.PlayfieldBounds.Bottom - ScreenSize.Scaled(GameplayConstants.QuarkFleeExitHighArcadePixels);
            if (_position.Y <= low || _position.Y >= high)
            {
                LifeState = EntityLifeState.Dead;
            }

            return;
        }

        if (--_reaimBeatsRemaining <= 0)
        {
            RollVelocity(field.Wall.PlayfieldBounds);
        }

        // Frozen: the quark still animates and re-rolls, but the tank-drop countdown is paused.
        if (field.RobotsFrozen)
        {
            return;
        }

        // Before the first drop the countdown ticks once per animation cycle; after, per beat.
        if (!_droppingTanks && _animationFrame != 0)
        {
            return;
        }

        // The drop phase is never left: when the allotment runs out the quark flees (ROM: SQ2).
        if (--_dropBeatsRemaining > 0)
        {
            return;
        }

        if (_tanksRemaining > 0 && field.CanDropTank)
        {
            _droppingTanks = true;
            _tanksRemaining--;
            // A new tank appears 4 arcade px right and down, with a smaller Y offset when the
            // quark is on the top wall so it does not spawn inside it (ROM: TNKDRP).
            int rowOffset = _position.Y == field.Wall.PlayfieldBounds.Y
                ? GameplayConstants.TankBirthOffsetY
                : GameplayConstants.TankBirthOffsetYOffTopWall;
            field.SpawnTank(_position + new IntVector2(GameplayConstants.TankBirthOffsetX, rowOffset));
            if (_tanksRemaining == 0)
            {
                StartFlee();
                return;
            }
        }

        // The next drop's delay is roughly half the first; it also runs when the cap blocked a drop.
        _dropBeatsRemaining = 1 + _random.Next((_dropDelayRomTicks >> 1) + 1);
    }

    /// <summary>Rolls a fresh random speed per axis, biased away from the walls.</summary>
    private void RollVelocity(Rectangle bounds)
    {
        int lowX = bounds.X + ScreenSize.Scaled(GameplayConstants.QuarkWallMarginLowArcadePixels);
        int highX = bounds.Right - ScreenSize.Scaled(GameplayConstants.QuarkWallMarginRightArcadePixels);
        int lowY = bounds.Y + ScreenSize.Scaled(GameplayConstants.QuarkWallMarginLowArcadePixels);
        int highY = bounds.Bottom - ScreenSize.Scaled(GameplayConstants.QuarkWallMarginBottomArcadePixels);

        bool xPositive = _position.X <= lowX || (_position.X < highX && _random.Next(2) == 0);
        bool yPositive = _position.Y <= lowY || (_position.Y < highY && _random.Next(2) != 0);

        _velocitySubpixels = new IntVector2(
            AxisVelocitySubpixels(GameplayConstants.QuarkVelocityXScale, xPositive, coordinateUnitArcadePixels: 2),
            AxisVelocitySubpixels(GameplayConstants.QuarkVelocityYScale, yPositive, coordinateUnitArcadePixels: 1));

        _reaimBeatsRemaining = 1 + _random.Next(GameplayConstants.QuarkReaimMaxBeats);
    }

    /// <summary>One axis's magnitude: the roll (1..this wave's cap) times the axis scale, in subpixels.</summary>
    /// <remarks>The arcade addresses video memory as <c>column*256 + row</c>, so X counts 2-pixel
    /// columns and Y counts 1-pixel rows: the ROM's X scale of 4 and Y scale of 8 give the same
    /// on-screen speed once columns are halved. The mover integrates this once per ROM frame.</remarks>
    private int AxisVelocitySubpixels(int scale, bool positive, int coordinateUnitArcadePixels)
    {
        int roll = 1 + _random.Next(_quarkSpeedRom);
        int subpixels = roll * scale * ScreenSize.Scaled(coordinateUnitArcadePixels);
        return positive ? subpixels : -subpixels;
    }

    /// <summary>Starts the exit run: X stops dead, Y becomes a fixed 2 arcade px per frame, from a coin flip up or down.</summary>
    /// <remarks>ROM: RRTK4.ASM's <c>SQ3</c>.</remarks>
    private void StartFlee()
    {
        _fleeing = true;
        int subpixels = GameplayConstants.QuarkFleeVelocityRom * ScreenSize.Scaled(1);
        _velocitySubpixels = new IntVector2(0, _random.Next(2) == 0 ? subpixels : -subpixels);
        _remainderSubpixels = IntVector2.Zero;
    }

    /// <summary>Moves by the whole-pixel part of the velocity, carrying the fraction.</summary>
    private void AdvancePosition(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;

        _remainderSubpixels += _velocitySubpixels;
        int stepX = _remainderSubpixels.X / GameplayConstants.QuarkSubpixelsPerPixel;
        int stepY = _remainderSubpixels.Y / GameplayConstants.QuarkSubpixelsPerPixel;
        _remainderSubpixels -= new IntVector2(
            stepX * GameplayConstants.QuarkSubpixelsPerPixel,
            stepY * GameplayConstants.QuarkSubpixelsPerPixel);

        if (stepX != 0 && IsInsideX(bounds, _position.X + stepX))
        {
            _position = _position with { X = _position.X + stepX };
        }

        if (stepY != 0 && IsInsideY(bounds, _position.Y + stepY))
        {
            _position = _position with { Y = _position.Y + stepY };
        }
    }

    /// <summary>True when an X coordinate keeps the quark's whole box inside the playfield.</summary>
    private bool IsInsideX(Rectangle bounds, int x) =>
        x >= bounds.X && x + CollisionSize.Width <= bounds.Right;

    /// <summary>True when a Y coordinate keeps the quark's whole box inside the playfield.</summary>
    private bool IsInsideY(Rectangle bounds, int y) =>
        y >= bounds.Y && y + CollisionSize.Height <= bounds.Bottom;

    /// <summary>Advances the animation one picture per beat; the range depends on the phase.</summary>
    /// <remarks>ROM: SQP0..SQP4 while wandering, SQP0..SQP8 while dropping tanks, and SQP8..SQP0
    /// during the exit.</remarks>
    private void AdvanceAnimation()
    {
        if (_fleeing)
        {
            _animationFrame = _animationFrame <= 0 ? GameplayConstants.QuarkTotalFrames - 1 : _animationFrame - 1;
            return;
        }

        int last = _droppingTanks ? GameplayConstants.QuarkTotalFrames - 1 : GameplayConstants.QuarkTravelFrames - 1;
        _animationFrame = _animationFrame >= last ? 0 : _animationFrame + 1;
    }

    /// <summary>Draws the current rotation frame.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        sprites.DrawSprite(spriteBatch, sprites.QuarkFrames[_animationFrame], Bounds, Color.White);
    }

    /// <summary>The current rotation frame, for the death burst (see <see cref="IArtSource"/>).</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <returns>The texture for the current rotation frame.</returns>
    public Texture2D CurrentFrameArt(SpriteSet sprites) => sprites.QuarkFrames[_animationFrame];
}
