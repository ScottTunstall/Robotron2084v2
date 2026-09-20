using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>An enforcer's spark: a ballistic shot whose fixed acceleration curves its path.</summary>
/// <seealso cref="Enforcer"/>
/// <remarks>ROM: RRC11.ASM's <c>ENFSHT</c>/<c>SPARK</c>, with the <c>SPKP0</c>-<c>SPKP3</c> flicker
/// frames (notes §41). At spawn each axis gets a velocity of 4 x (player coord + jitter - spark
/// coord), with a random -16..+15 columns of jitter and the X jitter forced to 0 when the player is
/// within 16 columns of the left wall, plus a constant acceleration rolled once as -16..+15 and fixed
/// for life. Every 4 ROM frames the acceleration is added to the velocity, so the path bends into a
/// parabola and the spark can start out moving away from the player. The mover adds the velocity once
/// per ROM frame and refuses a step that would leave the field, so a spark slides then stops at the
/// wall — no bounce. It lives 20-35 moves (80-140 ROM frames); the spec's 10-15 s was changed
/// deliberately, so don't revert it without checking. All the subpixel maths is in 1/256-px units,
/// not floating point. Timers count 5 per tick and 6 per arcade frame, so an interval of N frames is
/// due at 6 x N.</remarks>
public sealed class Spark : IEntity
{
    private static readonly int Size = ScreenSize.Scaled(GameplayConstants.MissileSizeSpecPixels);
    private readonly Random _random;
    private readonly int _stepScale; // 1 port px = 256 subpixel units
    private readonly IntVector2 _accelerationSubpixels; // the constant per-axis acceleration, in 1/256 px per move, rolled once at spawn (ROM: PD2/PD4)
    private IntVector2 _velocitySubpixels; // current velocity, in 1/256 px per ROM frame (ROM: OXV/OYV)
    private IntVector2 _positionRemainderSubpixels; // carries the sub-pixel part so the step never drifts
    private IntVector2 _position;
    private int _remainingLife;
    private int _accelerationTimer;   // counts up toward the next time acceleration is added to velocity, every 4 ROM frames
    private int _flickerTimer; // Counts up to the next flicker picture: 4 ROM frames per picture
    private int _moveTimer;  // counts up to one ROM frame's worth of ticks so the mover integrates velocity once per frame, not once per tick

    /// <summary>Fires a spark, aimed at the player once, with jitter.</summary>
    /// <param name="position">Where it appears — the firing enforcer's position.</param>
    /// <param name="playerPosition">The player, which the spark is aimed at.</param>
    /// <param name="random">The random source, standing in for the arcade's SEED/LSEED/HSEED rolls.</param>
    /// <param name="playfieldBounds">The playfield, used only for the "no X jitter near the left wall" rule. Null applies no suppression.</param>
    public Spark(IntVector2 position, IntVector2 playerPosition, Random random, Rectangle? playfieldBounds = null)
    {
        _position = position;
        _random = random;
        // A per-frame step, not per move-pass: spreading it over the move interval runs 4x slow.
        _stepScale = GameplayConstants.SparkVelocityScale;

        // Aim jitter, -16..+15 columns; suppressed on X when the player hugs the left wall.
        int jitterX = _random.Next(-GameplayConstants.SparkJitterColumns, GameplayConstants.SparkJitterColumns);
        int jitterY = _random.Next(-GameplayConstants.SparkJitterColumns, GameplayConstants.SparkJitterColumns);
        if (playfieldBounds is { } bounds &&
            playerPosition.X < bounds.X + ScreenSize.Scaled(2 * GameplayConstants.SparkLeftWallJitterColumns))
        {
            jitterX = 0;
        }

        // 1 ROM column = Scaled(2) port px.
        int columnPortPx = ScreenSize.Scaled(2);
        int deltaX = playerPosition.X + (jitterX * columnPortPx) - position.X;
        int deltaY = playerPosition.Y + (jitterY * columnPortPx) - position.Y;

        // 4x the aim delta, in subpixels (the mover only acts on the velocity's high byte).
        int subpixelsPerPortPxPerMove = GameplayConstants.SparkVelocityScale / GameplayConstants.SparkAimDivisor;
        _velocitySubpixels = new IntVector2(deltaX * subpixelsPerPortPxPerMove, deltaY * subpixelsPerPortPxPerMove);

        // The constant per-axis acceleration: a random -16..+15, in the same subpixel units.
        _accelerationSubpixels = new IntVector2(
            _random.Next(-GameplayConstants.SparkAccelRomRange, GameplayConstants.SparkAccelRomRange) * subpixelsPerPortPxPerMove,
            _random.Next(-GameplayConstants.SparkAccelRomRange, GameplayConstants.SparkAccelRomRange) * subpixelsPerPortPxPerMove);

        // Life, in timer units: 5 per tick, 6 per arcade frame.
        _remainingLife = _random.Next(
            GameplayConstants.SparkLifeMinRomTicks,
            GameplayConstants.SparkLifeMaxRomTicks + 1) * 6;

        _moveTimer = 6;
    }

    /// <summary>Top-left of the spark's collision box.</summary>
    public IntVector2 Position => _position;

    /// <summary>The spark's 4x4 spec-pixel collision box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, Size, Size);

    /// <summary>Only ever transitions Alive -> Dead (immediate removal, no death animation).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Laser hit: removed at once (25 points).</summary>
    public void Destroy() => LifeState = EntityLifeState.Dead;

    /// <summary>Which of the four flicker frames is showing (test hook).</summary>
    /// <remarks>The ROM's 4 flicker pictures, one per 4-ROM-frame cycle.</remarks>
    internal int FrameIndex => _flickerTimer / (GameplayConstants.SparkFramePeriodRomTicks * 6) % SpriteSet.SparkFrameCount;

    /// <summary>The current velocity, in 1/256 port pixels per ROM frame (test hook, for the ballistic tests).</summary>
    internal IntVector2 VelocitySubpixels => _velocitySubpixels;

    /// <summary>The per-axis acceleration, in 1/256 port px per ROM frame of velocity, applied once per move (test hook).</summary>
    /// <remarks>Rolled once at spawn and CONSTANT for the spark's life.</remarks>
    internal IntVector2 AccelerationSubpixels => _accelerationSubpixels;

    /// <summary>Runs the spark's clocks: flicker, life, the move step and the shared mover.</summary>
    /// <param name="gameTime">Unused — every clock is counted in ticks.</param>
    /// <param name="field">The playfield wall.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        _flickerTimer += 5;

        // Life counts down: 5 per tick, 6 per arcade frame.
        _remainingLife -= 5;
        if (_remainingLife <= 0)
        {
            LifeState = EntityLifeState.Dead;
            return;
        }

        // Every move the acceleration is added to the velocity, so the path curves into a parabola.
        _accelerationTimer += 5;
        if (_accelerationTimer >= GameplayConstants.SparkMoveIntervalRomTicks * 6)
        {
            _accelerationTimer -= GameplayConstants.SparkMoveIntervalRomTicks * 6;
            _velocitySubpixels = new IntVector2(
                _velocitySubpixels.X + _accelerationSubpixels.X,
                _velocitySubpixels.Y + _accelerationSubpixels.Y);
        }

        // The mover adds the velocity once per ROM frame, carrying the subpixel remainder.
        _moveTimer += 5;
        if (_moveTimer >= 6)
        {
            _moveTimer -= 6;
            _positionRemainderSubpixels = new IntVector2(
                _positionRemainderSubpixels.X + _velocitySubpixels.X,
                _positionRemainderSubpixels.Y + _velocitySubpixels.Y);

            int stepX = _positionRemainderSubpixels.X / _stepScale;
            int stepY = _positionRemainderSubpixels.Y / _stepScale;
            _positionRemainderSubpixels = new IntVector2(
                _positionRemainderSubpixels.X - (stepX * _stepScale),
                _positionRemainderSubpixels.Y - (stepY * _stepScale));

            MoveBy(field, stepX, stepY);
            return;
        }

        MoveBy(field, 0, 0);
    }

    /// <summary>Applies one (possibly zero) mover step and the wall rejection.</summary>
    /// <param name="field">The playfield wall.</param>
    /// <param name="stepX">This frame's whole-pixel step on X.</param>
    /// <param name="stepY">This frame's whole-pixel step on Y.</param>
    private void MoveBy(PlayField field, int stepX, int stepY)
    {

        // Safety cap, reproducing the ROM's own velocity ceiling.
        stepX = Math.Clamp(stepX, -GameplayConstants.SparkMaxSpeed, GameplayConstants.SparkMaxSpeed);
        stepY = Math.Clamp(stepY, -GameplayConstants.SparkMaxSpeed, GameplayConstants.SparkMaxSpeed);

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

    /// <summary>Draws the current flicker frame.
    /// </summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState == EntityLifeState.Alive)
        {
            sprites.DrawSprite(spriteBatch, sprites.SparkFrames[FrameIndex], Bounds, Color.White);
        }
    }
}
