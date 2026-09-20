using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A quark — the drifting, tank-dropping robot. On screen it looks like a small
/// rotating shape that wanders the field with no apparent purpose: unlike most
/// enemies it NEVER seeks the player. Every so often it rolls a fresh random
/// speed for each axis and glides in that new direction, only changing course
/// when it drifts too close to a wall (it turns away from the wall, not off
/// it), so it flies straight OVER electrodes rather than colliding with them.
///
/// A quark's real job is to give birth to tanks: at spawn it rolls how many
/// it will drop over its lifetime. The first drop comes after a random delay,
/// then a shorter random delay before each later drop (each new tank appears
/// just below and to the right of the quark). Once its tank allotment is used
/// up it flees straight off the nearest playfield edge and DISAPPEARS — there
/// is no death animation for this either; a laser kill just bursts it on the
/// spot. Drops are also gated on the field's 20-tanks-on-screen cap, so a
/// quark can be "ready" to drop and simply wait if the field is already full
/// of tanks. See <see cref="IEntity"/> for the "beat" / "ROM frame" / "..Timer"
/// / "notes §NN" terminology used throughout this class.
/// </summary>
/// <remarks>
/// A random-speed drift, never seeking the player — there is no distance/steering term
/// anywhere in the roll (ROM: RRTK4.ASM, the `SQUARE` process — `SQVEL` rolls the drift
/// each beat, `SQ3` handles the flee-and-vanish exit; notes §51). Every beat it rolls a fresh random
/// speed for each axis, biased so the sign points away from whichever wall it's nearest, and a
/// shared per-frame mover integrates that velocity every ROM frame, which is what makes it glide
/// smoothly rather than jump. It flies straight over electrodes and never bounces off a wall —
/// reaching one just makes the next random roll point back inward instead. At spawn it rolls how
/// many tanks it will drop over its lifetime (half of a random roll, rounded up); the first drop
/// comes after a random delay and each later one after a shorter random re-arm delay. Once the
/// allotment is used up it flees off the nearest playfield edge at a fixed speed and disappears
/// with no death animation (a laser kill instead bursts it on the spot, notes §50). Drops are
/// also gated on the field's 20-tanks-on-screen cap.
/// </remarks>
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
    /// Velocity in 1/256 PORT px per ROM FRAME, integrated once per frame by the
    /// playfield's generic mover. A fraction of a pixel, which is why the
    /// remainder below exists.
    /// </summary>
    /// <remarks>The ROM's own velocity fields (OXV/OYV), notes §43 fact 2, §93.</remarks>
    private IntVector2 _velocitySubpixels;

    /// <summary>Sub-pixel carry, so a 0.03 px/frame drift still accumulates into movement.</summary>
    private IntVector2 _remainderSubpixels;

    private int _beatTimer;             // Counts up toward the quark's next "beat" update (its AI/animation tick), in fixed-point fifths of a port tick — see IEntity.
    // This drives a separate clock from _beatTimer above: the movement step below runs on
    // its own per-frame schedule, independent of the quark's own beat/AI timer, and every
    // object in the game shares that same movement clock. Both just happen to use the same
    // "+5 per tick, roll over past 6" trick because a ROM frame is 6/5 of a port tick either way.
    private int _moveTimer;            // Counts up to the next movement step: one step fires every 6 fifth-ticks.
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
    /// <remarks>This wave's tank-allotment bound, drop delay and drift-speed cap (ROM: ENFNUM,
    /// TDPTIM, SQSPD; notes §11.2).</remarks>
    public Quark(IntVector2 position, Random random, int maxDropsX2 = 10, int dropDelayRomTicks = 12, int quarkSpeedRom = 50)
    {
        _position = position;
        _random = random;
        _dropDelayRomTicks = dropDelayRomTicks;
        _quarkSpeedRom = quarkSpeedRom;
        // Rolls how many tanks this quark will drop over its lifetime: half of a random number
        // up to the wave's cap, rounded up (ROM: PD3, the same halving trick as the spheroid's
        // own allotment roll).
        int roll = random.Next(maxDropsX2 + 1);
        _tanksRemaining = (roll + 1) / 2;
        // Rolls the delay before the first tank is due. This countdown ticks down in whole
        // animation cycles (five pictures = six beats), not individual beats, which is why the
        // wait feels longer than the raw random number suggests (ROM: PD2, notes §87).
        _dropBeatsRemaining = 1 + random.Next(dropDelayRomTicks);
        // A quark already has a drift velocity and is already animating from the instant it's
        // created — there's no warm-up pause — so both clocks below start pre-loaded to fire on
        // the very first Update rather than counting up from zero.
        _beatTimer = BeatPeriod;
        _moveTimer = 6;
    }

    /// <summary>
    /// One ROM beat expressed in 6ths of a port tick (a ROM frame is 6/5 of a
    /// tick, so a beat is <c>QuarkBeatRomTicks * 6</c>).
    /// </summary>
    private static int BeatPeriod => GameplayConstants.QuarkBeatRomTicks * 6;

    /// <summary>Top-left of the quark (the ROM's OBJX/OBJY).</summary>
    public IntVector2 Position => _position;

    /// <summary>The quark picture's own 16x15 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until shot or until it flees off the field; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>
    /// Kills the quark: it goes straight to Dead, so the field's explosion is the
    /// whole visual — the burst is not a blink.
    /// </summary>
    /// <remarks>
    /// A laser kill plays a bespoke shrink-and-burst animation in the original, not a blink
    /// (ROM: RRTK4.ASM `SQKIL`, notes §50). That specific animation isn't built yet, so this
    /// port instead bursts the quark via the field's shared explosion effect.
    /// </remarks>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>
    /// Runs one beat when its clock says so: moves on the generic mover's own frame clock,
    /// advances the rotation, re-rolls the drift when the direction timer expires, and — once the
    /// drop countdown (which counts animation CYCLES, not beats, until the first tank is due)
    /// runs out — drops a tank, or flees once the allotment is gone.
    /// </summary>
    /// <param name="gameTime">Unused — the mover and beat clocks are counted in ROM frames.</param>
    /// <param name="field">The playfield: the walls for the drift, the tank cap and the spawn hooks.</param>
    /// <remarks>The direction timer is ROM PD7.</remarks>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        // Movement runs on its own per-frame clock, separate from the quark's AI timer, and
        // every object in the game shares it. A ROM frame is 6/5 of a port tick, so this has to
        // fire every 6 fifth-ticks rather than every tick — firing it every tick would have made the
        // quark drift 20% too fast. The velocity itself is a fraction of a pixel, so the
        // sub-pixel remainder tracked below absorbs the leftover (ROM: RRS22.ASM `OPB80`, notes §93).
        _moveTimer += 5;
        if (_moveTimer >= 6)
        {
            _moveTimer -= 6;
            AdvancePosition(field);
        }

        // A beat's length in port ticks isn't a whole number (4.8, not 4), so it's tracked with
        // the same fixed-point fifths trick used elsewhere rather than rounded down — rounding
        // down made every beat 17% too short, so the quark re-rolled its direction and cycled
        // its animation noticeably more often than the original.
        _beatTimer += 5;
        if (_beatTimer < BeatPeriod)
        {
            return;
        }

        _beatTimer -= BeatPeriod;
        AdvanceAnimation();

        if (_fleeing)
        {
            // Once fleeing, the quark simply disappears the moment it has moved fully off the
            // top or bottom edge of the playfield (ROM: RRTK4.ASM `SQ3L`).
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

        // While the game is frozen (the brief grace period after the player spawns), the quark
        // still animates and still re-rolls its drift, but its tank-drop countdown is paused.
        if (field.RobotsFrozen)
        {
            return;
        }

        // Before the first drop, the countdown only ticks once per full animation cycle (five
        // pictures), not once per beat. Once the quark has started dropping, the countdown
        // instead ticks on every beat (notes §87).
        if (!_droppingTanks && _animationFrame != 0)
        {
            return;
        }

        // The drop phase, entered once the countdown above expires. The quark never returns to
        // plain wandering once here — it stays in drop mode until its tank allotment runs out,
        // then flees (ROM: RRTK4.ASM `SQ2`).
        if (--_dropBeatsRemaining > 0)
        {
            return;
        }

        if (_tanksRemaining > 0 && field.CanDropTank)
        {
            _droppingTanks = true;
            _tanksRemaining--;
            // A new tank appears a fixed offset below and to the right of the quark — 4 arcade
            // px right, and down, except when the quark sits exactly on the top wall, where the
            // tank instead gets a smaller vertical offset so it doesn't spawn inside the wall
            // (ROM: TNKDRP, notes §53).
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

        // Rolls the delay before the next drop attempt — roughly half the length of the first
        // delay. This also runs when the 20-tank screen cap blocked the drop, so the quark just
        // keeps wandering and tries again after a short wait (ROM: `SQ2` re-arm).
        _dropBeatsRemaining = 1 + _random.Next((_dropDelayRomTicks >> 1) + 1);
    }

    /// <summary>
    /// Rolls a fresh random speed per axis — four times the roll on X and eight
    /// times on Y, so Y is twice as fast per unit — with the sign flipped AWAY
    /// FROM THE WALLS first, and only then taken from a coin flip (X: heads =
    /// negative, Y: heads = POSITIVE — the opposite polarity decorrelates the
    /// axes). The direction timer is then set to a random 1..32 beats.
    /// </summary>
    /// <remarks>
    /// Rolls a new random speed (1 up to this wave's cap) for each axis, scaled differently per
    /// axis (see <see cref="AxisVelocitySubpixels"/>), and picks a fresh re-aim delay of 1-32 beats
    /// before the next re-roll (ROM: `SQVEL`).
    /// </remarks>
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

    /// <summary>
    /// One axis's magnitude: the roll (1..this wave's speed) times the axis scale,
    /// in 1/256-px-per-FRAME units, in PORT px (the arcade's own per-frame value,
    /// with no 60Hz rescaling).
    ///
    /// The <paramref name="coordinateUnitArcadePixels"/> argument is the size of
    /// a world-coordinate unit on that axis, and the two axes differ: the arcade's
    /// video memory is addressed as <c>column*256 + row</c>, an artifact of the
    /// original hardware, so <b>X counts two-pixel columns</b> (the picture
    /// descriptors are in bytes for the same reason, which is why a 7-wide brain
    /// picture is 14 px) while <b>Y counts single-pixel rows</b>. Dividing one
    /// column into two pixels is exactly why the ROM scales the Y velocity by 8
    /// where X gets 4 — the two numbers look different but produce the same
    /// speed on screen once each is converted to real pixels.
    /// </summary>
    /// <remarks>The playfield's mover integrates this velocity once per ROM frame (notes §93).</remarks>
    private int AxisVelocitySubpixels(int scale, bool positive, int coordinateUnitArcadePixels)
    {
        int roll = 1 + _random.Next(_quarkSpeedRom);
        int subpixels = roll * scale * ScreenSize.Scaled(coordinateUnitArcadePixels);
        return positive ? subpixels : -subpixels;
    }

    /// <summary>Starts the exit run: X stops dead, Y becomes a fixed 2 arcade px per frame, from a coin flip up or down.</summary>
    /// <remarks>The flee exit: horizontal drift stops completely, vertical speed is set to a
    /// fixed value, and a coin flip decides whether it heads up or down (ROM: RRTK4.ASM `SQ3`).</remarks>
    private void StartFlee()
    {
        _fleeing = true;
        int subpixels = GameplayConstants.QuarkFleeVelocityRom * ScreenSize.Scaled(1);
        _velocitySubpixels = new IntVector2(0, _random.Next(2) == 0 ? subpixels : -subpixels);
        _remainderSubpixels = IntVector2.Zero;
    }

    /// <summary>
    /// Moves the quark by the whole-pixel part of its velocity each frame, carrying over the
    /// leftover fractional part so a slow drift still adds up into movement over time. If moving
    /// an axis would push the quark outside the playfield, that axis's move (and its carried
    /// fraction) is simply skipped for this frame.
    /// </summary>
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

    /// <summary>
    /// Advances the animation ONE picture per beat; the range depends on the phase —
    /// five pictures while wandering, all nine once it is dropping tanks, and the same
    /// nine walked BACKWARDS during the exit.
    /// </summary>
    /// <remarks>The ROM's pictures are SQP0..SQP4 while wandering, SQP0..SQP8 while
    /// dropping tanks, and SQP8..SQP0 during the exit.</remarks>
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

    /// <summary>Draws the current rotation frame; a quark never flashes and never plays a death animation.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the quark frames.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        // No flash and no death animation: the quark is always drawn, and a laser kill
        // bursts it immediately instead of playing anything here (notes §50, see Kill()).
        sprites.DrawSprite(spriteBatch, sprites.QuarkFrames[_animationFrame], Bounds, Color.White);
    }

    /// <summary>The current rotation frame, for the death burst (see <see cref="IArtSource"/> and <see cref="ScoreBurst.ForQuark"/>).</summary>
    /// <param name="sprites">The shared sprite set, which holds the quark frames.</param>
    /// <returns>The texture for the current rotation frame.</returns>
    public Texture2D CurrentFrameArt(SpriteSet sprites) => sprites.QuarkFrames[_animationFrame];
}
