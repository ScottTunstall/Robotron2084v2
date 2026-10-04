using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A shot fired by an enforcer robot. It curves through the air along a bent path and stops dead at a wall instead of bouncing. It has no beat. The <see cref="PlayField"/> calls <see cref="Update"/> on nearly every tick, through <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. <see cref="_moveTimer"/> times its moves, <see cref="_accelerationTimer"/> times its speeding up, and <see cref="_flickerTimer"/> times its flicker (see <see cref="ArcadeClock"/>).</summary>
/// <seealso cref="Enforcer"/>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRC11.ASM</c>, routine <c>SPARK</c> (fired via <c>ENFSHT</c>, flicker frames <c>SPKP0</c>-<c>SPKP3</c>)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$1404</c> (<c>CREATE_SPARK</c>)</item>
/// </list>
/// </remarks>
public sealed class Spark : IEntity, IAnimationFrameSource, IRemovable
{
    private static readonly int Size = ScreenSize.ToPortPixels(CollisionSizes.MissileSizeSpecPixels);
    private readonly IntVector2 _accelerationSubpixels;
    private readonly Random _random;
    private readonly SpriteSet _sprites;
    private int _accelerationTimer;

    // counts up toward the next time acceleration is added to velocity, every 4 ROM frames
    private int _flickerTimer;

    // Counts up to the next flicker animation frame: 4 ROM frames per animation frame
    private int _moveTimer;

    private IntVector2 _position;

    private IntVector2 _positionRemainderSubpixels;

    // carries the sub-pixel part so the step never drifts
    private int _remainingLife;

    // the constant per-axis acceleration, in 1/256 px per move, rolled once at spawn (ROM: PD2/PD4)
    private IntVector2 _velocitySubpixels; // current velocity, in 1/256 px per ROM frame (ROM: OXV/OYV)

    // counts up to one ROM frame's worth of ticks so the mover integrates velocity once per frame, not once per tick

    /// <summary>Fires a spark, aimed at the player once, with jitter.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where it appears — the firing enforcer's position.</param>
    /// <param name="playerPosition">The player, which the spark is aimed at.</param>
    /// <param name="random">The random source, standing in for the arcade's SEED/LSEED/HSEED rolls.</param>
    /// <param name="playfieldBounds">The playfield, used only for the "no X jitter near the left wall" rule. Null applies no suppression.</param>
    public Spark(
        SpriteSet sprites,
        IntVector2 position,
        IntVector2 playerPosition,
        Random random,
        Rectangle? playfieldBounds = null)
    {
        _sprites = sprites;
        _position = position;
        _random = random;

        // Aim jitter, -16..+15 columns sideways and rows up and down; none sideways when the player hugs the left wall.
        int jitterX = _random.Next(-SparkTuning.SparkJitterRange, SparkTuning.SparkJitterRange);
        int jitterY = _random.Next(-SparkTuning.SparkJitterRange, SparkTuning.SparkJitterRange);
        if (playfieldBounds is { } bounds &&
            playerPosition.X < bounds.X + ScreenSize.ToPortPixelsFromColumns(SparkTuning.SparkLeftWallJitterColumns))
        {
            jitterX = 0;
        }

        int deltaX = playerPosition.X + ScreenSize.ToPortPixelsFromColumns(jitterX) - position.X;
        int deltaY = playerPosition.Y + ScreenSize.ToPortPixels(jitterY) - position.Y;

        // 4x the aim delta, in subpixels (the mover only acts on the velocity's high byte).
        int subpixelsPerPortPxPerMove = ScreenSize.SubpixelsPerPixel / SparkTuning.SparkAimDivisor;
        _velocitySubpixels = new IntVector2(deltaX * subpixelsPerPortPxPerMove, deltaY * subpixelsPerPortPxPerMove);

        // The constant per-axis acceleration: a random -16..+15, in the same subpixel units.
        // A sideways unit of acceleration is a column's worth, an up-and-down one a row's worth.
        _accelerationSubpixels = new IntVector2(
            _random.Next(-SparkTuning.SparkAccelRomRange, SparkTuning.SparkAccelRomRange) * subpixelsPerPortPxPerMove,
            _random.Next(-SparkTuning.SparkAccelRomRange, SparkTuning.SparkAccelRomRange) * subpixelsPerPortPxPerMove
                * ScreenSize.ToPortPixels(1) / ScreenSize.ToPortPixelsFromColumns(1));

        // Life, in timer units: 5 per tick, 6 per arcade frame.
        _remainingLife = ArcadeClock.ToClockUnits(_random.Next(
            SparkTuning.SparkLifeMinRomFrames,
            SparkTuning.SparkLifeMaxRomFrames + 1));

        _moveTimer = ArcadeClock.UnitsPerRomFrame;
    }

    /// <summary>The spark's 4x4 spec-pixel collision box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, Size, Size);

    /// <summary>The flicker frame this spark is showing — the sprite pixel-perfect collision compares.</summary>
    public Texture2D GetCurrentAnimationFrame() => _sprites.SparkAnimationFrames[AnimationFrameIndex];

    /// <summary>Only ever transitions Alive -> Dead (immediate removal, no death animation).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Top-left of the spark's collision box.</summary>
    public IntVector2 Position => _position;

    /// <summary>The per-axis acceleration, in 1/256 port px per ROM frame of velocity, applied once per move (test hook).</summary>
    /// <remarks>Rolled once at spawn and CONSTANT for the spark's life.</remarks>
    internal IntVector2 AccelerationSubpixels => _accelerationSubpixels;

    /// <summary>Which of the four flicker frames is showing (test hook).</summary>
    /// <remarks>The ROM's 4 flicker animation frames, one per 4-ROM-frame cycle.</remarks>
    internal int AnimationFrameIndex => _flickerTimer / ArcadeClock.ToClockUnits(SparkTuning.SparkFrameIntervalRomFrames) % SpriteSet.SparkAnimationFrameCount;

    /// <summary>The current velocity, in 1/256 port pixels per ROM frame (test hook, for the ballistic tests).</summary>
    internal IntVector2 VelocitySubpixels => _velocitySubpixels;

    /// <summary>Draws the current flicker frame.
    /// </summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (this.IsAlive())
        {
            _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), Bounds, Color.White);
        }
    }

    /// <summary>Laser hit: removed at once.</summary>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Runs the spark's clocks: flicker, life, the move step and the shared mover.</summary>
    /// <param name="gameTime">Unused — every clock is counted in ticks.</param>
    /// <param name="field">The playfield wall.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive())
        {
            return;
        }

        _flickerTimer += ArcadeClock.UnitsPerPortTick;

        // Life counts down: 5 per tick, 6 per arcade frame.
        _remainingLife -= ArcadeClock.UnitsPerPortTick;
        if (_remainingLife <= 0)
        {
            LifeState = EntityLifeState.Dead;
            return;
        }

        // Every move the acceleration is added to the velocity, so the path curves into a parabola.
        _accelerationTimer += ArcadeClock.UnitsPerPortTick;
        if (_accelerationTimer >= ArcadeClock.ToClockUnits(SparkTuning.SparkMoveIntervalRomFrames))
        {
            _accelerationTimer -= ArcadeClock.ToClockUnits(SparkTuning.SparkMoveIntervalRomFrames);
            _velocitySubpixels = new IntVector2(
                _velocitySubpixels.X + _accelerationSubpixels.X,
                _velocitySubpixels.Y + _accelerationSubpixels.Y);
        }

        // The mover adds the velocity once per ROM frame, carrying the subpixel remainder.
        _moveTimer += ArcadeClock.UnitsPerPortTick;
        if (_moveTimer >= ArcadeClock.UnitsPerRomFrame)
        {
            _moveTimer -= ArcadeClock.UnitsPerRomFrame;
            _positionRemainderSubpixels = new IntVector2(
                _positionRemainderSubpixels.X + _velocitySubpixels.X,
                _positionRemainderSubpixels.Y + _velocitySubpixels.Y);

            // The step is per ROM frame, not per move-pass: spreading it over the move interval
            // runs 4x slow. 1 port px = SparkVelocityScale subpixel units.
            int stepX = _positionRemainderSubpixels.X / ScreenSize.SubpixelsPerPixel;
            int stepY = _positionRemainderSubpixels.Y / ScreenSize.SubpixelsPerPixel;
            _positionRemainderSubpixels = new IntVector2(
                _positionRemainderSubpixels.X - (stepX * ScreenSize.SubpixelsPerPixel),
                _positionRemainderSubpixels.Y - (stepY * ScreenSize.SubpixelsPerPixel));

            MoveBy(field, stepX, stepY);
        }
    }

    /// <summary>Applies one mover step and the wall rejection.</summary>
    /// <param name="field">The playfield wall.</param>
    /// <param name="stepX">This frame's whole-pixel step on X.</param>
    /// <param name="stepY">This frame's whole-pixel step on Y.</param>
    private void MoveBy(PlayField field, int stepX, int stepY)
    {
        // Safety cap, reproducing the ROM's own velocity ceiling.
        stepX = Math.Clamp(stepX, -SparkTuning.SparkMaxSpeed, SparkTuning.SparkMaxSpeed);
        stepY = Math.Clamp(stepY, -SparkTuning.SparkMaxSpeed, SparkTuning.SparkMaxSpeed);

        // Each axis is updated only if the step stays inside the field — no clamp, no bounce.
        Rectangle inner = field.Wall.PlayfieldBounds;
        int x = _position.X;
        int y = _position.Y;
        if (_position.X + stepX >= inner.X && _position.X + stepX + Size <= inner.Right)
        {
            x += stepX;
        }

        if (_position.Y + stepY >= inner.Y && _position.Y + stepY + Size <= inner.Bottom)
        {
            y += stepY;
        }

        _position = new IntVector2(x, y);
    }
}
