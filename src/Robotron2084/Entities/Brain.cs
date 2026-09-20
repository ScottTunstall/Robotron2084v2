using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A brain — a floating robot that hunts the rescuable human family (the
/// civilians the player is trying to save) and, on contact, "reprograms" one
/// into a hostile <see cref="Prog"/> — the human freezes in place for a moment
/// while the brain visibly works on it, then the human is gone and a prog walks
/// away in its place. While it is not busy converting someone, a brain also
/// fires slow homing "cruise missiles" at the player.
///
/// It does not re-think every single tick. Instead it wakes up on a steady
/// beat — roughly every 1/10th of a second, the exact length depending on this
/// wave's difficulty (this codebase calls one such wake-up a "beat"; see
/// <see cref="IEntity"/> if you want the full story on why, and on the
/// "ROM frame"/"port tick"/"fifths" units used everywhere in this file). Each
/// time it wakes up, it does all three of its jobs in one go: takes one step
/// toward its target, advances its walk animation by one frame, and ticks its
/// missile-reload timer down by one.
///
/// The chase is not a plain "step toward the target on both axes":
/// <list type="bullet">
/// <item>X has a <b>±2 arcade px dead zone</b> — a brain that is roughly aligned
/// stops correcting horizontally.</item>
/// <item>Y has <b>no dead zone</b> and an exact row match counts as "below", so
/// a brain sharing the target's row oscillates ±1 px. That is the brain's
/// signature hover/jitter.</item>
/// <item>Each axis is checked separately against the wall, so a brain walking
/// straight into a wall can still slide sideways along it instead of getting stuck.</item>
/// <item>Which way the brain faces is decided by which way it just moved (X wins if
/// both moved), and changing facing always restarts the walk animation from its
/// first frame.</item>
/// </list>
///
/// It targets the NEAREST living human by Manhattan distance, falling back to the
/// player once the family is gone. Touching a human "programs" it into a PROG —
/// the swap is the field's job (ResolveHumanCollisions). Cruise missiles are its
/// weapon: when the fire timer expires and the 8-missile cap is free it fires one
/// at brain + (3,4). A brain is worth 500 points.
///
/// Frozen while <see cref="PlayField.RobotsFrozen"/>; lasers kill it outright,
/// with no blink.
/// </summary>
/// <remarks>
/// Ported from the arcade's own brain behaviour (ROM: RRB10.ASM's `BRNORG`; notes §18, §26,
/// §46). One deliberate deviation: the ROM's own wall check rejects an ENTIRE step — both
/// axes at once — if either axis alone would leave the playfield, which can permanently
/// freeze a brain pinned against a wall by its target. This port checks each axis
/// independently instead, matching the arcade's own general-purpose movement code used
/// elsewhere, so a blocked brain can still slide sideways along the wall (notes §49).
///
/// The swap from human to <see cref="Prog"/> itself is <see cref="PlayField"/>'s job
/// (<c>ResolveHumanCollisions</c>); notes §50 covers the laser-kill/no-death-animation rule.
/// </remarks>
public sealed class Brain : IEntity, IExplodable
{
    /// <summary>Extra ROM frames added to this wave's brain speed to get the real gap between wake-ups.</summary>
    /// <remarks>ROM: brain speed (`BRNSPD`) plus this 1-frame overhead (notes §26).</remarks>
    private const int BeatExecutionRomTicks = 1;

    /// <summary>How far the brain moves on each axis, every time it takes a step: a single arcade pixel.</summary>
    /// <remarks>The ROM's own step size, on either axis, is one arcade pixel per wake-up.</remarks>
    private static readonly int StepPixels = ScreenSize.Scaled(1);

    /// <summary>X approach dead zone: within this many pixels of the target's column, the brain
    /// stops correcting X and just hovers (there's no equivalent dead zone on Y).</summary>
    /// <remarks>ROM: `BRNL1`/`BRN3A`.</remarks>
    private static readonly int ApproachDeadZonePixels = ScreenSize.Scaled(2);

    /// <summary>The collision box: the brain picture's own size, 14x16 arcade px, in port pixels.</summary>
    /// <remarks>The ROM picture (7 bytes x 16 rows = 14x16 arcade px).</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.BrainCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.BrainCollisionSize.Height));

    /// <summary>
    /// The walk animation's frame order, as indices into a direction's 3 pictures: play
    /// picture 1, then 2, then back to 1, then 3, then repeat — not a plain 1-2-3 loop. (This
    /// codebase calls that repeating "first, second, first, third" shape an "A-B-A-C" pattern.)
    /// </summary>
    /// <remarks>ROM: each direction's animation table (`BRNAL`/`BRNAR`/`BRNAD`/`BRNAU`) plays
    /// its 3 pictures in this same order.</remarks>
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    /// <summary>The four walk directions' picture bases, in <see cref="SpriteSet.BrainFrames"/> order
    /// (left, right, down, up); each has 3 pictures, so a base's frame 0 is that direction's first.</summary>
    private const int LeftDirectionBase = 0;
    private const int RightDirectionBase = 1;
    private const int DownDirectionBase = 2;
    private const int UpDirectionBase = 3;

    private readonly Random _random;
    private readonly int _beatPeriod; // how long between wake-ups, in the fixed-point "fifths" clock (see IEntity)
    private readonly int _fireDelayRomTicks;
    private IntVector2 _position;
    private int _beatTimer; // counts up toward the next wake-up
    private int _directionBase = DownDirectionBase; // starts facing down, like a freshly spawned brain
    private int _frameStep;         // index 0..3 into the current direction's 4-frame walk pattern
    private int _fireBeatsRemaining; // wake-ups left before the next missile
    private Human? _victim;                 // the human currently being reprogrammed, if any
    private int _reprogramRedrawsRemaining; // 2 redraws per iteration
    private int _reprogramTimer;           // ReprogramStepRomTicks in exact 6ths (3 frames = 3.6)
    private bool _reprogramLifting;         // next redraw lifts the human's Y (+), then drops it (-)

    /// <summary>Creates a brain at <paramref name="position"/> with its facing, fire timer and hover state set up.</summary>
    /// <param name="position">Top-left of the brain.</param>
    /// <param name="random">The random source: the fire timer and the reprogramming jitter.</param>
    /// <param name="brainSpeedRomTicks">How many ROM frames this wave's brain waits between wake-ups.</param>
    /// <param name="fireDelayRomTicks">How many ROM frames this wave's brain waits between cruise missiles.</param>
    /// <remarks>These two speeds come from this wave's row in the difficulty table (ROM: `BRNSPD` and
    /// `BSHTIM`).</remarks>
    public Brain(IntVector2 position, Random random, int brainSpeedRomTicks, int fireDelayRomTicks)
    {
        _position = position;
        _random = random;
        _fireDelayRomTicks = fireDelayRomTicks;
        _beatPeriod = (BeatExecutionRomTicks + brainSpeedRomTicks) * 6;
        _fireBeatsRemaining = 1 + random.Next(fireDelayRomTicks);
    }

    /// <summary>Top-left of the brain.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>The brain picture's own 14x16 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until shot or until it walks into an electrode; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>
    /// Kills the brain: it goes straight to Dead, with no death animation of any
    /// kind.
    /// </summary>
    /// <remarks>
    /// A laser kill explodes the brain and removes it in the same instant, with no
    /// death animation of any kind (ROM: RRB10.ASM's `BRNKIL`; notes §50). A brain
    /// killed while it is reprogramming a human releases the victim with a SKULL; the
    /// field handles that.
    /// </remarks>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>
    /// Runs one wake-up when it's due: chases the nearest human (or the player), steps one pixel
    /// per axis (unless the X dead zone is holding it steady), advances the walk animation, and
    /// counts the fire timer down, firing a cruise missile if it just ran out. While it's
    /// reprogramming a human, it runs the reprogramming animation instead of any of that. Held
    /// still while <see cref="PlayField.RobotsFrozen"/>.
    /// </summary>
    /// <param name="gameTime">Unused — the wake-up timer is counted in ROM frames, not real time.</param>
    /// <param name="field">The playfield: the human list, the walls, and the missile cap and spawn hook.</param>
    /// <remarks>The wake-up period is this wave's brain speed plus 1 ROM frame, and the
    /// reprogramming animation is driven by the ROM's `BMUT` routine.</remarks>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        if (field.RobotsFrozen)
        {
            return;
        }

        // ROM BMUT: while reprogramming a human the brain does NOT move, fire
        // or animate — it is drawn as a solid block and runs its own 20-
        // iteration redraw loop instead of the chase.
        if (_victim is { } victim)
        {
            AdvanceReprogramming(field, victim);
            return;
        }

        _beatTimer += 5;
        if (_beatTimer < _beatPeriod)
        {
            return;
        }

        _beatTimer -= _beatPeriod;

        // Target: nearest living human to THIS BRAIN (ROM GETHTG, Manhattan
        // metric), else the player — the brain hunts the family, then you.
        IntVector2 target = field.NearestHumanPositionTo(_position) ?? field.Player.Position;

        // ---- X: one arcade px toward the target, SKIPPED inside the dead
        //      zone (ROM BRNL1) ----
        int dx = 0;
        int targetDx = target.X - _position.X;
        if (Math.Abs(targetDx) > ApproachDeadZonePixels)
        {
            dx = Math.Sign(targetDx) * StepPixels;
        }

        // Y always moves one pixel (no dead zone), which is what makes a brain sharing the
        // target's row jitter up and down instead of holding still.
        int dy = target.Y >= _position.Y ? StepPixels : -StepPixels;

        // Each axis is checked against the wall separately (see the class remarks for why).
        Rectangle bounds = field.Wall.PlayfieldBounds;
        if (dx != 0 && FitsInsideX(bounds, _position.X + dx))
        {
            _position = _position with { X = _position.X + dx };
        }

        if (FitsInsideY(bounds, _position.Y + dy))
        {
            _position = _position with { Y = _position.Y + dy };
        }

        // Which way the brain is facing comes from which way it just moved (X wins if both
        // moved). Facing the SAME way as last time just advances to the next frame in the
        // walk pattern; a facing CHANGE always restarts that pattern from its first frame.
        // (ROM: `BRNDIR`/`BRNSD`.)
        int nextBase = dx != 0
            ? (dx > 0 ? RightDirectionBase : LeftDirectionBase)
            : (dy < 0 ? UpDirectionBase : DownDirectionBase);
        if (nextBase == _directionBase)
        {
            _frameStep = (_frameStep + 1) % WalkCycle.Length;
        }
        else
        {
            _directionBase = nextBase;
            _frameStep = 0;
        }

        // The missile timer ticks down by one on every wake-up; when it reaches zero, fire
        // (if the 8-missile cap allows it) and reload it to a fresh random 1..(this wave's
        // fire delay) wake-ups.
        if (--_fireBeatsRemaining <= 0)
        {
            if (field.CanFireCruiseMissile)
            {
                field.SpawnCruiseMissile(_position + new IntVector2(
                    3 * ScreenSize.SpecScale, 4 * ScreenSize.SpecScale));
            }

            _fireBeatsRemaining = 1 + _random.Next(_fireDelayRomTicks);
        }
    }

    /// <summary>True when the brain's picture box would still sit inside the playfield on X.</summary>
    private static bool FitsInsideX(Rectangle bounds, int x) =>
        x >= bounds.X && x + CollisionSize.Width <= bounds.Right;

    /// <summary>True when the brain's picture box would still sit inside the playfield on Y.</summary>
    private static bool FitsInsideY(Rectangle bounds, int y) =>
        y >= bounds.Y && y + CollisionSize.Height <= bounds.Bottom;

    /// <summary>
    /// True while this brain is reprogramming a human. The field must not hand it
    /// another victim, and the victim itself is off the human list until the
    /// animation finishes.
    /// </summary>
    /// <remarks>ROM BMUT.</remarks>
    public bool IsReprogramming => _victim is not null;

    /// <summary>
    /// Gives up the current victim, if any — used when the brain is killed mid-animation, so
    /// the conversion never completes and the human is freed as a skull instead of a prog.
    /// </summary>
    /// <returns>The victim this brain was reprogramming, or null if there was none.</returns>
    /// <remarks>ROM: `BRNKIL`.</remarks>
    internal Human? ReleaseVictim()
    {
        Human? victim = _victim;
        _victim = null;
        return victim;
    }

    /// <summary>
    /// Starts the reprogramming: called by the field when the two objects' TOP-LEFT
    /// CORNERS are within ±3px on both axes (NOT a picture overlap). Sets the human
    /// up beside the brain and starts the redraw loop.
    /// </summary>
    /// <param name="human">The victim, which is taken off the human list until the animation ends.</param>
    /// <param name="playfieldBounds">The playfield bounds, used to place the human beside the brain.</param>
    /// <remarks>ROM BMUT entry; the proximity test is BRNL1's tail.</remarks>
    internal void BeginReprogramming(Human human, Rectangle playfieldBounds)
    {
        _victim = human;
        human.BeginReprogramming();
        _reprogramRedrawsRemaining = GameplayConstants.ReprogramIterations * GameplayConstants.ReprogramRedrawsPerIteration;
        _reprogramLifting = true;
        _reprogramTimer = GameplayConstants.ReprogramStepRomTicks * 6;

        // The human is placed just left of the brain, or 8px to its right if that would
        // cross the left wall (and back to the left if THAT would cross the right wall).
        // Whichever side is picked also becomes the brain's facing, so it faces the human
        // for the whole animation. (ROM: `BMUT`/`BMUT1`.)
        int humanWidth = human.Bounds.Width;
        int x = _position.X - humanWidth - ScreenSize.Scaled(1);
        int facingBase = LeftDirectionBase;
        if (x < playfieldBounds.X)
        {
            x = _position.X + ScreenSize.Scaled(8);
            facingBase = RightDirectionBase;
            if (x >= playfieldBounds.Right - ScreenSize.Scaled(4))
            {
                x = _position.X - humanWidth - ScreenSize.Scaled(1);
                facingBase = LeftDirectionBase;
            }
        }

        // The picture is the direction's FIRST walk frame: no walk step is taken while
        // reprogramming, and the chase recomputes both the facing and the step when it
        // resumes.
        _directionBase = facingBase;
        _frameStep = 0;

        human.TeleportTo(new IntVector2(x, _position.Y + ScreenSize.Scaled(2)));
    }

    /// <summary>
    /// Each iteration redraws the human TWICE — once with its Y lifted by a random
    /// 0..7 and once with it dropped by the same, each clamped into the playfield —
    /// then decrements the iteration counter. The lift/drop are cumulative on the
    /// human's own Y, so it wanders; the clamps at both edges keep it on screen.
    /// </summary>
    /// <remarks>The lift/drop amount is a random 0..7 pixels each time (ROM: `BMUTL`).</remarks>
    private void AdvanceReprogramming(PlayField field, Human victim)
    {
        // One iteration every 3 ROM frames, via the fixed-point "fifths" clock (see IEntity).
        _reprogramTimer += 5;
        if (_reprogramTimer < GameplayConstants.ReprogramStepRomTicks * 6)
        {
            return;
        }

        _reprogramTimer -= GameplayConstants.ReprogramStepRomTicks * 6;

        Rectangle bounds = field.Wall.PlayfieldBounds;
        int jitter = _random.Next(GameplayConstants.ReprogramJitterPixels); // a random 0..7 px
        int height = victim.Bounds.Height;
        int y = _reprogramLifting
            ? Math.Min(victim.Position.Y + jitter, bounds.Bottom - height)
            : Math.Max(victim.Position.Y - jitter, bounds.Y);
        victim.TeleportTo(new IntVector2(victim.Position.X, y));

        if (_reprogramLifting)
        {
            // The arcade requests its reprogramming sound at the top of EVERY iteration
            // (once per lift/drop pair); the engine's priority gate is what keeps it
            // from restarting constantly. (ROM: `BMUTL`.)
            Sound.Play(SoundTables.ProgProgramming);
        }

        if (--_reprogramRedrawsRemaining > 0)
        {
            _reprogramLifting = !_reprogramLifting;
            return;
        }

        // The animation's tail: play the conversion sound, free the human (never a
        // skull — the conversion completed), and spawn a PROG at the human's LAST
        // position, keeping its art. (ROM: `BMUT`'s end, `PROGST`.)
        Sound.Play(SoundTables.HumanProgConversion);
        victim.FinishReprogramming();
        field.SpawnProg(victim.Position, victim.Kind);
        _victim = null;
    }

    /// <summary>The current walk frame, as an index into <see cref="SpriteSet.BrainFrames"/>.</summary>
    /// <remarks>Picks from the facing direction's 3 pictures, in the "first, second, first,
    /// third" order described on <see cref="WalkCycle"/>.</remarks>
    internal int WalkFrameIndex => _directionBase * 3 + WalkCycle[_frameStep];

    /// <summary>Draws the current walk frame — over a solid slot-11 block while it is reprogramming.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the brain frames.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        // No death animation: the ROM bursts it immediately (notes §50).

        if (IsReprogramming)
        {
            // Reprogramming draws two layers: a solid coloured block filling the brain's
            // box, then the brain's own picture on top of it (ROM: `DRAW_BRAIN_IN_PROGGING_STATE`).
            sprites.DrawSolidRectangle(spriteBatch, Bounds, sprites.SlotColor(GameplayConstants.ReprogramShapeSlot));
        }

        sprites.DrawSprite(spriteBatch, sprites.BrainFrames[WalkFrameIndex], Bounds, Color.White);
    }

    /// <summary>The frame an explosion would copy (see <see cref="IArtSource"/>).</summary>
    /// <param name="sprites">The shared sprite set, which holds the brain frames.</param>
    /// <returns>The texture for the current walk frame.</returns>
    public Texture2D CurrentFrameArt(SpriteSet sprites)
        => sprites.BrainFrames[WalkFrameIndex];
}
