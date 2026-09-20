using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A spheroid — a drifting ring on screen that is harmless in itself but is the
/// enemy that gives birth to enforcers (a separate, dangerous entity). It never
/// aims or shoots at the player: it simply glides, changing its own heading at
/// random, until it has dropped its quota of enforcers, then makes a beeline for
/// the edge of the screen and disappears. See the terminology glossary on
/// <see cref="IEntity"/> for "beat", "ROM frame" and "fifths" — the units this
/// class's timers are counted in.
///
/// Its glide is not steered toward anything: a random acceleration is re-rolled
/// every 1..15 beats and damped back toward a top speed of 1 column (2 arcade px)
/// per frame on X and 2 rows per frame on Y — the same speed in pixels — so it
/// eases up to speed, and it CLAMPS against the walls (stops, rather than
/// bouncing off) rather than reflecting. It flies OVER electrodes (the wall
/// hazard other ground entities must avoid).
///
/// It cycles through three phases, each stepping the picture pointer by one on
/// its own beat clock:
/// - spin (five ring pictures): the drop countdown decrements only on a full
///   five-picture wrap, so it counts ROTATIONS;
/// - drop (eight pictures): a shorter rotation countdown between drops, each of
///   which drops one enforcer unless eight are already out, until the allotment
///   (half of this wave's maximum, rounded up) is exhausted;
/// - escape: a straight sideways run — X fixed at 1 column a frame, Y stopped —
///   which spins the same five pictures the spin phase does and ends once the
///   spheroid leaves the field, whereupon it is removed with NO death animation
///   (it just vanishes off the edge; nothing plays).
///
/// A laser hit instead bursts it (the bespoke bubble is still to be built).
/// </summary>
/// <remarks>
/// The arcade's CIRCLE object (ROM: RRC11.ASM, the `CIRCLE`/`CIRNAC`/`CIRGO`/`CIRC2L`/`CIRC3L`
/// routines; notes §43/§56). It CLAMPS against the walls rather than bouncing (the
/// generic mover rejects a step that would leave the field on either axis) and flies
/// OVER electrodes. A laser hit instead bursts it into the 7-step bubble (notes §50/§64).
/// </remarks>
public sealed class Spheroid : IEntity, IArtSource
{
    /// <summary>Collision box = the ROM picture dimensions (16x15 arcade px), top-left anchored at <see cref="Position"/>.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.SpheroidCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.SpheroidCollisionSize.Height));

    /// <summary>
    /// The last picture of the spin and of the escape, i.e. the pointer value whose
    /// step wraps. Those two phases therefore spin FIVE pictures, 0..4.
    /// </summary>
    /// <remarks>Both the spin and escape routines wrap after the fifth picture (index 4)
    /// (ROM: `CIRCLE`/`CIRC3L`; notes §90).</remarks>
    private const int SpinLastPicture = 4;

    /// <summary>The drop phase's wrap boundary, so it spins all EIGHT pictures.</summary>
    /// <remarks>The drop phase wraps after its eighth picture (ROM: `CIRC2L`).</remarks>
    private const int DropLastPicture = 7;

    private readonly Random _random;
    private readonly int _dropDelayRomTicks;
    private IntVector2 _position;
    private int _velocityXSubpixels; // X velocity, in 1/256 column per frame (a column is 2 arcade px)
    private int _velocityYSubpixels; // Y velocity, in 1/256 row per frame
    private int _remainderXSubpixels;
    private int _remainderYSubpixels;
    private int _accelX; // current X acceleration: -16..+15, in 1/256 column per frame per beat
    private int _accelY; // current Y acceleration: -32..+31, in 1/256 row per frame per beat
    private int _accelBeatsRemaining; // beats left before the accelerations are re-rolled: 1..15
    private int _beatTimer;
    private int _moveTimer; // glide cadence: one velocity integration per 6 fifth-ticks = 1 ROM frame
    private int _enforcersRemaining; // how many enforcers this spheroid still owes: 1..5, never 0
    private int _dropRotationsRemaining; // rotations left until the next enforcer drop
    private int _rotation; // current picture: 0..4 while spinning/escaping, 0..7 while dropping
    private bool _dropping; // spinning -> dropping enforcers
    private bool _escaping; // sideways run toward the edge of the field, then vanish
    private int _escapeDirection;

    /// <summary>Drops a spheroid at <paramref name="position"/> with its enforcer allotment already rolled; it is born mid-spin.</summary>
    /// <param name="position">Top-left of the spheroid.</param>
    /// <param name="random">The random source: the allotment, the accelerations and the escape direction.</param>
    /// <param name="maxDropsX2">This wave's enforcer-allotment bound; the roll happens here.</param>
    /// <param name="dropDelayRomTicks">This wave's rotation countdown, in ROM frames.</param>
    /// <remarks>
    /// These are this wave's enforcer allotment bound and rotation delay (ROM: ENFNUM
    /// and CDPTIM, notes §11.2/§17).
    ///
    /// There is deliberately no speed parameter: the spheroid's speed IS its
    /// accumulated, damped glide velocity, so a "speed bonus" cannot be expressed in
    /// this model (notes §56).
    /// </remarks>
    public Spheroid(IntVector2 position, Random random, int maxDropsX2 = 10, int dropDelayRomTicks = 24)
    {
        _position = position;
        _random = random;
        _dropDelayRomTicks = dropDelayRomTicks;
        // The enforcer allotment is a random roll (never 0), halved and rounded up, so
        // a spheroid always owes 1..5 enforcers, never zero (notes §17).
        int roll = random.Next(1, maxDropsX2 + 1);
        _enforcersRemaining = (roll + 1) / 2;
        // The escape direction is effectively a coin flip (the arcade derives it from
        // its own random-seed byte, which this port doesn't reproduce bit-for-bit).
        _escapeDirection = random.Next(2) == 0 ? -1 : 1;
        // The spin phase's drop countdown starts as a random 1..(this wave's delay)
        // rotations, and the same setup rolls the first accelerations and their
        // re-roll timer.
        _dropRotationsRemaining = random.Next(1, dropDelayRomTicks + 1);
        // The mover moves it from the first frame (notes §93), so the mover
        // accumulator starts at one full frame.
        _moveTimer = 6;
        // A spheroid is BORN already showing the medium ring (the spin phase's last
        // picture), so its very first beat is already a wrap pass.
        _rotation = SpinLastPicture;
        RollAccelerations();
    }

    /// <summary>Top-left of the spheroid (the ROM's OBJX/OBJY).</summary>
    public IntVector2 Position => _position;

    /// <summary>The spheroid picture's own 16x15 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until shot or until it finishes its sideways escape; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>
    /// Test hook: which picture the spheroid is showing — 0..4 while spinning or
    /// escaping, 0..7 while dropping.
    /// </summary>
    /// <remarks>The ROM's current-picture pointer.</remarks>
    internal int PictureIndex => _rotation;

    /// <summary>Test hook: true once the sideways exit run has started.</summary>
    /// <remarks>ROM: the `CIRC3` escape phase.</remarks>
    internal bool IsEscaping => _escaping;

    /// <summary>
    /// Kills the spheroid: it goes straight to Dead, so the field's explosion is
    /// the whole visual — there is no blink.
    /// </summary>
    /// <remarks>ROM: RRC11.ASM's `CIRKIL` plays a 7-frame bubble burst then a "1000"
    /// score picture; the bespoke burst is still to be built (notes §50) — until then
    /// it uses the field's generic explosion.</remarks>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>
    /// Runs the spheroid: the generic mover's per-frame glide, then the beat — the phase's own
    /// logic (accelerate and damp, or drop an enforcer, or run for the exit) plus one picture
    /// step, with the phase's countdown living on the picture's WRAP pass.
    /// </summary>
    /// <param name="gameTime">Unused — the mover and beat clocks are counted in ROM frames.</param>
    /// <param name="field">The playfield: the walls to clamp against and the enforcer-drop cap.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        // The generic mover (notes §43) runs once per ROM FRAME, independently of how
        // often this object's own process wakes up — the escape included, whose X
        // velocity is a fixed ±1 column/frame. A frame is 6/5 of a tick, so the
        // velocity is integrated every 6 fifth-ticks, not every tick: integrating per tick
        // ran the spheroid 20% too fast (notes §93).
        _moveTimer += 5;
        if (_moveTimer >= 6)
        {
            _moveTimer -= 6;
            AdvancePosition(field);
        }

        // Every phase's process runs every 3 ROM frames = 3.6 port ticks (notes §43),
        // so the picture and the phase's own logic step on that same beat clock,
        // including the escape (notes §90).
        _beatTimer += 5;
        if (_beatTimer < GameplayConstants.SpheroidBeatRomFrames * 6)
        {
            return;
        }

        _beatTimer -= GameplayConstants.SpheroidBeatRomFrames * 6;

        // The picture advances one entry a beat unless it is already at the phase's
        // LAST picture. That beat is the WRAP pass: the phase's own countdown (or, for
        // the escape, the exit-toward-the-edge test) lives there, and on some branches
        // the picture is deliberately held instead of advanced.
        int lastPicture = _dropping && !_escaping ? DropLastPicture : SpinLastPicture;
        bool wrapPass = _rotation >= lastPicture;

        if (_escaping)
        {
            // The exit test sits INSIDE the wrap branch, so it is evaluated once per
            // five-picture cycle, not every beat; a spheroid that runs out of field
            // before its next wrap pass just stops at the wall (the mover rejects the
            // step) and leaves on it. (ROM: `CIRC3L`.)
            if (wrapPass)
            {
                Rectangle bounds = field.Wall.PlayfieldBounds;
                if (_position.X <= bounds.X + ScreenSize.Scaled(2 * GameplayConstants.SpheroidEscapeExitLeftColumn) ||
                    _position.X >= ScreenSize.Scaled(2 * GameplayConstants.SpheroidEscapeExitRightColumn))
                {
                    LifeState = EntityLifeState.Dead; // removed at once, no burst (ROM: `CIR4`)
                    return;
                }

                _rotation = 0;
                return;
            }

            _rotation++;
            return;
        }

        // Accumulate/clamp/damp the velocity, and re-roll the acceleration timer, once
        // per beat while spinning or dropping; the escape never touches either (its
        // velocity is fixed).
        AccelerateAndDamp();
        if (--_accelBeatsRemaining <= 0)
        {
            RollAccelerations();
        }

        if (!wrapPass)
        {
            _rotation++;
            return;
        }

        // ---- the WRAP pass: the phase's countdown ----------------------------------
        // While spinning, a frozen game holds the picture at its wrap target without
        // decrementing the countdown, so a held spheroid keeps spinning and
        // accelerating but its SPIN countdown does not advance. The drop and escape
        // phases have no such freeze check of their own — the arcade's freeze test is
        // per routine, not per object (§88's trap) — so a dropping spheroid keeps
        // counting through a pause. The port used to (incorrectly) freeze both phases.
        if (!_dropping && field.RobotsFrozen)
        {
            _rotation = 0;
            return;
        }

        if (--_dropRotationsRemaining > 0)
        {
            _rotation = 0;
            return;
        }

        if (!_dropping)
        {
            // Switching from spin to drop does NOT reset the picture pointer, so the
            // drop phase carries the SAME picture on for one more beat, then continues
            // through the rest of its own set. (The port used to wrongly restart the
            // cycle at picture 0.)
            _dropping = true;
            RerollDropCountdown();
            return;
        }

        // A drop that is capped by the field's enforcer limit is NOT deferred — it
        // just re-rolls the countdown and tries again next time.
        if (field.CanDropEnforcer)
        {
            field.SpawnEnforcer(_position);
            if (--_enforcersRemaining <= 0)
            {
                StartEscape(); // the picture stays where it is; the first escape beat wraps it
                return;
            }
        }

        RerollDropCountdown();
        _rotation = 0;
    }

    /// <summary>Re-rolls both accelerations and the 1..15-beat re-roll timer.</summary>
    /// <remarks>ROM: `CIRNAC`.</remarks>
    private void RollAccelerations()
    {
        // X acceleration: a random -16..+15, in 1/256 column per frame per beat.
        // Y acceleration: a random -32..+31 — twice as large because a column is 2
        // pixels, so the two axes reach the same speed IN PIXELS.
        _accelX = _random.Next(0, 32) - 16;
        _accelY = _random.Next(0, 64) - 32;
        _accelBeatsRemaining = 1 + _random.Next(0, 15); // 1..15 beats until the next re-roll
    }

    /// <summary>Re-arms the drop countdown: a random 1..(this wave's delay / 4) rotations between drops.</summary>
    /// <remarks>ROM: the `CIRC2` re-arm.</remarks>
    private void RerollDropCountdown() =>
        _dropRotationsRemaining = 1 + _random.Next(0, _dropDelayRomTicks / 4);

    /// <summary>
    /// Starts the escape: Y velocity 0, X velocity exactly ±1 column per frame,
    /// forever. The picture pointer is deliberately NOT touched.
    /// </summary>
    /// <remarks>
    /// The arcade leaves the picture on the last drop-phase frame, and the first
    /// escape beat wraps it back to the first spin picture (ROM: `CIRC3`).
    /// </remarks>
    private void StartEscape()
    {
        _escaping = true;
        _velocityXSubpixels = _escapeDirection * GameplayConstants.SpheroidMaxVelocityXSubpixels;
        _velocityYSubpixels = 0;
        _remainderXSubpixels = 0;
        _remainderYSubpixels = 0;
    }

    /// <summary>Adds the acceleration, clamps to the top speed, then damps by a 64th.</summary>
    /// <remarks>ROM: `CIRGO`.</remarks>
    private void AccelerateAndDamp()
    {
        _velocityXSubpixels = ClampThenDamp(_velocityXSubpixels + _accelX, GameplayConstants.SpheroidMaxVelocityXSubpixels);
        _velocityYSubpixels = ClampThenDamp(_velocityYSubpixels + _accelY, GameplayConstants.SpheroidMaxVelocityYSubpixels);
    }

    /// <summary>
    /// Clamps the velocity to the limit, then damps it toward 64 x the acceleration —
    /// which is why the limit is the usual terminal state.
    /// </summary>
    /// <remarks>
    /// The arcade's damping arithmetic amounts to nudging the velocity toward -4x
    /// itself minus a small constant each beat, which makes it converge on 64 times the
    /// acceleration — and that is why the speed clamp (1 column/frame on X, 2
    /// rows/frame on Y) ends up being the usual terminal state. (ROM: the second half
    /// of `CIRGO`.)
    /// </remarks>
    private static int ClampThenDamp(int velocitySubpixels, int limitSubpixels)
    {
        velocitySubpixels = Math.Clamp(velocitySubpixels, -limitSubpixels, limitSubpixels);
        return velocitySubpixels + (((-4 * velocitySubpixels) - 4) >> 8);
    }

    /// <summary>
    /// Integrates the 1/256-unit velocity on both axes for one frame. An axis whose
    /// step would leave the field is REJECTED — the old coordinate (and its fraction)
    /// is kept, so the spheroid slides along the wall instead of reflecting off it.
    /// </summary>
    /// <remarks>The arcade's generic mover (notes §43) integrates the velocity EVERY
    /// frame, and the fraction lives in the coordinate's low byte.</remarks>
    private void AdvancePosition(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        _position = new IntVector2(
            AdvanceAxis(_position.X, _velocityXSubpixels, ref _remainderXSubpixels, bounds.X, bounds.Right - CollisionSize.Width),
            AdvanceAxis(_position.Y, _velocityYSubpixels, ref _remainderYSubpixels, bounds.Y, bounds.Bottom - CollisionSize.Height));
    }

    /// <summary>
    /// One axis of the ROM mover's step: whole pixels of the 1/256-unit velocity, with the
    /// fraction kept for next time — unless the step would leave the field, in which case the
    /// coordinate AND the fraction are left exactly as they were.
    /// </summary>
    private static int AdvanceAxis(int position, int velocitySubpixels, ref int remainderSubpixels, int min, int max)
    {
        int nextRemainder = remainderSubpixels + velocitySubpixels;
        int step = nextRemainder >> 8;
        int next = position + step;
        if (next < min || next > max)
        {
            return position; // out of bounds: keep the old coordinate AND its carried fraction
        }

        remainderSubpixels = nextRemainder - (step << 8);
        return next;
    }

    /// <summary>
    /// Draws the current picture. The spheroid's shimmer is NOT a flash: its art is drawn in
    /// colour-cycling palette slots, so the palette supplies the shimmer (as the arcade's does).
    /// </summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the spheroid frames.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        // No flash: spec.txt's "flashing light green" describes the spheroid's
        // colour-cycling palette slot (the M4 marker remap), not a visibility toggle.

        // No death animation: the ROM turns the object off and bursts it
        // immediately (notes §50) — see Kill().

        // The picture advances one entry per beat, wrapping at the fifth picture while
        // spinning or escaping (0..4) and at the eighth while dropping (0..7) — one
        // picture per 3-frame beat, NOT a fixed tick clock (notes §56, §90).
        sprites.DrawSprite(spriteBatch, sprites.SpheroidFrames[_rotation % sprites.SpheroidFrames.Length], Bounds, Color.White);
    }

    /// <summary>The current picture, for the death burst (see <see cref="IArtSource"/> and <see cref="ScoreBurst.ForSpheroid"/>).</summary>
    /// <param name="sprites">The shared sprite set, which holds the spheroid frames.</param>
    /// <returns>The texture for the current rotation frame.</returns>
    public Texture2D CurrentFrameArt(SpriteSet sprites)
        => sprites.SpheroidFrames[_rotation % sprites.SpheroidFrames.Length];
}
