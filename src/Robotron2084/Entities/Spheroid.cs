using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The ROM's CIRCLE object (RRC11 `CIRCLE`, `CIRNAC`, `CIRGO`, `CIRC2L`, `CIRC3L`).
/// It GLIDES: the velocity is ACCUMULATED from a random acceleration that is
/// re-rolled every 1..15 bodies and damped back toward 64 x acceleration every
/// body, so it eases up to a top speed of 1 COLUMN (2 arcade px) per frame on X
/// and 2 ROWS per frame on Y — the same speed in pixels — and it CLAMPS against
/// the walls rather than reflecting (the generic mover rejects an out-of-bounds
/// axis, notes §43/§56). It flies OVER electrodes.
///
/// Its phases, each body being `NAP 2` = 3 ROM frames:
/// - CIRCLE (spin, 4 pictures): the drop countdown PD2 = RND(1..CDPTIM)
///   decrements only on a full 4-picture wrap, so it counts ROTATIONS (12 frames);
/// - CIRC2 (drop, 8 pictures): PD2 = RND(1..CDPTIM/4) rotations between drops,
///   each of which drops one enforcer (ENFDRP) unless 8 are already out, until
///   the allotment (RND(1..ENFNUM) halved, rounded up = 1..5) is exhausted;
/// - CIRC3 (escape): OYV = 0 and OXV = exactly 1 column/frame, a straight
///   sideways run that ends once the column passes XMIN+3 or XMAX-10, whereupon
///   the object is removed with NO death animation (`CIR4`: KILLOF + SUCIDE).
/// A laser hit instead bursts it into the 7-step bubble (still to build,
/// notes §50).
/// </summary>
public sealed class Spheroid : IEntity, IArtSource
{
    /// <summary>Collision box = the ROM picture dimensions (16x15 arcade px), top-left anchored at <see cref="Position"/>.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.SpheroidCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.SpheroidCollisionSize.Height));

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
    private int _enforcersRemaining; // PD3: ceil(RND(1..ENFNUM)/2) = 1..5, never 0
    private int _dropRotationsRemaining; // PD2, counted in full picture ROTATIONS
    private int _rotation; // OPICT: 0..3 while spinning, 0..7 while dropping
    private bool _dropping; // CIRCLE (spin) -> CIRC2 (drop)
    private bool _escaping; // CIRC3: sideways run, then vanish
    private int _escapeDirection;

    /// <param name="maxDropsX2">ROM ENFNUM for this wave; the roll happens here (notes §11.2/§17).</param>
    /// <param name="dropDelayRomTicks">ROM CDPTIM for this wave.</param>
    /// <remarks>
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
        RollAccelerations();
    }

    public IntVector2 Position => _position;

    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>
    /// Laser kill (ROM RRC11 `CIRKIL`): `JSR KILOFP` (image and process off)
    /// then `MAKP CIRKP` — a 7-frame BUBBLE BURST in slot 10's colour, followed
    /// by the "1000" score picture for 30 steps in slot $FF. So there is no
    /// blink here; the burst itself is still to be built (notes §50), and until
    /// then the object simply bursts via the field's explosion.
    /// </summary>
    public void Kill() => LifeState = EntityLifeState.Dead;

    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        Rectangle bounds = field.Wall.PlayfieldBounds;

        // CIRC3: the escape is a straight sideways run — OYV = 0, OXV = ±$0100,
        // exactly 1 column = 2 arcade px per frame — which ends once the column
        // passes XMIN+3 or XMAX-10; CIR4 then removes the object with no burst.
        if (_escaping)
        {
            AdvancePosition(field);
            _rotation = (_rotation + 1) % 4;
            if (_position.X <= bounds.X + ScreenSize.Scaled(2 * GameplayConstants.SpheroidEscapeExitLeftColumn) ||
                _position.X >= ScreenSize.Scaled(2 * GameplayConstants.SpheroidEscapeExitRightColumn))
            {
                LifeState = EntityLifeState.Dead;
            }

            return;
        }

        // The generic mover ($OPB80, notes §43) runs EVERY frame, independently of
        // how often this object's own process wakes up.
        AdvancePosition(field);

        // The CIRCLE body is `NAP 2` = 3 ROM frames (notes §43: NAP n is n + 1 frames).
        _bodyFifths += 5;
        if (_bodyFifths < GameplayConstants.SpheroidBodyRomFrames * 6)
        {
            return;
        }

        _bodyFifths -= GameplayConstants.SpheroidBodyRomFrames * 6;

        // CIRCLE/CIRC2L: `OPICT += 4` then wrap at the phase's last picture — 4
        // pictures while spinning, 8 once dropping. The drop countdown (PD2) is
        // decremented only on the wrap pass, so it counts ROTATIONS, not bodies.
        _rotation = (_rotation + 1) % (_dropping ? 8 : 4);
        AccelerateAndDamp();

        if (--_accelBodiesRemaining <= 0)
        {
            RollAccelerations();
        }

        // `TST STATUS / BNE CIRC1`: a frozen game keeps spinning (and accelerating)
        // but never advances the drop countdown.
        if (_rotation != 0 || field.RobotsFrozen)
        {
            return;
        }

        if (--_dropRotationsRemaining > 0)
        {
            return;
        }

        if (!_dropping)
        {
            // CIRC1 -> CIRC2: start dropping, on a fresh RND(1..CDPTIM/4) countdown.
            _dropping = true;
            RerollDropCountdown();
            return;
        }

        // CIRC2L: drop one enforcer unless the arcade's 8-enforcer cap is reached;
        // a capped drop is NOT deferred, it simply re-rolls the countdown.
        if (field.CanDropEnforcer)
        {
            field.SpawnEnforcer(_position);
            if (--_enforcersRemaining <= 0)
            {
                StartEscape();
                return;
            }
        }

        RerollDropCountdown();
    }

    /// <summary>ROM CIRNAC: re-roll both accelerations and the 1..15-body re-roll timer.</summary>
    private void RollAccelerations()
    {
        // PD5 = (HSEED &amp; $1F) - $10 → -16..+15 (1/256 column per frame per body).
        // PD6 = ((LSEED ^ SEED) &amp; $3F) - $20 → -32..+31 — twice as large because a
        // column is 2 px, so the two axes reach the same speed IN PIXELS.
        _accelX = _random.Next(0, 32) - 16;
        _accelY = _random.Next(0, 64) - 32;
        _accelBodiesRemaining = 1 + _random.Next(0, 15); // RANDU(15)
    }

    /// <summary>ROM CIRC2's re-arm: PD2 = RMAX(CDPTIM/4) = RND(1..CDPTIM/4) rotations between drops.</summary>
    private void RerollDropCountdown() =>
        _dropRotationsRemaining = 1 + _random.Next(0, _dropDelayRomTicks / 4);

    /// <summary>ROM CIRC3: Y velocity 0, X velocity exactly ±1 column per frame, forever.</summary>
    private void StartEscape()
    {
        _escaping = true;
        _rotation = 0;
        _velocityXFp = _escapeDirection * GameplayConstants.SpheroidMaxVelocityXFp;
        _velocityYFp = 0;
        _remainderXFp = 0;
        _remainderYFp = 0;
    }

    /// <summary>ROM CIRGO: add the acceleration, clamp to the limit, then damp by a 64th.</summary>
    private void AccelerateAndDamp()
    {
        _velocityXFp = ClampThenDamp(_velocityXFp + _accelX, GameplayConstants.SpheroidMaxVelocityXFp);
        _velocityYFp = ClampThenDamp(_velocityYFp + _accelY, GameplayConstants.SpheroidMaxVelocityYFp);
    }

    /// <summary>
    /// ROM CIRGO's second half — `COMA COMB / ASLB ROLA / ASLB ROLA / TFR A,B / SEX /
    /// ADDD OXV,X`: complement the 16-bit velocity, shift it left twice (so it is
    /// -4v - 4), keep the HIGH byte sign-extended and add that. The velocity therefore
    /// converges on 64 x acceleration, which is why the clamp (±$0100 = 1 column/frame
    /// on X, ±$0200 = 2 rows/frame on Y) is the usual terminal state.
    /// </summary>
    private static int ClampThenDamp(int velocityFp, int limitFp)
    {
        velocityFp = Math.Clamp(velocityFp, -limitFp, limitFp);
        return velocityFp + (((-4 * velocityFp) - 4) >> 8);
    }

    /// <summary>
    /// The two axes of the generic ROM mover ($OPB80, notes §43): the 1/256-unit
    /// velocity is integrated EVERY frame, and an axis whose step would leave the
    /// field is REJECTED — the old coordinate (and its fraction, which in the ROM
    /// lives in the low byte) is kept, so the spheroid slides along the wall
    /// instead of reflecting off it.
    /// </summary>
    private void AdvancePosition(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        _position = new IntVector2(
            AdvanceAxis(_position.X, _velocityXFp, ref _remainderXFp, bounds.X, bounds.Right - CollisionSize.Width),
            AdvanceAxis(_position.Y, _velocityYFp, ref _remainderYFp, bounds.Y, bounds.Bottom - CollisionSize.Height));
    }

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

        // ROM animation: `OPICT += 4` per body, wrapping at CIRP3 while spinning
        // (pictures 0..3) and at CIRP7 while dropping (0..7) — one picture per
        // 3-frame body, NOT a fixed tick clock (notes §56).
        sprites.DrawSprite(spriteBatch, sprites.SpheroidFrames[_rotation % sprites.SpheroidFrames.Length], Bounds, Color.White);
    }

    public Texture2D CurrentFrameArt(SpriteSet sprites)
        => sprites.SpheroidFrames[_rotation % sprites.SpheroidFrames.Length];
}
