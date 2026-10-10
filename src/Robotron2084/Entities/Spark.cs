using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A shot fired by an enforcer robot. It curves through the air along a bent path and stops dead at a wall instead of bouncing.</summary>
/// <seealso cref="Enforcer"/>
/// <remarks>
/// It has no beat. The <see cref="PlayField"/> calls <see cref="Update"/> on nearly every tick, through
/// <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. <see cref="_moveTimer"/> times its moves,
/// <see cref="_accelerationTimer"/> times its speeding up, and <see cref="_flickerTimer"/> times its flicker (see
/// <see cref="ArcadeClock"/>).
///
/// <list type="bullet">
/// <item>Original source: <c>RRC11.ASM</c>, routine <c>SPARK</c> (fired via <c>ENFSHT</c>, flicker frames
/// <c>SPKP0</c>-<c>SPKP3</c>)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$1404</c> (<c>CREATE_SPARK</c>)</item>
/// </list>
/// </remarks>
public sealed class Spark : IEntity, IAnimationFrameSource, IRemovable
{
    private static readonly int Size = ScreenSize.ToPortPixels(CollisionSizes.MissileSizeSpecPixels);
    // The acceleration on each axis: one fixed value, in 1/256 of a pixel per move, rolled once when the spark is made (ROM: PD2/PD4)
    private readonly IntVector2 _accelerationSubpixels;
    private readonly Random _random;
    private readonly SpriteSet _sprites;

    // Counts up towards the next time the acceleration is added to the velocity. That happens every 4 ROM frames (see ArcadeClock).
    private int _accelerationTimer;

    // Counts up towards the next flicker frame. Each animation frame is shown for 4 ROM frames (see ArcadeClock).
    private int _flickerTimer;

    // Counts the port ticks until one ROM frame has passed, so the velocity is added once per frame and not once per tick (see ArcadeClock).
    private int _moveTimer;

    private IntVector2 _position;

    // Keeps the fraction of a pixel left over from each move, so the steps never drift.
    private IntVector2 _positionRemainderSubpixels;

    // How long the spark has left to live, in clock units (see ArcadeClock).
    private int _lifeClockUnitsRemaining;

    private IntVector2 _velocitySubpixels; // The current velocity, in 1/256 of a pixel per ROM frame (see ArcadeClock) (ROM: OXV/OYV)

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

        // A random aim wobble (see Player): from -16 to +15 columns sideways and rows up and down. There is no sideways wobble when the Player is against the left wall.
        int jitterX = _random.Next(-SparkTuning.SparkJitterRange, SparkTuning.SparkJitterRange);
        int jitterY = _random.Next(-SparkTuning.SparkJitterRange, SparkTuning.SparkJitterRange);
        if (playfieldBounds is { } bounds &&
            playerPosition.X < bounds.X + ScreenSize.ToPortPixelsFromColumns(SparkTuning.SparkLeftWallJitterColumns))
        {
            jitterX = 0;
        }

        int deltaX = playerPosition.X + ScreenSize.ToPortPixelsFromColumns(jitterX) - position.X;
        int deltaY = playerPosition.Y + ScreenSize.ToPortPixels(jitterY) - position.Y;

        // Four times the aim change, in fractions of a pixel. The mover only acts on the high byte of the velocity.
        int subpixelsPerPortPixelPerMove = ScreenSize.SubpixelsPerPixel / SparkTuning.SparkAimDivisor;
        _velocitySubpixels = new IntVector2(deltaX * subpixelsPerPortPixelPerMove, deltaY * subpixelsPerPortPixelPerMove);

        // The acceleration on each axis: a random value from -16 to +15, in the same fractions of a pixel.
        // One unit of sideways acceleration is one column. One unit of up-and-down acceleration is one row.
        _accelerationSubpixels = new IntVector2(
            _random.Next(-SparkTuning.SparkAccelRomRange, SparkTuning.SparkAccelRomRange) * subpixelsPerPortPixelPerMove,
            _random.Next(-SparkTuning.SparkAccelRomRange, SparkTuning.SparkAccelRomRange) * subpixelsPerPortPixelPerMove
                * ScreenSize.ToPortPixels(1) / ScreenSize.ToPortPixelsFromColumns(1));

        // How long the spark lives, in clock units. Each port tick adds 5, and each ROM frame needs 6 (see ArcadeClock).
        _lifeClockUnitsRemaining = ArcadeClock.ToClockUnits(_random.Next(
            SparkTuning.SparkLifeMinRomFrames,
            SparkTuning.SparkLifeMaxRomFrames + 1));

        _moveTimer = ArcadeClock.UnitsPerRomFrame;
    }

    /// <summary>The spark's 4x4 spec-pixel collision box at <see cref="Position"/>.</summary>
    public Rectangle GetBounds() => new(_position.X, _position.Y, Size, Size);

    /// <summary>The flicker frame this spark is showing — the sprite pixel-perfect collision compares.</summary>
    public Texture2D GetCurrentAnimationFrame() => _sprites.SparkAnimationFrames[GetAnimationFrameIndex()];

    /// <summary>Only ever transitions Alive -> Dead (immediate removal, no death animation).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Top-left of the spark's collision box.</summary>
    public IntVector2 Position => _position;

    /// <summary>The per-axis acceleration, in 1/256 port px per ROM frame of velocity, applied once per move (test hook).</summary>
    /// <remarks>Rolled once at spawn and CONSTANT for the spark's life.</remarks>
    internal IntVector2 AccelerationSubpixels => _accelerationSubpixels;

    /// <summary>Which of the four flicker frames is showing (test hook).</summary>
    /// <remarks>The ROM's 4 flicker animation frames, one per 4-ROM-frame cycle.</remarks>
    internal int GetAnimationFrameIndex() => _flickerTimer / ArcadeClock.ToClockUnits(SparkTuning.SparkFrameIntervalRomFrames) % SpriteSet.SparkAnimationFrameCount;

    /// <summary>The current velocity, in 1/256 port pixels per ROM frame (test hook, for the ballistic tests).</summary>
    internal IntVector2 VelocitySubpixels => _velocitySubpixels;

    /// <summary>Draws the current flicker frame.
    /// </summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (this.IsAlive())
        {
            _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), Color.White);
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

        // The life counts down. Each port tick takes 5 clock units and each ROM frame needs 6 (see ArcadeClock).
        _lifeClockUnitsRemaining -= ArcadeClock.UnitsPerPortTick;
        if (_lifeClockUnitsRemaining <= 0)
        {
            LifeState = EntityLifeState.Dead;
            return;
        }

        // Each move, the acceleration is added to the velocity, so the path bends into a curve.
        _accelerationTimer += ArcadeClock.UnitsPerPortTick;
        if (_accelerationTimer >= ArcadeClock.ToClockUnits(SparkTuning.SparkMoveIntervalRomFrames))
        {
            _accelerationTimer -= ArcadeClock.ToClockUnits(SparkTuning.SparkMoveIntervalRomFrames);
            _velocitySubpixels = new IntVector2(
                _velocitySubpixels.X + _accelerationSubpixels.X,
                _velocitySubpixels.Y + _accelerationSubpixels.Y);
        }

        // The mover adds the velocity to the position once per ROM frame (see ArcadeClock), keeping the fraction left over.
        _moveTimer += ArcadeClock.UnitsPerPortTick;
        if (_moveTimer >= ArcadeClock.UnitsPerRomFrame)
        {
            _moveTimer -= ArcadeClock.UnitsPerRomFrame;
            _positionRemainderSubpixels = new IntVector2(
                _positionRemainderSubpixels.X + _velocitySubpixels.X,
                _positionRemainderSubpixels.Y + _velocitySubpixels.Y);

            // The step is taken once per ROM frame (see ArcadeClock), not once per move pass. Spreading it over the move interval
            // would run four times too slow. One port pixel is SparkVelocityScale fractions of a pixel.
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
        // A safety cap on the velocity, matching the ROM's own top speed.
        stepX = Math.Clamp(stepX, -SparkTuning.SparkMaxSpeed, SparkTuning.SparkMaxSpeed);
        stepY = Math.Clamp(stepY, -SparkTuning.SparkMaxSpeed, SparkTuning.SparkMaxSpeed);

        // Each axis moves only if the step stays inside the playfield. Otherwise the spark stops dead, with no clamping and no bounce.
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
