using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A spark — the missile an enforcer fires at the player. Its flight is BALLISTIC rather than straight
/// or random: launched toward the player with jitter, then bent by a constant per-axis acceleration as
/// it goes, so it flies a parabola and can even start out moving AWAY.
///
/// It dies of old age, or instantly on a laser hit (25 points, no death animation). It does not bounce:
/// the mover refuses a step that would leave the playfield, so a spark slides and then stops at the wall.
/// </summary>
/// <remarks>
/// Arcade-faithful per the GOSPEL (<c>ref/original-source/RRC11.ASM</c>: `ENFSHT` + `SPARK` + `SPKP0`..3;
/// the mover convention from RRSCRIPT.ASM's MONOP; notes 41).
///
/// At spawn (`ENFSHT`) each axis gets two things, both in "subpixel" units:
/// <list type="bullet">
/// <item>a VELOCITY of <c>4 x (player coord + jitter - spark coord)</c>, where the jitter is
/// <c>(seed &amp; $1F) - 16</c> = -16..+15 COLUMNS and the X jitter is forced to 0 when the player is
/// within 16 columns of the left wall (<c>CMPA #XMIN+$10 / BHS ENFS2 / CLRB</c>);</item>
/// <item>a CONSTANT acceleration <c>PD2/PD4 = (LSEED/HSEED &amp; $1F) - 16</c>, chosen independently per
/// axis and fixed for the spark's whole life.</item>
/// </list>
/// Every move (<c>NAP 4</c> = 4 vblanks) the `SPARK` process does <c>OXV += PD2; OYV += PD4</c>, so the
/// acceleration integrates into the velocity — the arcade's "habit of going off course". Because the
/// jitter is comparable to the delta at close range, a spark can start out moving AWAY from the player.
///
/// Speed comes from the generic mover (RRS22.ASM `OPB80`: <c>ADDD OXV,X / STD OX16,X</c>), which adds
/// the full 16-bit velocity to the 16-bit world position once per ROM frame (notes §43, §93).
///
/// Life = <c>PD7 = (HSEED &amp; $F) + $14</c> = 20..35 MOVES x 4 vblanks = 80..140 ROM ticks (1.6-2.8 s),
/// NOT the spec's 10-15 s (playtest round 9: sparks "linger for a while").
///
/// Walls: the mover REJECTS an axis update that would push the picture out of the playfield, so a spark
/// slides and then stops AT the wall — no bounce, no wall-death.
///
/// Flicker (notes 32): the ROM advances `OPICT` one 4-byte entry (`SPKP0`..3) per body pass and re-runs
/// every 4 vblanks (<c>NAP 4</c>) — a four-frame flash, one frame per PortTicks(4) port ticks, no holds.
/// </remarks>
public sealed class Spark : IEntity
{
    private static readonly int Size = ScreenSize.Scaled(GameplayConstants.MissileSizeSpecPixels);
    private readonly Random _random;
    private readonly int _stepScale; // fixed point: 1 port px = 256 fp units
    private readonly IntVector2 _accelerationFp; // ROM PD2/PD4, 1/256 px per move, CONSTANT
    private IntVector2 _velocityFp; // ROM OXV/OYV, 1/256 px per frame
    private IntVector2 _positionRemainderFp; // carries the sub-pixel part so the step never drifts
    private IntVector2 _position;
    private int _remainingLifeFifths;
    private int _moveFifths;   // 4 ROM frames = 4.8 ticks (notes §52)
    private int _flickerFifths; // the 4-frame flicker clock, also exact
    private int _moverSixths;  // OPB80 cadence: one velocity integration per 6 sixths = 1 ROM frame

    /// <summary>Fires a spark, aimed at the player once, with jitter.</summary>
    /// <param name="position">Where it appears — the firing enforcer's position.</param>
    /// <param name="playerPosition">The player, which the spark is aimed at.</param>
    /// <param name="random">The random source, standing in for the arcade's SEED/LSEED/HSEED rolls.</param>
    /// <param name="playfieldBounds">The playfield, used only for the "no X jitter near the left wall" rule. Null applies no suppression.</param>
    /// <remarks>`ENFSHT` (RRC11.ASM) aims with the player's position: the velocity is proportional to the distance,
    /// per axis, and the per-axis acceleration is rolled once here and never changes.</remarks>
    public Spark(IntVector2 position, IntVector2 playerPosition, Random random, Rectangle? playfieldBounds = null)
    {
        _position = position;
        _random = random;
        // CONFIRMED 2026-09-16: the generic object mover (RRS22.ASM OPB80:
        // `ADDD OXV,X / STD OX16,X`, looping the whole object list) applies the
        // FULL 16-bit velocity to the position ONCE PER FRAME — so OXV is a
        // per-FRAME step, not a per-process-pass one. An earlier version spread
        // the step across the NAP 4 interval and therefore ran 4x too slow.
        _stepScale = GameplayConstants.SparkVelocityScale;

        // ENFSHT: jitter = (seed & $1F) - 16 => -16..+15 COLUMNS per axis, and
        // the X jitter is suppressed when the player hugs the left wall
        // (CMPA #XMIN+$10 / BHS ENFS2 / CLRB).
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

        // OXV = 4 x delta, and the mover adds its HIGH byte, so a move covers
        // delta/64 columns = deltaPort/64 port px. In fp: deltaPort x 256/64.
        int fpPerPortPxPerMove = GameplayConstants.SparkVelocityScale / GameplayConstants.SparkAimDivisor;
        _velocityFp = new IntVector2(deltaX * fpPerPortPxPerMove, deltaY * fpPerPortPxPerMove);

        // PD2/PD4: the CONSTANT per-axis acceleration, (seed & $1F) - 16 in the
        // same subpixel units => a/64 port px per frame of velocity per move => a x 4 in fp.
        _accelerationFp = new IntVector2(
            _random.Next(-GameplayConstants.SparkAccelRomRange, GameplayConstants.SparkAccelRomRange) * fpPerPortPxPerMove,
            _random.Next(-GameplayConstants.SparkAccelRomRange, GameplayConstants.SparkAccelRomRange) * fpPerPortPxPerMove);

        // PD7 = (HSEED & $F) + $14 moves x 4 vblanks = 80..140 ROM frames, held in
        // exact 6ths (notes §52/§65 — PortTicks() truncates each one by up to 0.8).
        _remainingLifeFifths = _random.Next(
            GameplayConstants.SparkLifeMinRomTicks,
            GameplayConstants.SparkLifeMaxRomTicks + 1) * 6;

        // The mover moves the spark from the first frame (notes §93).
        _moverSixths = 6;
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
    /// <remarks>ROM `SPKP0`..3: one frame per 4-vblank body pass (<c>NAP 4</c>), cycling.</remarks>
    internal int FrameIndex => _flickerFifths / (GameplayConstants.SparkFramePeriodRomTicks * 6) % SpriteSet.SparkFrameCount;

    /// <summary>The current velocity, in 1/256 port pixels per ROM frame (test hook, for the ballistic tests).</summary>
    /// <remarks>ROM `OXV`/`OYV`.</remarks>
    internal IntVector2 VelocityFp => _velocityFp;

    /// <summary>The per-axis acceleration, in 1/256 port px per ROM frame of velocity, applied once per move (test hook).</summary>
    /// <remarks>ROM `PD2`/`PD4`, rolled once at spawn and CONSTANT for the spark's life.</remarks>
    internal IntVector2 AccelerationFp => _accelerationFp;

    /// <summary>
    /// Runs the spark's clocks: the four-frame flicker, its life, the `SPARK` process (every 4
    /// vblanks, where the constant acceleration is added to the velocity) and the generic mover, which
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

        _flickerFifths += 5;

        // Life: 20..35 ROM cycles x 4 vblanks = 80..140 ROM frames, in exact 6ths.
        _remainingLifeFifths -= 5;
        if (_remainingLifeFifths <= 0)
        {
            LifeState = EntityLifeState.Dead;
            return;
        }

        // SPARK: once per move, OXV += PD2 and OYV += PD4. The acceleration is
        // CONSTANT for the spark's life, so the velocity integrates it and the
        // path is a parabola — this is the arcade's flight, not a random walk.
        // The move interval is 4 ROM frames = 4.8 ticks exactly (notes §52) —
        // `PortTicks(4)` = 4 made the flight 20% fast.
        _moveFifths += 5;
        if (_moveFifths >= GameplayConstants.SparkMoveIntervalRomTicks * 6)
        {
            _moveFifths -= GameplayConstants.SparkMoveIntervalRomTicks * 6;
            _velocityFp = new IntVector2(
                _velocityFp.X + _accelerationFp.X,
                _velocityFp.Y + _accelerationFp.Y);
        }

        // Mover (RRS22.ASM OPB80): the full 16-bit velocity is added to the
        // 16-bit world position once per ROM frame — a frame is 6/5 of a tick, so
        // the integration runs every 6 sixths, not every tick (that ran the spark
        // 60/50 = 20% fast, notes §93). The port carries the sub-pixel remainder so
        // the step never drifts.
        _moverSixths += 5;
        if (_moverSixths >= 6)
        {
            _moverSixths -= 6;
            _positionRemainderFp = new IntVector2(
                _positionRemainderFp.X + _velocityFp.X,
                _positionRemainderFp.Y + _velocityFp.Y);

            int stepX = _positionRemainderFp.X / _stepScale;
            int stepY = _positionRemainderFp.Y / _stepScale;
            _positionRemainderFp = new IntVector2(
                _positionRemainderFp.X - (stepX * _stepScale),
                _positionRemainderFp.Y - (stepY * _stepScale));

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

        // Port safety cap (the ROM relies on its 16-bit velocity saturating).
        stepX = Math.Clamp(stepX, -GameplayConstants.SparkMaxSpeed, GameplayConstants.SparkMaxSpeed);
        stepY = Math.Clamp(stepY, -GameplayConstants.SparkMaxSpeed, GameplayConstants.SparkMaxSpeed);

        // Walls: OPB80 REJECTS an axis update that would leave the playfield
        // (`CMPA #XMIN / BLO` and the width check against XMAX) — it does NOT
        // clamp. The object keeps its last valid coordinate on that axis while
        // the other axis keeps moving, which is why a spark slides along and
        // then stops at a wall instead of sticking to the edge. It dies only
        // when its life expires — no bounce, no wall-death.
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
