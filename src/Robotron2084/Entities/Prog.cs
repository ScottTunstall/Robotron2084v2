using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A PROG — a human converted by a brain's touch (ROM RRB10 PROGST/process;
/// the Gospel decode is in arcade-fidelity-notes (18) and (46)). Walks
/// STRAIGHT cardinal lines: the PRGAL/PRGAR/PRGAD/PRGAU tables give the step
/// per body as (±2 COLUMNS, 0) horizontally and (0, ±4 ROWS) vertically — and a
/// column is TWO arcade pixels, so <b>both axes cover the same 4 px a body</b>
/// (the old "vertical progs move twice as fast as horizontal ones" read the
/// column as a pixel and left the horizontal walk at half speed). Body of NAP 3
/// ROM ticks.
///
/// It does NOT home in on you. Each prog carries a persistent random targeting
/// OFFSET (GPOFF: X in [-28,+28] step 4, Y in [-16,+18] step 2) applied to your
/// position to make its aim point, re-rolled on ~3% of bodies; and only HALF
/// the aims consider X at all — GPDIR picks the axis from HSEED's top bit
/// (50/50), so a prog walks horizontally toward where you were or vertically,
/// never both. ~10% of bodies re-aim, and a blocked step re-aims immediately.
/// An aim point that falls off the far end of an axis wraps to the opposite
/// edge (XMAX+$30 / YMAX+18), which is how progs occasionally turn AWAY.
///
/// It keeps the art (and collision box) of the human it became, but it is NOT
/// drawn as a sprite: like every solid-colour object in the ROM it goes through
/// the blitter's remap ops, as TWO colour pairs per body (notes §47/§49):
/// <list type="bullet">
/// <item>the position it is leaving, `HUMON $EE00` — a slot-14 block with a
/// BLACK silhouette;</item>
/// <item>the position it is entering, `HUMON $00AA` — a BLACK block with the
/// silhouette in slot 10.</item>
/// </list>
/// The shadow ring is SEVEN entries — `PD+8` is the index and the entries run
/// `PD+10`, `PD+12` ... wrapping at `SPSIZE`, which with `PD = 7`
/// (RRF.ASM:551) and `SPSIZE = 31` (RRF.ASM:565) means byte offsets 17, 19 ...
/// 29. Each body erases the entry under the index (`PCTOFF`) and overwrites it
/// with the position being vacated, so SEVEN ghosts trail behind each prog. A
/// ghost is also FROZEN: the ROM blits it once and never re-blits it, so the
/// trail shows the ABAC walk cycle repeating rather than one animated snake.
/// That trail is what makes a prog unmistakable on the arcade screen, and both
/// colour pairs name cycling slots, so a prog shimmers as it walks.
///
/// ⚠ The two pairs are EXACT INVERSES, and that is the ROM's design, not a bug
/// (author, 2026-09-16: "I think the trail of the prog has inverted colours").
/// `HUMON`'s own header names the pair `A=OUTER SHELL, B=INNER`, and A goes to
/// `BLKON` (op `$12`, which FILLS the rect) while B goes to `MPCTON` (op `$1A`,
/// the figure only) — so `$EE00` really is a slot-E card carrying a BLACK
/// figure, and `$00AA` a black card carrying a slot-A figure. Notes §53 has the
/// decode; do not swap them without the author's say-so.
///
/// On a laser hit `PRGKIL` erases the whole trail, swaps the object's picture to
/// the PHONY burst (`PGXPIC` — a 12×16 SOLID card), clamps the position to
/// `(XMAX-5, YMAX-15)`, and then calls the ordinary `EXST` — so a prog dies like
/// every other robot, with the same direction-dispatched strip explosion, only fed
/// the phony card instead of the human it used to be. PROG = 100 pts.
///
/// Contact with the player KILLS the player (it is an enemy robot — the
/// arcade's progs hunt the player down).
/// </summary>
public sealed class Prog : IExplodable
{
    /// <summary>ROM body: NAP 3 game ticks per step.</summary>
    private const int BodyPeriodRomTicks = 3;

    /// <summary>The body period in exact 6ths (notes §52, §65): 3 ROM frames = 3.6 ticks.</summary>
    private static int BodyFifths => BodyPeriodRomTicks * 6;

    /// <summary>
    /// ROM PRGAL/PRGAR table step: **±2 COLUMNS** on X. The prog's `OX16` is a screen
    /// coordinate, i.e. `column*256 + row`, so the table's X byte moves a whole COLUMN at a
    /// time — two arcade pixels (§55) — and the horizontal prog walks 4 px a body, the same
    /// 4 px the vertical tables' ±4 ROWS give. The port used 2 px here (half speed): the
    /// author's "the progs horizontal movement seems a little slow" (notes §87).
    /// </summary>
    private const int StepXColumns = 2;

    /// <summary>ROM PRGAD/PRGAU table step: ±4 ROWS on Y.</summary>
    private const int StepYRows = 4;

    /// <summary>Arcade pixels in one ROM column (the video buffer is `column*256 + row`).</summary>
    private const int ArcadePixelsPerColumn = 2;

    /// <summary>ROM GPOFF: the X offset is (RND(1..15) - 8) * 4, i.e. ±28 COLUMNS in steps of 4.</summary>
    private const int OffsetXHalfRange = 8;

    /// <summary>ROM GPOFF: the Y offset is ((19 - RND(1..18)) * 2) - 18, i.e. -16..+18 ROWS in steps of 2.</summary>
    private const int OffsetYSteps = 18;

    /// <summary>ROM GPDIR wrap limits: an aim point past XMAX+$30 COLUMNS / YMAX+18 ROWS wraps to the opposite edge.</summary>
    private const int WrapMarginXColumns = 0x30;
    private const int WrapMarginYRows = 18;

    /// <summary>ROM PROG: random-seed thresholds out of 256 — SEED >= $F8 re-rolls the offsets,
    /// LSEED > $E4 re-aims (8/256 and 27/256).</summary>
    private const int ReOffsetThreshold256 = 0xF8;
    private const int ReDirectionThreshold256 = 0xE4;

    /// <summary>Collision box = the converted human's picture (per kind).</summary>
    private static (int Width, int Height) ArcadeCollisionSize(HumanKind kind) => kind switch
    {
        HumanKind.Mikey => GameplayConstants.MikeyCollisionSize,
        HumanKind.Mom => GameplayConstants.MomCollisionSize,
        _ => GameplayConstants.DadCollisionSize,
    };

    private static readonly int[] WalkCycle = { 0, 1, 0, 2 }; // ABAC over the human's 3 frames

    private readonly Random _random;
    private readonly HumanKind _kind;
    private readonly (int Width, int Height) _collisionSize;
    private IntVector2 _position;
    private Direction8 _direction = Direction8.Down; // cardinal only (the ROM has 4 tables); set by the first body's GPDIR
    private int _bodyFifths;
    private int _frameStep; // 0..3 into the ABAC table (ROM ODATA)
    private int _offsetX;   // ROM PD4: persistent aim-offset on X, re-rolled by GPOFF
    private int _offsetY;   // ROM PD5: persistent aim-offset on Y

    /// <summary>
    /// One shadow-ring entry: a position this prog vacated, plus the pose it was
    /// drawn in at that moment. The ROM blits a ghost once (`HUMON` at `OPICT`)
    /// and never touches those pixels again until `PCTOFF` erases them, so a
    /// ghost keeps its creation pose for life.
    /// </summary>
    private readonly record struct Ghost(IntVector2 Position, int FrameIndex);

    /// <summary>The shadow ring, NEWEST FIRST (see <see cref="Ghost"/>).</summary>
    private readonly List<Ghost> _ghosts = new();

    /// <summary>Test hook: the ghost trail's positions, newest first.</summary>
    internal IReadOnlyList<IntVector2> GhostTrail
    {
        get
        {
            var positions = new List<IntVector2>(_ghosts.Count);
            foreach (Ghost ghost in _ghosts)
            {
                positions.Add(ghost.Position);
            }

            return positions;
        }
    }

    /// <summary>Test hook: the pose each ghost was frozen in, newest first.</summary>
    internal IReadOnlyList<int> GhostFrames
    {
        get
        {
            var frames = new List<int>(_ghosts.Count);
            foreach (Ghost ghost in _ghosts)
            {
                frames.Add(ghost.FrameIndex);
            }

            return frames;
        }
    }

    public Prog(IntVector2 position, HumanKind kind, Random random)
    {
        _position = position;
        _kind = kind;
        _random = random;
        _collisionSize = (
            ScreenSize.Scaled(ArcadeCollisionSize(kind).Width),
            ScreenSize.Scaled(ArcadeCollisionSize(kind).Height));
        RollOffsets(); // ROM PROGST calls GPOFF at creation
    }

    /// <summary>Which human's art/box this prog carries (it became that human).</summary>
    public HumanKind Kind => _kind;

    public IntVector2 Position => _position;

    public Rectangle Bounds => new(_position.X, _position.Y, _collisionSize.Width, _collisionSize.Height);

    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>
    /// ROM PRGKIL: `JSR KILL` then `STD OPICT,X` = `PGXPIC`. The object is GONE the
    /// instant it is hit (`KILROB`), and the only thing left is the strip explosion
    /// of the picture it swapped in — the field's, because
    /// <see cref="CurrentFrameArt"/> is now the phony card. There is no Dying state:
    /// the port used to draw the whole card as a 20-tick static pop here instead of
    /// running the ROM's explosion (notes §90).
    /// </summary>
    public void Kill()
    {
        if (LifeState == EntityLifeState.Alive)
        {
            LifeState = EntityLifeState.Dead;
        }
    }

    /// <summary>
    /// ROM PRGKIL: the picture the death explosion shatters is `PGXPIC`, the phony
    /// burst card — NOT the human art the prog was walking in.
    /// </summary>
    public Texture2D CurrentFrameArt(SpriteSet sprites) => sprites.ProgBurst;

    /// <summary>
    /// ROM PRGKIL swaps the picture to the 12×16 `PGXPIC` descriptor and leaves
    /// `OBJX/OBJY` alone, and `EXSTV` takes its record's rect as `UL = OBJX/OBJY` with
    /// the PICTURE's W/H (`LDD ,X`) — so the explosion is the CARD's rect at the
    /// prog's corner, not the (smaller) human box it was standing in. The ROM also
    /// clamps that corner to `(XMAX-5, YMAX-15)`; a prog is always inside the play
    /// area, so the port's corner already is.
    /// </summary>
    public Rectangle ExplosionBounds => new(
        _position.X,
        _position.Y,
        ScreenSize.Scaled(GameplayConstants.ProgBurstSize.Width),
        ScreenSize.Scaled(GameplayConstants.ProgBurstSize.Height));

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

        // ROM body: NAP 3 = 3 ROM frames = 3.6 port ticks (exact 6ths, notes
        // §52), not the truncated PortTicks(3) = 3 that made the prog 20% fast.
        _bodyFifths += 5;
        if (_bodyFifths < BodyFifths)
        {
            return;
        }

        // Animation first (ROM PROG1: ODATA += 3 through the 4-entry table,
        // wrapping past 9) — the frame advances even if the step is refused.
        _bodyFifths -= BodyFifths;
        _frameStep = (_frameStep + 1) % WalkCycle.Length;

        // Re-roll the aim offsets on ~3% of bodies (ROM: SEED > $F8), then
        // possibly re-aim on ~10% (ROM: LSEED > $E4) — in that order, because
        // GPDIR aims with whatever offsets are current.
        if (_random.Next(256) > ReOffsetThreshold256)
        {
            RollOffsets();
        }

        if (_random.Next(256) > ReDirectionThreshold256)
        {
            _direction = PickDirection(field);
            _frameStep = 0; // ROM GPDIR sets ODATA = $FD, which wraps to frame 0
        }

        // ROM PROG3 draws the ghost at the position it is LEAVING and stores
        // that position in the shadow ring — whether or not this body's step
        // succeeded (a refused step would just put the ghost on top of the
        // prog). The ring is 7 entries, so 7 ghosts remain visible, and the
        // pose goes into the entry with it: the ROM never re-blits an old ghost.
        _ghosts.Insert(0, new Ghost(_position, WalkFrameIndex));
        if (_ghosts.Count > GameplayConstants.ProgGhostCount)
        {
            _ghosts.RemoveAt(_ghosts.Count - 1); // PCTOFF erases this one this body
        }

        // Step: the table's per-axis magnitudes — 2 COLUMNS on X (4 px) and 4 ROWS on Y, so
        // both axes cover the same 4 px a body. The direction is cardinal, so exactly one
        // axis moves.
        int stepX = ScreenSize.Scaled(StepXColumns * ArcadePixelsPerColumn);
        int stepY = ScreenSize.Scaled(StepYRows);
        IntVector2 step = _direction switch
        {
            Direction8.Left => new IntVector2(-stepX, 0),
            Direction8.Right => new IntVector2(stepX, 0),
            Direction8.Up => new IntVector2(0, -stepY),
            _ => new IntVector2(0, stepY),
        };

        IntVector2 candidate = _position + step;
        Rectangle next = new(candidate.X, candidate.Y, _collisionSize.Width, _collisionSize.Height);
        if (FitsInside(field.Wall.PlayfieldBounds, next))
        {
            _position = candidate;
        }
        else
        {
            // ROM CKLIM failure -> PROGND -> GPDIR: re-aim and DROP the step.
            _direction = PickDirection(field);
            _frameStep = 0;
        }
    }

    /// <summary>
    /// ROM CKLIMV: the object's picture box must be inside the playfield; a
    /// failure REJECTS the whole move (it is not clamped, and the prog never
    /// slides along a wall).
    /// </summary>
    private static bool FitsInside(Rectangle bounds, Rectangle box) =>
        box.X >= bounds.X
        && box.Y >= bounds.Y
        && box.Right <= bounds.Right
        && box.Bottom <= bounds.Bottom;

    /// <summary>
    /// ROM GPOFF: the persistent aim offsets, in the ROM's OWN units — X in COLUMNS
    /// (`(RND(1..15) - 8) * 4`, so -28..+28 columns = -56..+56 arcade px) and Y in ROWS
    /// (`((19 - RND(1..18)) * 2) - 18`, so -16..+18 rows). These are what stop a prog from
    /// steering straight at you: it walks toward where you were plus its own standing error.
    /// </summary>
    private void RollOffsets()
    {
        _offsetX = (_random.Next(1, 16) - OffsetXHalfRange) * 4;
        _offsetY = ((19 - _random.Next(1, OffsetYSteps + 1)) * 2) - OffsetYSteps;
    }

    /// <summary>
    /// ROM GPDIR: HALF of all re-aims consider X and half consider Y (HSEED's
    /// top bit) — a prog never walks diagonally. The chosen axis aims at your
    /// coordinate plus this prog's offset; an aim point past the field's
    /// far edge by the ROM's margin wraps to the opposite edge. Ties walk
    /// left/up (the ROM compares with BLS, so "equal" turns around).
    /// </summary>
    private Direction8 PickDirection(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        IntVector2 player = field.Player.Position;

        if (_random.Next(2) == 0)
        {
            // The offset is in COLUMNS, so it becomes pixels first (§55).
            int aimX = player.X + ScreenSize.Scaled(_offsetX * ArcadePixelsPerColumn);
            if (aimX > bounds.Right + ScreenSize.Scaled(WrapMarginXColumns * ArcadePixelsPerColumn))
            {
                aimX = bounds.Left;
            }

            return aimX <= _position.X ? Direction8.Left : Direction8.Right;
        }

        int aimY = player.Y + ScreenSize.Scaled(_offsetY);
        if (aimY > bounds.Bottom + ScreenSize.Scaled(WrapMarginYRows))
        {
            aimY = bounds.Top;
        }

        return aimY <= _position.Y ? Direction8.Up : Direction8.Down;
    }

    /// <summary>ABAC frame within the walking direction's 3-frame human set.</summary>
    internal int WalkFrameIndex
    {
        get
        {
            int set = _direction switch
            {
                Direction8.Left => 0,
                Direction8.Right => 1,
                Direction8.Down => 2,
                _ => 3,
            };
            return set * 3 + WalkCycle[_frameStep];
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        Texture2D[] frames = _kind switch
        {
            HumanKind.Mikey => sprites.MikeyFrames,
            HumanKind.Mom => sprites.MomFrames,
            _ => sprites.DadFrames,
        };
        Texture2D art = frames[WalkFrameIndex];

        // Oldest ghost first so the newer ones paint over it (a prog that is
        // blocked draws its ghost and itself in the same spot), and each ghost
        // in the pose it was BORN with — the ROM blits a ghost once, so the
        // trail is a frozen ABAC sequence, not one animated snake.
        for (int i = _ghosts.Count - 1; i >= 0; i--)
        {
            Ghost ghost = _ghosts[i];
            sprites.DrawSpriteSolidWithBackground(
                spriteBatch,
                frames[ghost.FrameIndex],
                BoundsAt(ghost.Position),
                sprites.SlotColor(GameplayConstants.ProgGhostBackgroundSlot),
                sprites.SlotColor(GameplayConstants.ProgGhostShapeSlot));
        }

        sprites.DrawSpriteSolidWithBackground(
            spriteBatch,
            art,
            Bounds,
            sprites.SlotColor(GameplayConstants.ProgBackgroundSlot),
            sprites.SlotColor(GameplayConstants.ProgShapeSlot));
    }

    private Rectangle BoundsAt(IntVector2 position) =>
        new(position.X, position.Y, _collisionSize.Width, _collisionSize.Height);
}
