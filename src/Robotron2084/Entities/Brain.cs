using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A brain — the robot that hunts the human family and reprograms its victims
/// into progs while firing cruise missiles at the player. It runs on a steady
/// body clock (this wave's speed plus one frame of body execution); each body it
/// moves, advances one walk frame and decrements its fire timer.
///
/// The chase is not a plain "step toward the target on both axes":
/// <list type="bullet">
/// <item>X has a <b>±2 arcade px dead zone</b> — a brain that is roughly aligned
/// stops correcting horizontally.</item>
/// <item>Y has <b>no dead zone</b> and an exact row match counts as "below", so
/// a brain sharing the target's row oscillates ±1 px. That is the brain's
/// signature hover/jitter.</item>
/// <item>The step is checked against the picture box PER AXIS, so a brain at a
/// wall can still slide along it instead of deadlocking.</item>
/// <item>The animation facing comes from the step DELTAS (X wins), and a facing
/// change RESETS the walk cycle to its first frame.</item>
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
/// The BRAIN (ROM RRB10 BRNORG $1AC0; decoded in arcade-fidelity-notes (18),
/// cadence corrected in (26), movement re-derived from the Gospel in (46)).
/// Steady-state body = the SLEEP(BRNSPD) count plus ONE vblank of body
/// execution (the NAP 12 entry pause happens only on the first body, and NAP 4
/// is the frozen-status branch — both were previously (mis)counted as the
/// cadence, making the port brain ~3.6x too slow). Each body moves, advances
/// one ABAC animation frame and decrements the fire timer.
///
/// The chase is not a plain "step toward the target on both axes":
/// <list type="bullet">
/// <item>X has a <b>±2 arcade px dead zone</b> (BRNL1) — a brain that is
/// roughly aligned stops correcting horizontally.</item>
/// <item>Y has <b>no dead zone</b> and an exact row match counts as "below"
/// (BRN3A), so a brain sharing the target's row oscillates ±1 px. That is the
/// arcade brain's hover/jitter.</item>
/// <item>The step is checked against the picture box — the Gospel does that on
/// the combined move and undoes BOTH axes, which <b>deadlocks</b> a seeking
/// brain at a wall (see the call site); the port rejects PER AXIS instead.</item>
/// <item>The animation facing comes from the step DELTAS (X wins), and a
/// facing change RESETS the 4-entry ABAC index to 0 (BRNDIR/BRNSD).</item>
/// </list>
///
/// Target = the NEAREST living human measured from the BRAIN with the ROM's
/// Manhattan metric (GETHTG), falling back to the player when the family is
/// gone. Touching a human "programs" it into a PROG — the swap is the field's
/// job (ResolveHumanCollisions). Cruise missiles are its weapon: when the fire
/// timer expires (RND(1..BSHTIM) bodies) and the 8-missile cap is free it
/// fires at brain + (3,4). BRAIN = 500 pts.
///
/// Frozen while <see cref="PlayField.RobotsFrozen"/>; killable by lasers
/// (the ROM's `BRNKIL` is `HVEXST` + `KILROB` + `KILL` — explode, off, no blink;
/// notes §50).
/// </remarks>
public sealed class Brain : IEntity, IExplodable
{
    /// <summary>How many extra ROM frames the brain's body adds to this wave's speed to make
    /// one body period.</summary>
    /// <remarks>Period = BRNSPD (SLEEP) + 1 (notes 26). Body execution costs ~1 vblank (the body
    /// runs on its own frame); the period is this wave's speed plus this.</remarks>
    private const int BodyExecutionRomTicks = 1;

    /// <summary>How far the brain steps on one axis per body, in port pixels: one arcade pixel.</summary>
    /// <remarks>The ROM's step is one arcade pixel per axis per body pass.</remarks>
    private static readonly int StepPixels = ScreenSize.Scaled(1);

    /// <summary>
    /// The X approach dead zone: the brain skips its X step while |dx| &lt;= 2 arcade
    /// px, so it stops aligning horizontally once it is close and hovers. There is
    /// NO dead zone on Y: an exact row match counts as "target is below", so on a
    /// shared row the brain drifts down one px, then up again — that vertical jitter
    /// is the brain's signature look.
    /// </summary>
    /// <remarks>
    /// ROM BRNL1: `LDA OBJX,Y / SUBA OBJX,X / ADDA #2 / CMPA #4 / BLS BRN3A`. BRN3A
    /// always picks ±1 and takes the `BHS` branch on an exact match.
    /// </remarks>
    private static readonly int ApproachDeadZonePixels = ScreenSize.Scaled(2);

    /// <summary>The collision box: the brain picture's own size, 14x16 arcade px, in port pixels.</summary>
    /// <remarks>The ROM picture (7 bytes x 16 rows = 14x16 arcade px).</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.BrainCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.BrainCollisionSize.Height));

    /// <summary>The walk cycle: each direction runs frame 1, 2, 1, 3 of its three pictures.</summary>
    /// <remarks>The cycle is ABAC. ROM BRNAL/BRNAR/BRNAD/BRNAU each run P1,P2,P1,P3.</remarks>
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    /// <summary>
    /// The four walk directions' picture bases, in <see cref="SpriteSet.BrainFrames"/> order:
    /// left, right, down, up. Three frames each, so a base's frame 0 is that direction's first
    /// picture.
    /// </summary>
    /// <remarks>ROM BRNAL (left), BRNAR (right), BRNAD (down), BRNAU (up); a base's frame 0 is
    /// that direction's P1 — BRLP1 / BRRP1 / BRDP1 / BRUP1.</remarks>
    private const int LeftDirectionBase = 0;
    private const int RightDirectionBase = 1;
    private const int DownDirectionBase = 2;
    private const int UpDirectionBase = 3;

    private readonly Random _random;
    private readonly int _bodyFifthsPerStep; // (1 + BRNSPD) ROM frames in exact 6ths (notes §52)
    private readonly int _fireDelayRomTicks;
    private IntVector2 _position;
    private int _bodyFifths;
    private int _directionBase = DownDirectionBase; // ROM PD6 init = BRNAD (down), BRNSTV
    private int _frameStep;         // ROM PD4: index 0..3 into the direction's 4-entry table
    private int _fireBodiesRemaining;
    private Human? _victim;                 // ROM PD2: the human being reprogrammed
    private int _reprogramRedrawsRemaining; // 2 redraws per iteration
    private int _reprogramFifths;           // ReprogramStepRomTicks in exact 6ths (3 frames = 3.6)
    private bool _reprogramLifting;         // next redraw lifts the human's Y (+), then drops it (-)

    /// <summary>Creates a brain at <paramref name="position"/> with its facing, fire timer and hover state set up.</summary>
    /// <param name="position">Top-left of the brain.</param>
    /// <param name="random">The random source: the fire timer and the reprogramming jitter.</param>
    /// <param name="brainSpeedRomTicks">How many ROM frames this wave's brain waits between bodies.</param>
    /// <param name="fireDelayRomTicks">How many ROM frames this wave's brain waits between cruise missiles.</param>
    /// <remarks>These are ROM BRNSPD and BSHTIM for this wave (wave table).</remarks>
    public Brain(IntVector2 position, Random random, int brainSpeedRomTicks, int fireDelayRomTicks)
    {
        _position = position;
        _random = random;
        _fireDelayRomTicks = fireDelayRomTicks;
        _bodyFifthsPerStep = (BodyExecutionRomTicks + brainSpeedRomTicks) * 6;
        _fireBodiesRemaining = 1 + random.Next(fireDelayRomTicks);
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
    /// Laser kill (ROM RRB10 `BRNKIL`): `JSR HVEXST` (explode) then `JSR
    /// KILROB` and `JSR KILL` on the process — the brain is gone immediately,
    /// with no death animation of any kind (notes §50). A brain killed while it
    /// is reprogramming a human releases the victim with a SKULL; the field
    /// handles that.
    /// </remarks>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>
    /// Runs one brain body when its body clock says so: chases the nearest human (or the player),
    /// steps one pixel per axis under the X dead zone, advances the walk, and fires a cruise
    /// missile when the fire timer expires. While reprogramming a human it runs the redraw loop
    /// INSTEAD of all of that. Held still while <see cref="PlayField.RobotsFrozen"/>.
    /// </summary>
    /// <param name="gameTime">Unused — the body clock is counted in ROM frames.</param>
    /// <param name="field">The playfield: the human list, the walls, and the missile cap and spawn hook.</param>
    /// <remarks>The body clock is BRNSPD + 1 ROM frames, and the reprogramming redraw loop is ROM `BMUT`.</remarks>
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

        _bodyFifths += 5;
        if (_bodyFifths < _bodyFifthsPerStep)
        {
            return;
        }

        _bodyFifths -= _bodyFifthsPerStep;

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

        // ---- Y: ALWAYS one arcade px. An exact match counts as "below"
        //      (ROM BRN3A compares with BHS and has no dead zone), which is
        //      what makes a brain on the target's row jitter ±1px ----
        int dy = target.Y >= _position.Y ? StepPixels : -StepPixels;

        // ---- the move is checked against the picture box (ROM CKLIM on the
        //      combined (X+dx, Y+dy)) — but PER AXIS, not all-or-nothing.
        //      The Gospel undoes BOTH axes on failure, which DEADLOCKS a
        //      seeking brain: its Y step is unconditional, so a brain flush
        //      against the bottom wall with its target below can never move at
        //      all, not even sideways. The author reported exactly that
        //      (2026-09-16, "the brains seem to get stuck at the bottom wall"),
        //      and the ROM's own generic object mover (RRS22 OPB80) rejects
        //      PER AXIS for the same reason — so per-axis is the house
        //      convention and the combined check is the special case.
        //      See notes §49. ----
        Rectangle bounds = field.Wall.PlayfieldBounds;
        if (dx != 0 && FitsInsideX(bounds, _position.X + dx))
        {
            _position = _position with { X = _position.X + dx };
        }

        if (FitsInsideY(bounds, _position.Y + dy))
        {
            _position = _position with { Y = _position.Y + dy };
        }

        // ---- animation (ROM BRNDIR/BRNSD): the facing is picked from the
        //      DELTAS with X taking precedence, the direction's 4-entry ABAC
        //      table is indexed by a byte offset advancing by 2 (0,2,4,6),
        //      and a DIRECTION CHANGE RESETS THE INDEX to 0 ----
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

        // Fire timer: one body tick per decrement (ROM PD5 counts down per
        // body); reload RND(1..BSHTIM) bodies. The 8-missile cap gates firing.
        if (--_fireBodiesRemaining <= 0)
        {
            if (field.CanFireCruiseMissile)
            {
                field.SpawnCruiseMissile(_position + new IntVector2(
                    3 * ScreenSize.SpecScale, 4 * ScreenSize.SpecScale));
            }

            _fireBodiesRemaining = 1 + _random.Next(_fireDelayRomTicks);
        }
    }

    /// <summary>
    /// True when the object's PICTURE box would still sit inside the playfield on X.
    /// See the call site for why the test is split per axis.
    /// </summary>
    /// <remarks>ROM CKLIM/CKLIMV, split per axis; the Gospel's is a combined test.</remarks>
    private static bool FitsInsideX(Rectangle bounds, int x) =>
        x >= bounds.X && x + CollisionSize.Width <= bounds.Right;

    /// <summary>True when a Y coordinate keeps the whole picture box inside the playfield.
    /// (The X half above carries the explanation of why the check is split per axis.)</summary>
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
    /// Gives up the current victim, if any — used when the brain is killed
    /// mid-animation, so the human is freed and left as a SKULL instead of a prog
    /// (the conversion never completes).
    /// </summary>
    /// <returns>The victim this brain was reprogramming, or null if there was none.</returns>
    /// <remarks>The ROM's BRNKIL compares the dead process's address against BMUT3 and, from that
    /// point on, frees the human and leaves a SKULL.</remarks>
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
        _reprogramFifths = GameplayConstants.ReprogramStepRomTicks * 6;

        // ROM BMUT's placement: the human goes just LEFT of the brain (brain X
        // minus the human's own picture width minus 1), or, if that would cross
        // XMIN, 8px to its RIGHT; its Y is the brain's Y + 2.
        //
        // The placement PICKS THE BRAIN'S PICTURE and BMUT1 stores it
        // (`STD OPICT,X`): BMUT00 loads BRLP1 (BRNAL's frame 0 — facing LEFT)
        // and BMUT10 loads BRRP1 (BRNAR's frame 0 — facing RIGHT), so the brain
        // faces the human for the whole animation. BMUT10's own out-of-bounds
        // `BHS BMUT00` sends the right-hand case back to the left one, which is
        // why the fallback below re-selects the left facing too.
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

        // The picture is the direction's FIRST frame (BRLP1/BRRP1): no walk
        // step is taken while progging, and the chase recomputes both the base
        // and the step when it resumes.
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
    /// <remarks>ROM BMUTL: the lift/drop amount is `SEED &amp; 7`.</remarks>
    private void AdvanceReprogramming(PlayField field, Human victim)
    {
        // One ROM iteration every 3 frames = 3.6 ticks exactly (notes §52) —
        // `PortTicks(3)` = 3 ran the whole animation 17% fast.
        _reprogramFifths += 5;
        if (_reprogramFifths < GameplayConstants.ReprogramStepRomTicks * 6)
        {
            return;
        }

        _reprogramFifths -= GameplayConstants.ReprogramStepRomTicks * 6;

        Rectangle bounds = field.Wall.PlayfieldBounds;
        int jitter = _random.Next(GameplayConstants.ReprogramJitterPixels); // ROM: SEED & 7
        int height = victim.Bounds.Height;
        int y = _reprogramLifting
            ? Math.Min(victim.Position.Y + jitter, bounds.Bottom - height)
            : Math.Max(victim.Position.Y - jitter, bounds.Y);
        victim.TeleportTo(new IntVector2(victim.Position.X, y));

        if (_reprogramLifting)
        {
            // ROM BMUTL requests PRGSND at the top of EVERY iteration (once per
            // lift/drop pair); the engine's priority gate is what keeps it from
            // restarting constantly.
            Sound.Play(SoundTables.ProgProgramming);
        }

        if (--_reprogramRedrawsRemaining > 0)
        {
            _reprogramLifting = !_reprogramLifting;
            return;
        }

        // ROM BMUT's tail: HPSND, the human is freed (never a skull), and
        // PROGST makes a PROG at the human's LAST position, keeping its art.
        Sound.Play(SoundTables.HumanProgConversion);
        victim.FinishReprogramming();
        field.SpawnProg(victim.Position, victim.Kind);
        _victim = null;
    }

    /// <summary>The current walk frame, as an index into <see cref="SpriteSet.BrainFrames"/>.</summary>
    /// <remarks>Chosen from the facing direction's 3-frame set, the ROM's four-step ABAC walk cycle.</remarks>
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
            // ROM DRAW_BRAIN_IN_PROGGING_STATE ($1DAF):
            //   LDB #$BB / STB $2D        ; blitter colour 1 = palette slot 11
            //   LDD $000A,X / STD $0004,X ; the destination
            //   LDY $0002,X               ; the brain's CURRENT animation frames
            //   JSR $D093                 ; = BLIT_RECTANGLE_WITH_COLOUR_REMAP ($DA61)
            //   JMP $D018                 ; = "do blit" — the image, normally
            // $DA61 is blitter op $12: a BLOCK fill (it clears bit 3, so the
            // rectangle is SOLID in $BB). The block is the FIRST of TWO blits —
            // the second draws the brain's own picture on top. $47's "the brain
            // is not drawn as a sprite while it reprograms" came from reading
            // only the first blit, so the port drew the block and threw the
            // sprite away: the author's "the brain sometimes turns into a square
            // block" (notes §72).
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
