using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A hazard entity: the small flickering missile an "enforcer" (a floating gun-turret enemy)
/// fires at the player. Touching one kills the player just like touching an enemy does. What
/// makes it dangerous — and different from the player's own laser or a tank shell — is that its
/// flight path is BALLISTIC rather than straight or random: it launches roughly toward the
/// player's position (with some randomness thrown in, so its aim isn't perfect), and from then
/// on a constant, fixed-at-launch per-axis acceleration steadily bends its path, so it flies a
/// curve (a parabola) rather than a straight line, and can even start out moving AWAY from the
/// player before curving back in — making it harder to predict and dodge than a shot that just
/// beelines for you.
///
/// It dies of old age, or instantly on a laser hit (25 points, no death animation). It does not bounce:
/// the mover refuses a step that would leave the playfield, so a spark slides and then stops at the wall.
/// </summary>
/// <remarks>
/// <para>
/// See the terminology glossary on <see cref="IEntity"/> for what "ROM frame", the
/// "..Timer" fixed-point clock and "notes §NN" mean generally. This class additionally uses a
/// second, finer fixed-point scheme of its own (see the "subpixel" note below) to reproduce the
/// ROM's sub-pixel-precise ballistic maths exactly — that is unrelated to the "..Timer" timing
/// clocks and is explained where it's used.
/// </para>
/// Ported from the arcade's own spark behaviour, routine for routine (ROM: RRC11.ASM, the
/// `ENFSHT`/`SPARK` routines and the `SPKP0`-`SPKP3` flicker frames, following the same
/// shared-mover convention used elsewhere; notes §41).
///
/// At spawn each axis gets two things, both in "subpixel" units — a fixed-point scheme
/// (implemented below by <see cref="_stepScale"/> and <see cref="_positionRemainderSubpixels"/>) where
/// each whole screen pixel is subdivided into 256 fractional steps, so slow, curving motion can
/// be tracked precisely without floating point:
/// <list type="bullet">
/// <item>a VELOCITY of <c>4 x (player coord + jitter - spark coord)</c>, where the jitter is a
/// random -16..+15 COLUMNS and the X jitter is forced to 0 when the player is within 16 columns
/// of the left wall;</item>
/// <item>a CONSTANT acceleration, rolled independently per axis as a random -16..+15 and fixed
/// for the spark's whole life.</item>
/// </list>
/// Every move (every 4 ROM frames) the acceleration is added into the velocity, so the spark's
/// path steadily bends — the arcade's "habit of going off course". Because the jitter is
/// comparable to the delta at close range, a spark can start out moving AWAY from the player.
///
/// Speed comes from the shared mover, which adds the full 16-bit velocity to the 16-bit world
/// position once per ROM frame (notes §43, §93).
///
/// Life is a random 20-35 moves x 4 ROM frames each = 80-140 ROM frames (1.6-2.8 s) — a
/// deliberate deviation from the spec's 10-15 s; don't revert without checking.
///
/// Walls: the mover REJECTS an axis update that would push the picture out of the playfield, so a spark
/// slides and then stops AT the wall — no bounce, no wall-death.
///
/// Flicker (notes §32): the ROM advances to the next of its 4 flicker pictures once per beat pass
/// and re-runs every 4 ROM frames — a four-frame flash, one frame per PortTicks(4) port ticks, no holds.
/// </remarks>
public sealed class Spark : IEntity
{
    private static readonly int Size = ScreenSize.Scaled(GameplayConstants.MissileSizeSpecPixels);
    private readonly Random _random;
    private readonly int _stepScale; // fixed point: 1 port px = 256 subpixel units
    private readonly IntVector2 _accelerationSubpixels; // the constant per-axis acceleration, in 1/256 px per move, rolled once at spawn (ROM: PD2/PD4)
    private IntVector2 _velocitySubpixels; // current velocity, in 1/256 px per ROM frame (ROM: OXV/OYV)
    private IntVector2 _positionRemainderSubpixels; // carries the sub-pixel part so the step never drifts
    private IntVector2 _position;
    private int _remainingLife;
    private int _accelerationTimer;   // counts up toward the next time acceleration is added to velocity, every 4 ROM frames
    private int _flickerTimer; // the 4-frame flicker clock, also exact
    private int _moveTimer;  // counts up to one ROM frame's worth of ticks so the mover integrates velocity once per frame, not once per tick

    /// <summary>Fires a spark, aimed at the player once, with jitter.</summary>
    /// <param name="position">Where it appears — the firing enforcer's position.</param>
    /// <param name="playerPosition">The player, which the spark is aimed at.</param>
    /// <param name="random">The random source, standing in for the arcade's SEED/LSEED/HSEED rolls.</param>
    /// <param name="playfieldBounds">The playfield, used only for the "no X jitter near the left wall" rule. Null applies no suppression.</param>
    /// <remarks>The spark aims at the player's position: the velocity is proportional to the distance,
    /// per axis, and the per-axis acceleration is rolled once here and never changes (ROM: `ENFSHT`).</remarks>
    public Spark(IntVector2 position, IntVector2 playerPosition, Random random, Rectangle? playfieldBounds = null)
    {
        _position = position;
        _random = random;
        // The shared object mover applies the FULL 16-bit velocity to the position
        // ONCE PER FRAME — a per-FRAME step, not a per-process-pass one. Spreading
        // the step across the 4-frame move interval instead runs the spark 4x too slow.
        _stepScale = GameplayConstants.SparkVelocityScale;

        // The aim jitter is a random -16..+15 COLUMNS per axis, and the X jitter
        // is suppressed when the player hugs the left wall.
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

        // The initial velocity is 4x the aim delta, and the mover only acts on
        // its high byte, so a move actually covers delta/64 columns = deltaPort/64
        // port px. Converted to our subpixel fixed point: deltaPort x 256/64.
        int subpixelsPerPortPxPerMove = GameplayConstants.SparkVelocityScale / GameplayConstants.SparkAimDivisor;
        _velocitySubpixels = new IntVector2(deltaX * subpixelsPerPortPxPerMove, deltaY * subpixelsPerPortPxPerMove);

        // The CONSTANT per-axis acceleration: a random -16..+15 in the same
        // subpixel units, i.e. a/64 port px added to the velocity per move (a x 4 in subpixels).
        _accelerationSubpixels = new IntVector2(
            _random.Next(-GameplayConstants.SparkAccelRomRange, GameplayConstants.SparkAccelRomRange) * subpixelsPerPortPxPerMove,
            _random.Next(-GameplayConstants.SparkAccelRomRange, GameplayConstants.SparkAccelRomRange) * subpixelsPerPortPxPerMove);

        // Lifespan: a random 20-35 moves of 4 ROM frames each = 80-140 ROM frames,
        // held in exact 6ths (notes §52/§65) rather than rounded to whole ticks,
        // which would cut each move slightly short.
        _remainingLife = _random.Next(
            GameplayConstants.SparkLifeMinRomTicks,
            GameplayConstants.SparkLifeMaxRomTicks + 1) * 6;

        // The mover moves the spark from the first frame (notes §93).
        _moveTimer = 6;
    }

    /// <summary>Top-left of the spark's collision box.</summary>
    public IntVector2 Position => _position;

    /// <summary>The spark's 4x4 spec-pixel collision box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, Size, Size);

    /// <summary>Only ever transitions Alive -> Dead (immediate removal, no death animation).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Laser hit: removed from the screen immediately (25 pts).</summary>
    public void Destroy() => LifeState = EntityLifeState.Dead;

    /// <summary>Which of the four flicker frames is showing (test hook).</summary>
    /// <remarks>The ROM's 4 flicker pictures, one shown per 4-ROM-frame cycle.</remarks>
    internal int FrameIndex => _flickerTimer / (GameplayConstants.SparkFramePeriodRomTicks * 6) % SpriteSet.SparkFrameCount;

    /// <summary>The current velocity, in 1/256 port pixels per ROM frame (test hook, for the ballistic tests).</summary>
    internal IntVector2 VelocitySubpixels => _velocitySubpixels;

    /// <summary>The per-axis acceleration, in 1/256 port px per ROM frame of velocity, applied once per move (test hook).</summary>
    /// <remarks>Rolled once at spawn and CONSTANT for the spark's life.</remarks>
    internal IntVector2 AccelerationSubpixels => _accelerationSubpixels;

    /// <summary>
    /// Runs the spark's clocks: the four-frame flicker, its life, the move step (every 4 ROM frames,
    /// where the constant acceleration is added to the velocity) and the shared mover, which
    /// adds the velocity to the position once per ROM frame.
    /// </summary>
    /// <param name="gameTime">Unused — every clock here is counted in ROM frames.</param>
    /// <param name="field">The playfield wall, which the spark stops against.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        _flickerTimer += 5;

        // Life: a random 20..35 moves of 4 ROM frames each = 80..140 ROM frames, in exact 6ths.
        _remainingLife -= 5;
        if (_remainingLife <= 0)
        {
            LifeState = EntityLifeState.Dead;
            return;
        }

        // Once per move, the constant acceleration is added into the velocity on
        // each axis. Because the acceleration is CONSTANT for the spark's whole
        // life, the velocity integrates it and the path curves into a parabola —
        // this is the arcade's flight, not a random walk. The move interval is
        // 4 ROM frames = 4.8 ticks exactly (notes §52) — rounding it to 4 whole
        // ticks made the flight 20% too fast.
        _accelerationTimer += 5;
        if (_accelerationTimer >= GameplayConstants.SparkMoveIntervalRomTicks * 6)
        {
            _accelerationTimer -= GameplayConstants.SparkMoveIntervalRomTicks * 6;
            _velocitySubpixels = new IntVector2(
                _velocitySubpixels.X + _accelerationSubpixels.X,
                _velocitySubpixels.Y + _accelerationSubpixels.Y);
        }

        // Shared mover: the full velocity is added to the world position once per
        // ROM frame — a frame is 6/5 of a tick, so the integration runs every 6
        // fifth-ticks, not every tick (running it every tick made the spark 20% too
        // fast, notes §93). The port carries the sub-pixel remainder so the step
        // never drifts.
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

        // Port safety cap (the ROM relies on its velocity naturally topping out at
        // the limit of its own number range; this cap reproduces that ceiling).
        stepX = Math.Clamp(stepX, -GameplayConstants.SparkMaxSpeed, GameplayConstants.SparkMaxSpeed);
        stepY = Math.Clamp(stepY, -GameplayConstants.SparkMaxSpeed, GameplayConstants.SparkMaxSpeed);

        // Walls: the mover REJECTS an axis update that would leave the playfield —
        // it does NOT clamp. The object keeps its last valid coordinate on that
        // axis while the other axis keeps moving, which is why a spark slides
        // along and then stops at a wall instead of sticking to the edge. It dies
        // only when its life expires — no bounce, no wall-death.
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
    /// <param name="sprites">The shared sprite set, which holds the spark frames.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState == EntityLifeState.Alive)
        {
            sprites.DrawSprite(spriteBatch, sprites.SparkFrames[FrameIndex], Bounds, Color.White);
        }
    }
}
