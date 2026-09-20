using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A spheroid — the gliding ring that gives birth to enforcers. It never aims: a
/// random acceleration is re-rolled every 1..15 bodies and damped back toward a
/// top speed of 1 column (2 arcade px) per frame on X and 2 rows per frame on Y —
/// the same speed in pixels — so it eases up to speed, and it CLAMPS against the
/// walls rather than reflecting. It flies OVER electrodes.
///
/// It cycles through three phases, each stepping the picture pointer by one on
/// its own body clock:
/// - spin (five ring pictures): the drop countdown decrements only on a full
///   five-picture wrap, so it counts ROTATIONS;
/// - drop (eight pictures): a shorter rotation countdown between drops, each of
///   which drops one enforcer unless eight are already out, until the allotment
///   (half of this wave's maximum, rounded up) is exhausted;
/// - escape: a straight sideways run — X fixed at 1 column a frame, Y stopped —
///   which spins the same five pictures the spin phase does and ends once the
///   spheroid leaves the field, whereupon it is removed with NO death animation.
///
/// A laser hit instead bursts it (the bespoke bubble is still to be built).
/// </summary>
/// <remarks>
/// The ROM's CIRCLE object (RRC11 `CIRCLE`, `CIRNAC`, `CIRGO`, `CIRC2L`, `CIRC3L`).
/// It GLIDES: the velocity is ACCUMULATED from a random acceleration that is
/// re-rolled every 1..15 bodies and damped back toward 64 x acceleration every
/// body, so it eases up to a top speed of 1 COLUMN (2 arcade px) per frame on X
/// and 2 ROWS per frame on Y — the same speed in pixels — and it CLAMPS against
/// the walls rather than reflecting (the generic mover rejects an out-of-bounds
/// axis, notes §43/§56). It flies OVER electrodes.
///
/// Its phases, each body being `NAP 2` = 3 ROM frames, and each advancing the
/// picture pointer by one (`OPICT += 4`) on ITS OWN body clock:
/// - CIRCLE (spin, pictures CIRP0..CIRP4): the drop countdown PD2 = RND(1..CDPTIM)
///   decrements only on a full 5-picture wrap, so it counts ROTATIONS (15 frames);
/// - CIRC2 (drop, CIRP0..CIRP7): PD2 = RND(1..CDPTIM/4) rotations between drops,
///   each of which drops one enforcer (ENFDRP) unless 8 are already out, until
///   the allotment (RND(1..ENFNUM) halved, rounded up = 1..5) is exhausted;
/// - CIRC3 (escape): OYV = 0 and OXV = exactly 1 column/frame, a straight
///   sideways run that spins the SAME five pictures the idle phase does and ends
///   once the column passes XMIN+3 or XMAX-10 — a test that itself runs only on
///   the wrap pass — whereupon the object is removed with NO death animation
///   (`CIR4`: KILLOF + SUCIDE).
/// A laser hit instead bursts it into the 7-step bubble (notes §50/§64).
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
    /// <remarks>
    /// `CIRCLE`/`CIRC3L` both compare against `CIRP4` (`CMPD #$1502` at R5 $11B6 / $1239
    /// with the `BLS` that stores), so the spin is CIRP0..CIRP4. §56.2 read the boundary
    /// as "wraps past CIRP3" and gave the port a four-picture spin (author, 2026-09-17:
    /// the spheroid "after giving birth to all the enforcers, looks weird animation
    /// wise", notes §90).
    /// </remarks>
    private const int SpinLastPicture = 4;

    /// <summary>The drop phase's wrap boundary, so it spins all EIGHT pictures.</summary>
    /// <remarks>ROM `CIRC2L`'s wrap boundary: `CMPD #$150E` = CIRP7.</remarks>
    private const int DropLastPicture = 7;

    private readonly Random _random;
    private readonly int _dropDelayRomTicks;
    private IntVector2 _position;
    private int _velocityXFp; // OXV: 1/256 COLUMN per frame (a column is 2 arcade px)
    private int _velocityYFp; // OYV: 1/256 ROW per frame
    private int _remainderXFp;
    private int _remainderYFp;
    private int _accelX; // PD5: -16..+15, in 1/256 column per frame per body
    private int _accelY; // PD6: -32..+31, in 1/256 row per frame per body
    private int _accelBodiesRemaining; // PD7: RANDU(15) = 1..15 bodies until a re-roll
    private int _bodyFifths;
    private int _moverSixths; // OPB80 cadence: one velocity integration per 6 sixths = 1 ROM frame
    private int _enforcersRemaining; // PD3: ceil(RND(1..ENFNUM)/2) = 1..5, never 0
    private int _dropRotationsRemaining; // PD2, counted in full picture ROTATIONS
    private int _rotation; // OPICT: 0..4 while spinning/escaping, 0..7 while dropping
    private bool _dropping; // CIRCLE (spin) -> CIRC2 (drop)
    private bool _escaping; // CIRC3: sideways run, then vanish
    private int _escapeDirection;

    /// <summary>Drops a spheroid at <paramref name="position"/> with its enforcer allotment already rolled; it is born mid-spin.</summary>
    /// <param name="position">Top-left of the spheroid.</param>
    /// <param name="random">The random source: the allotment, the accelerations and the escape direction.</param>
    /// <param name="maxDropsX2">This wave's enforcer-allotment bound; the roll happens here.</param>
    /// <param name="dropDelayRomTicks">This wave's rotation countdown, in ROM frames.</param>
    /// <remarks>
    /// These are ROM ENFNUM and CDPTIM for this wave (notes §11.2/§17).
    ///
    /// There is deliberately no speed parameter: the spheroid's speed IS the
    /// accumulated and damped CIRGO velocity, so a "speed bonus" cannot be
    /// expressed in this model (notes §56).
    /// </remarks>
    public Spheroid(IntVector2 position, Random random, int maxDropsX2 = 10, int dropDelayRomTicks = 24)
    {
        _position = position;
        _random = random;
        _dropDelayRomTicks = dropDelayRomTicks;
        // R5 $1193-119C: RMAX(ENFNUM) = RND(1..ENFNUM) (never 0), then LSR +
        // carry = ceil(v/2) → 1..5 drops, never zero (notes §17).
        int roll = random.Next(1, maxDropsX2 + 1);
        _enforcersRemaining = (roll + 1) / 2;
        // R5 CIRC3: the escape direction is the SIGN of the ROM's seed byte — a
        // coin flip here, since the port has no matching LFSR state.
        _escapeDirection = random.Next(2) == 0 ? -1 : 1;
        // R5 CIRSTL: PD2 = RND(1..CDPTIM) starts the SPIN phase's countdown; CIRNAC
        // (called there too) rolls the first accelerations and the re-roll timer.
        _dropRotationsRemaining = random.Next(1, dropDelayRomTicks + 1);
        // The mover moves it from the first frame (notes §93), so the mover
        // accumulator starts at one full frame.
        _moverSixths = 6;
        // `MKPROB CIRCLE,CIRP4,CIRKIL` → MPROB's `LDD ,U++ / STD OLDPIC,X /
        // STD OPICT,X` puts CIRP4 in the new object's picture, so a spheroid is BORN
        // showing the medium ring and its FIRST body is already a wrap pass.
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
    /// <remarks>The ROM's `OPICT` pointer.</remarks>
    internal int PictureIndex => _rotation;

    /// <summary>Test hook: true once the sideways exit run has started.</summary>
    /// <remarks>ROM CIRC3.</remarks>
    internal bool IsEscaping => _escaping;

    /// <summary>
    /// Kills the spheroid: it goes straight to Dead, so the field's explosion is
    /// the whole visual — there is no blink.
    /// </summary>
    /// <remarks>
    /// Laser kill (ROM RRC11 `CIRKIL`): `JSR KILOFP` (image and process off)
    /// then `MAKP CIRKP` — a 7-frame BUBBLE BURST in slot 10's colour, followed
    /// by the "1000" score picture for 30 steps in slot $FF. So there is no
    /// blink here; the burst itself is still to be built (notes §50), and until
    /// then the object simply bursts via the field's explosion.
    /// </remarks>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>
    /// Runs the spheroid: the generic mover's per-frame glide, then the body — the phase's own
    /// logic (accelerate and damp, or drop an enforcer, or run for the exit) plus one picture
    /// step, with the phase's countdown living on the picture's WRAP pass.
    /// </summary>
    /// <param name="gameTime">Unused — the mover and body clocks are counted in ROM frames.</param>
    /// <param name="field">The playfield: the walls to clamp against and the enforcer-drop cap.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        // The generic mover ($OPB80, notes §43) runs once per ROM FRAME, independently
        // of how often this object's own process wakes up — the escape included, whose
        // OXV is a fixed ±1 column/frame. A frame is 6/5 of a tick, so the velocity is
        // integrated every 6 sixths, not every tick: integrating per tick ran the
        // spheroid 60/50 = 20% fast (notes §93).
        _moverSixths += 5;
        if (_moverSixths >= 6)
        {
            _moverSixths -= 6;
            AdvancePosition(field);
        }

        // Every phase's process is `NAP 2` = 3 ROM frames = 3.6 port ticks (notes
        // §43: `NAP n` is n + 1 frames), so the PICTURE and the phase's own logic
        // step on the body clock. The escape used to advance its picture on every
        // port tick instead — 3.6x the arcade's rate, a 15 Hz strobe of a picture
        // that is still growing — which is what the author saw once a spheroid had
        // dropped its last enforcer (notes §90).
        _bodyFifths += 5;
        if (_bodyFifths < GameplayConstants.SpheroidBodyRomFrames * 6)
        {
            return;
        }

        _bodyFifths -= GameplayConstants.SpheroidBodyRomFrames * 6;

        // ROM `ADDD #4 / CMPD #<last> / BLS …`: the picture advances one entry a body
        // unless it is already at the phase's LAST picture. That body is the WRAP
        // pass: the phase's own countdown (or, for the escape, the X exit test) lives
        // there, and on some branches the store is SKIPPED so the picture is held.
        int lastPicture = _dropping && !_escaping ? DropLastPicture : SpinLastPicture;
        bool wrapPass = _rotation >= lastPicture;

        if (_escaping)
        {
            // CIRC3L. The exit test sits INSIDE the wrap branch, so it is evaluated
            // once per five-picture cycle, not every body — the ROM's `CMPA #XMIN+3` /
            // `CMPA #XMAX-10`; a spheroid that runs out of field before its next wrap
            // pass just stops at the wall (the mover rejects the step) and leaves on it.
            if (wrapPass)
            {
                Rectangle bounds = field.Wall.PlayfieldBounds;
                if (_position.X <= bounds.X + ScreenSize.Scaled(2 * GameplayConstants.SpheroidEscapeExitLeftColumn) ||
                    _position.X >= ScreenSize.Scaled(2 * GameplayConstants.SpheroidEscapeExitRightColumn))
                {
                    LifeState = EntityLifeState.Dead; // CIR4: KILLOF + SUCIDE — removed, no burst
                    return;
                }

                _rotation = 0;
                return;
            }

            _rotation++;
            return;
        }

        // CIRGO (accumulate, clamp, damp) and the PD7 re-roll run once per BODY in
        // both CIRCLE and CIRC2L; CIRC3L never calls them (its velocity is fixed).
        AccelerateAndDamp();
        if (--_accelBodiesRemaining <= 0)
        {
            RollAccelerations();
        }

        if (!wrapPass)
        {
            _rotation++;
            return;
        }

        // ---- the WRAP pass: the phase's countdown ----------------------------------
        // CIRCLE's `TST STATUS / BNE CIRC1` stores the wrap target (CIRP0) without
        // decrementing, so a held game keeps spinning and accelerating but the SPIN
        // countdown does not advance. CIRC2L and CIRC3L have no such test — the check
        // is per routine, not per object (§88's trap) — so a dropping spheroid keeps
        // counting through a pause. The port used to gate both phases.
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
            // CIRCLE -> CIRC2 (`BEQ CIRC2`): the pointer is NOT stored on this branch,
            // so the drop phase carries the SAME pointer on — CIRP4 held for this body,
            // then CIRP5, CIRP6, CIRP7. (The port used to restart the cycle at CIRP0.)
            _dropping = true;
            RerollDropCountdown();
            return;
        }

        // CIRC2L: `ENFCNT >= 8` and the ROM's `$42 >= 17` test gate the drop; a capped
        // drop is NOT deferred, it just re-rolls the countdown.
        if (field.CanDropEnforcer)
        {
            field.SpawnEnforcer(_position);
            if (--_enforcersRemaining <= 0)
            {
                StartEscape(); // CIRC3 — the pointer stays on CIRP7; the first escape body wraps it
                return;
            }
        }

        RerollDropCountdown();
        _rotation = 0;
    }

    /// <summary>Re-rolls both accelerations and the 1..15-body re-roll timer.</summary>
    /// <remarks>ROM CIRNAC.</remarks>
    private void RollAccelerations()
    {
        // PD5 = (HSEED &amp; $1F) - $10 → -16..+15 (1/256 column per frame per body).
        // PD6 = ((LSEED ^ SEED) &amp; $3F) - $20 → -32..+31 — twice as large because a
        // column is 2 px, so the two axes reach the same speed IN PIXELS.
        _accelX = _random.Next(0, 32) - 16;
        _accelY = _random.Next(0, 64) - 32;
        _accelBodiesRemaining = 1 + _random.Next(0, 15); // RANDU(15)
    }

    /// <summary>Re-arms the drop countdown: a random 1..(this wave's delay / 4) rotations between drops.</summary>
    /// <remarks>ROM CIRC2's re-arm: PD2 = RMAX(CDPTIM/4) = RND(1..CDPTIM/4) rotations between drops.</remarks>
    private void RerollDropCountdown() =>
        _dropRotationsRemaining = 1 + _random.Next(0, _dropDelayRomTicks / 4);

    /// <summary>
    /// Starts the escape: Y velocity 0, X velocity exactly ±1 column per frame,
    /// forever. The picture pointer is deliberately NOT touched.
    /// </summary>
    /// <remarks>
    /// ROM CIRC3. The ROM leaves the pointer on CIRP7 (the last drop-phase picture)
    /// and the first escape body wraps it to CIRP0.
    /// </remarks>
    private void StartEscape()
    {
        _escaping = true;
        _velocityXFp = _escapeDirection * GameplayConstants.SpheroidMaxVelocityXFp;
        _velocityYFp = 0;
        _remainderXFp = 0;
        _remainderYFp = 0;
    }

    /// <summary>Adds the acceleration, clamps to the top speed, then damps by a 64th.</summary>
    /// <remarks>ROM CIRGO.</remarks>
    private void AccelerateAndDamp()
    {
        _velocityXFp = ClampThenDamp(_velocityXFp + _accelX, GameplayConstants.SpheroidMaxVelocityXFp);
        _velocityYFp = ClampThenDamp(_velocityYFp + _accelY, GameplayConstants.SpheroidMaxVelocityYFp);
    }

    /// <summary>
    /// Clamps the velocity to the limit, then damps it toward 64 x the acceleration —
    /// which is why the limit is the usual terminal state.
    /// </summary>
    /// <remarks>
    /// ROM CIRGO's second half — `COMA COMB / ASLB ROLA / ASLB ROLA / TFR A,B / SEX /
    /// ADDD OXV,X`: complement the 16-bit velocity, shift it left twice (so it is
    /// -4v - 4), keep the HIGH byte sign-extended and add that. The velocity therefore
    /// converges on 64 x acceleration, which is why the clamp (±$0100 = 1 column/frame
    /// on X, ±$0200 = 2 rows/frame on Y) is the usual terminal state.
    /// </remarks>
    private static int ClampThenDamp(int velocityFp, int limitFp)
    {
        velocityFp = Math.Clamp(velocityFp, -limitFp, limitFp);
        return velocityFp + (((-4 * velocityFp) - 4) >> 8);
    }

    /// <summary>
    /// Integrates the 1/256-unit velocity on both axes for one frame. An axis whose
    /// step would leave the field is REJECTED — the old coordinate (and its fraction)
    /// is kept, so the spheroid slides along the wall instead of reflecting off it.
    /// </summary>
    /// <remarks>The generic ROM mover ($OPB80, notes §43) integrates the velocity
    /// EVERY frame, and the fraction lives in the coordinate's low byte.</remarks>
    private void AdvancePosition(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        _position = new IntVector2(
            AdvanceAxis(_position.X, _velocityXFp, ref _remainderXFp, bounds.X, bounds.Right - CollisionSize.Width),
            AdvanceAxis(_position.Y, _velocityYFp, ref _remainderYFp, bounds.Y, bounds.Bottom - CollisionSize.Height));
    }

    /// <summary>
    /// One axis of the ROM mover's step: whole pixels of the 1/256-unit velocity, with the
    /// fraction kept for next time — unless the step would leave the field, in which case the
    /// coordinate AND the fraction are left exactly as they were.
    /// </summary>
    private static int AdvanceAxis(int position, int velocityFp, ref int remainderFp, int min, int max)
    {
        int nextRemainder = remainderFp + velocityFp;
        int step = nextRemainder >> 8;
        int next = position + step;
        if (next < min || next > max)
        {
            return position; // out of bounds: the ROM keeps the old coordinate AND fraction
        }

        remainderFp = nextRemainder - (step << 8);
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

        // NO flash. spec.txt's "flashing light green" describes the SPHEROID'S
        // LOOK, not a visibility toggle: its art is rendered in colour-cycling
        // palette slots, so the shimmer comes from the palette (the M4 marker
        // remap) exactly as in the arcade. Author, 2026-09-16: "The spheroid is
        // rendered in a palette index which colour cycles, I think."

        // No death animation: the ROM turns the object off and bursts it
        // immediately (notes §50) — see Kill().

        // ROM animation: `OPICT += 4` per body, wrapping at CIRP4 while spinning or
        // escaping (pictures 0..4) and at CIRP7 while dropping (0..7) — one picture
        // per 3-frame body, NOT a fixed tick clock (notes §56, §90).
        sprites.DrawSprite(spriteBatch, sprites.SpheroidFrames[_rotation % sprites.SpheroidFrames.Length], Bounds, Color.White);
    }

    /// <summary>The current picture, for the death burst (see <see cref="IArtSource"/> and <see cref="ScoreBurst.ForSpheroid"/>).</summary>
    /// <param name="sprites">The shared sprite set, which holds the spheroid frames.</param>
    /// <returns>The texture for the current rotation frame.</returns>
    public Texture2D CurrentFrameArt(SpriteSet sprites)
        => sprites.SpheroidFrames[_rotation % sprites.SpheroidFrames.Length];
}
