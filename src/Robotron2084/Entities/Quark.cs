using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A random-speed DRIFT (RRTK4 `SQUARE`/`SQVEL`/`SQ3`, notes §51) — the quark
/// NEVER seeks the player (the spec's "travels toward the player" is an
/// oversimplification, and the §28 waypoint model that came from the R5 disasm
/// was wrong: `SQVEL` has no distance term at all). Every body (NAP 3) it rolls
/// a fresh SPEED per axis — `RND(1..SQSPD)` scaled ×4 on X and ×8 on Y in
/// 1/256ths of a column/row per FRAME, the sign flipping AWAY FROM THE WALLS
/// first — and the ROM's mover (RRS22 `OPB80`) integrates it every frame, which
/// is why it glides. It flies OVER electrodes and reflects off nothing (the
/// sign is re-rolled, not mirrored). At spawn it rolls how many Tanks it will
/// drop — RND(0..ENFNUM) halved (rounded up); first drop after RND(TDPTIM), then
/// RND(TDPTIM/2+1) re-arms; when its allotment is gone it flees off the nearest
/// wall edge (SQ3: `OXV = 0`, `OYV = ±$0200` per frame = 2 px/frame) and
/// DISAPPEARS (no death animation — a laser kill bursts it, notes §50).
/// Drops are gated on the arcade's 20-tanks-on-screen cap.
/// </summary>
public sealed class Quark : IEntity, IArtSource
{
    /// <summary>Collision box = the ROM picture dimensions (16x15 arcade px), top-left anchored at <see cref="Position"/>.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.QuarkCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.QuarkCollisionSize.Height));
    private readonly Random _random;
    private readonly int _dropDelayRomTicks;
    private readonly int _quarkSpeedRom;
    private IntVector2 _position;

    /// <summary>
    /// Velocity in 1/256 PORT px per ROM FRAME (ROM OXV/OYV, integrated by the
    /// generic mover once per frame — notes §43 fact 2, §93). A fraction of a
    /// pixel, which is why the remainder below exists.
    /// </summary>
    private IntVector2 _velocityFp;

    /// <summary>Sub-pixel carry, so a 0.03 px/frame drift still accumulates into movement.</summary>
    private IntVector2 _remainderFp;

    private int _bodyFifths;             // ROM NAP 3 + the body vblank, in exact 6/5 ticks
    private int _moverSixths;            // OPB80 cadence: one velocity integration per 6 sixths
    private int _reaimBodiesRemaining;   // ROM PD7: counts down in BODIES
    private int _animationFrame;         // ROM OPICT index, SQP0..SQP8
    private int _tanksRemaining;
    private int _dropBodiesRemaining;    // ROM PD2, also in BODIES
    private bool _droppingTanks;
    private bool _fleeing;

    /// <param name="maxDropsX2">ROM ENFNUM for this wave; the roll happens here (notes §11.2).</param>
    /// <param name="dropDelayRomTicks">ROM TDPTIM for this wave.</param>
    /// <param name="quarkSpeedRom">ROM SQSPD for this wave (the velocity roll's upper bound).</param>
    public Quark(IntVector2 position, Random random, int maxDropsX2 = 10, int dropDelayRomTicks = 12, int quarkSpeedRom = 50)
    {
        _position = position;
        _random = random;
        _dropDelayRomTicks = dropDelayRomTicks;
        _quarkSpeedRom = quarkSpeedRom;
        // ROM (4C24): PD3 = RND(ENFNUM) halved with carry (ceil), same as the spheroid.
        int roll = random.Next(maxDropsX2 + 1);
        _tanksRemaining = (roll + 1) / 2;
        // ROM (4C35-4C3E): PD2 = RND(1..TDPTIM) — and SQUARE only decrements that countdown
        // on the pass that WRAPS the idle animation (five pictures, so six bodies), so it
        // counts CYCLES, not bodies (notes §87).
        _dropBodiesRemaining = 1 + random.Next(dropDelayRomTicks);
        // ROM SQST1 ends with `JSR SQVEL`: a velocity exists from creation, and
        // the body (SQ1) runs on the very first pass — the mover moves it from
        // the first frame, the body does not NAP first. Seeding the accumulator
        // at one full body makes the first Update run a body immediately.
        _bodyFifths = BodyFifths;
        // The mover moves it from the first frame too, so the mover accumulator
        // starts at one full frame (notes §93).
        _moverSixths = 6;
    }

    /// <summary>
    /// One ROM body expressed in 6ths of a port tick (a ROM frame is 6/5 of a
    /// tick, so a body is <c>QuarkBodyRomTicks * 6</c>).
    /// </summary>
    private static int BodyFifths => GameplayConstants.QuarkBodyRomTicks * 6;

    public IntVector2 Position => _position;

    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>
    /// Laser kill (ROM RRTK4 `SQKIL`): `JSR KILOFP` then `MAKP CIRKV` with
    /// colours `$DDDD` over 8 steps — a bespoke shrink/burst, not a blink
    /// (notes §50). Until that animation is built the object bursts via the
    /// field's explosion.
    /// </summary>
    public void Kill() => LifeState = EntityLifeState.Dead;

    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        // The ROM's mover (RRS22 OPB80) advances EVERY object once per FRAME,
        // independent of the object's own NAP. A frame is 6/5 of a tick, so the
        // integration runs every 6 sixths — not every tick, which ran the quark
        // 60/50 = 20% fast (notes §93). The quark's velocity is a fraction of a
        // pixel, so the sub-pixel carry does the work inside.
        _moverSixths += 5;
        if (_moverSixths >= 6)
        {
            _moverSixths -= 6;
            AdvancePosition(field);
        }

        // A body is QuarkBodyRomTicks ROM FRAMES, and a ROM frame is 6/5 of a
        // port tick — so it is 4.8 ticks, not the 4 that `PortTicks(4)`'s
        // integer division yields. Accumulating in 6ths (5 per tick) keeps the
        // aim/animation cadence exact; truncating made EVERY body 17% short,
        // so the quark re-rolled its direction and cycled its animation a fifth
        // more often than the arcade does.
        _bodyFifths += 5;
        if (_bodyFifths < BodyFifths)
        {
            return;
        }

        _bodyFifths -= BodyFifths;
        AdvanceAnimation();

        if (_fleeing)
        {
            // ROM SQ3L ends the quark once it has left the field vertically.
            int low = field.Wall.PlayfieldBounds.Y + ScreenSize.Scaled(GameplayConstants.QuarkFleeExitLowArcadePixels);
            int high = field.Wall.PlayfieldBounds.Bottom - ScreenSize.Scaled(GameplayConstants.QuarkFleeExitHighArcadePixels);
            if (_position.Y <= low || _position.Y >= high)
            {
                LifeState = EntityLifeState.Dead;
            }

            return;
        }

        if (--_reaimBodiesRemaining <= 0)
        {
            RollVelocity(field.Wall.PlayfieldBounds);
        }

        // `TST STATUS / BNE SQ1`: a frozen game (the player-start grace) still animates and
        // still re-rolls its velocity, but it never advances the drop countdown.
        if (field.RobotsFrozen)
        {
            return;
        }

        // SQUARE decrements PD2 only on the pass that WRAPS the idle animation — its
        // `ADDD #4 / CMPD #SQP4 / BLS SQ1` sends every other pass down SQ1, and the
        // `DEC PD2,U` sits on the wrap branch — so the first tank is due after 1..TDPTIM
        // CYCLES of five pictures (six bodies each), not after that many bodies. Counting
        // bodies put the first tank six times too early: the author's "the Quarks are
        // dropping tanks WAY too quickly" (notes §87). Once it IS dropping (SQ2L) the same
        // countdown runs on every body.
        if (!_droppingTanks && _animationFrame != 0)
        {
            return;
        }

        // ROM SQ2: the drop phase. Entered when the drop timer expires, and it
        // never returns to the plain wander — the quark stays in it until its
        // tank allotment is gone, then flees.
        if (--_dropBodiesRemaining > 0)
        {
            return;
        }

        if (_tanksRemaining > 0 && field.CanDropTank)
        {
            _droppingTanks = true;
            _tanksRemaining--;
            // ROM $4CD4-4CDE (TNKDRP): the tank's address = the quark's + $0206,
            // i.e. +2 COLUMNS and +6 ROWS — and the row is decremented first
            // unless the quark sits exactly on the top wall. A column is 2 px, so
            // the X offset is 4 arcade px (notes §53; the port had 2).
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

        // ROM SQ2's re-arm: PD2 = RND(1..(TDPTIM >> 1) + 1) BODIES. (Also the
        // path taken when the 20-tank cap blocks the drop: the quark keeps
        // wandering and tries again.)
        _dropBodiesRemaining = 1 + _random.Next((_dropDelayRomTicks >> 1) + 1);
    }

    /// <summary>
    /// ROM SQVEL: a fresh random SPEED per axis — <c>RND(1..SQSPD) × 4</c> on X
    /// and <c>× 8</c> on Y, so Y is twice as fast per unit — with the sign
    /// flipped AWAY FROM THE WALLS first, and only then taken from the seed bit
    /// (X: set = negative, Y: set = POSITIVE — the opposite polarity
    /// decorrelates the axes). PD7 = <c>(SEED &amp; $1F) + 1</c> BODIES to the
    /// next re-roll.
    /// </summary>
    private void RollVelocity(Rectangle bounds)
    {
        int lowX = bounds.X + ScreenSize.Scaled(GameplayConstants.QuarkWallMarginLowArcadePixels);
        int highX = bounds.Right - ScreenSize.Scaled(GameplayConstants.QuarkWallMarginRightArcadePixels);
        int lowY = bounds.Y + ScreenSize.Scaled(GameplayConstants.QuarkWallMarginLowArcadePixels);
        int highY = bounds.Bottom - ScreenSize.Scaled(GameplayConstants.QuarkWallMarginBottomArcadePixels);

        bool xPositive = _position.X <= lowX || (_position.X < highX && _random.Next(2) == 0);
        bool yPositive = _position.Y <= lowY || (_position.Y < highY && _random.Next(2) != 0);

        _velocityFp = new IntVector2(
            AxisVelocityFp(GameplayConstants.QuarkVelocityXScale, xPositive, coordinateUnitArcadePixels: 2),
            AxisVelocityFp(GameplayConstants.QuarkVelocityYScale, yPositive, coordinateUnitArcadePixels: 1));

        _reaimBodiesRemaining = 1 + _random.Next(GameplayConstants.QuarkReaimMaxBodies);
    }

    /// <summary>
    /// One axis's magnitude: <c>RND(1..SQSPD) × scale</c> in 1/256-px-per-FRAME
    /// units, in PORT px (the arcade's own per-frame value — the mover integrates
    /// it once per ROM frame, notes §93; there is no 60Hz rescaling).
    ///
    /// The <paramref name="coordinateUnitArcadePixels"/> argument is the size of
    /// a world-coordinate unit on that axis, and the two axes differ: the video
    /// buffer is addressed <c>column*256 + row</c>, so <b>X counts two-pixel
    /// columns</b> (the picture descriptors are in bytes for the same reason,
    /// which is why a 7-wide brain picture is 14 px) while <b>Y counts rows</b>.
    /// Dividing one column into two pixels is exactly why the ROM scales the Y
    /// velocity by 8 where X gets 4 — they come out the same speed on screen.
    /// </summary>
    private int AxisVelocityFp(int scale, bool positive, int coordinateUnitArcadePixels)
    {
        int roll = 1 + _random.Next(_quarkSpeedRom);
        int fp = roll * scale * ScreenSize.Scaled(coordinateUnitArcadePixels);
        return positive ? fp : -fp;
    }

    /// <summary>ROM SQ3 (the exit): X stops dead, Y becomes ±$0200 per frame, one coin flip up or down.</summary>
    private void StartFlee()
    {
        _fleeing = true;
        int fp = GameplayConstants.QuarkFleeVelocityRom * ScreenSize.Scaled(1);
        _velocityFp = new IntVector2(0, _random.Next(2) == 0 ? fp : -fp);
        _remainderFp = IntVector2.Zero;
    }

    /// <summary>
    /// ROM: the position advances by the whole-pixel part of the velocity each
    /// FRAME, carrying the rest — the port's stand-in for the ROM's 16-bit world
    /// coordinates. An axis whose update would leave the playfield is REJECTED
    /// (the object keeps that coordinate, and the carry with it).
    /// </summary>
    private void AdvancePosition(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;

        _remainderFp += _velocityFp;
        int stepX = _remainderFp.X / GameplayConstants.QuarkSubpixelsPerPixel;
        int stepY = _remainderFp.Y / GameplayConstants.QuarkSubpixelsPerPixel;
        _remainderFp -= new IntVector2(
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

    private bool IsInsideX(Rectangle bounds, int x) =>
        x >= bounds.X && x + CollisionSize.Width <= bounds.Right;

    private bool IsInsideY(Rectangle bounds, int y) =>
        y >= bounds.Y && y + CollisionSize.Height <= bounds.Bottom;

    /// <summary>
    /// ROM: the animation advances ONE picture per body, and the range depends on
    /// the phase — SQP0..SQP4 while wandering, SQP0..SQP8 once it is dropping
    /// tanks, and SQP8..SQP0 walking BACKWARDS during the exit.
    /// </summary>
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

    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        // NO flash: the quark is always drawn (author, 2026-09-16: "Quarks and
        // tanks should not flash"). The counter above is now purely the frame
        // clock for the rotation animation below.

        // No death animation: the ROM turns the object off and bursts it
        // immediately (notes §50) — see Kill().

        // ROM animation: the pointer advances ONE picture per body, over a range
        // that depends on the phase (see AdvanceAnimation).
        sprites.DrawSprite(spriteBatch, sprites.QuarkFrames[_animationFrame], Bounds, Color.White);
    }

    public Texture2D CurrentFrameArt(SpriteSet sprites) => sprites.QuarkFrames[_animationFrame];
}
