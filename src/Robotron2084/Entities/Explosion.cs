using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// Which way the dying sprite is cut up and fanned — the ROM has one module per
/// axis (notes §61): <see cref="Rows"/> = RRX7/RRDX2 (the sprite's ROWS become
/// strips that fan up and down), <see cref="Columns"/> = RRHX4 (its COLUMNS
/// become strips that fan left and right).
/// </summary>
public enum StripFanAxis
{
    Rows,
    Columns,
}

/// <summary>
/// The playfield interior in the strip engine's units — X in ART PIXELS, Y in
/// ROWS. The ROM's <c>XMIN 7</c>/<c>XMAX $8F</c>/<c>YMIN 24</c>/<c>YMAX 234</c>
/// (RRF.ASM:67-70) are its own screen's columns/rows; these are the port's wall.
/// Strips are DROPPED (never scaled) when they fall outside.
/// </summary>
public readonly record struct StripClip(int MinX, int MaxX, int MinY, int MaxY);

/// <summary>One strip to draw: the source index (row or column) and its top-left.</summary>
public readonly record struct Strip(int SourceIndex, int X, int Y);

/// <summary>
/// The arcade explosion — the ROM's `MAKE_ENEMY_EXPLODE` / `CREATE_EXPLOSION`
/// ($F0D7) and `CREATE_DIRECTIONAL_EXPLOSION` ($473F), plus the sources'
/// `EXSTZ`/`APGO`/`WRITE` (notes §35.5, §61, §67).
///
/// **The shape.** The Gospel's own writer settles it (notes §67, §69, §71):
///
/// <code>
/// YSIZE  = YSIZER >> 8                     ; this frame's step (1, 2, 3, …)
/// base   = YCENT − YSIZE*YOF + YSIZE/2     ; the FIRST segment's screen row
/// segment i: row = base + i*YSIZE          ; each further one steps DOWN
/// </code>
///
/// The segments are the picture's ROWS (RRX7/RRDX2) or COLUMNS (RRHX4), the step is
/// the ROM's own 16-bit accumulator, and `YCENT`/`YOF` are the fan's fixed point and
/// its offset inside the picture. At step 1 the `YSIZE*YOF` term cancels, so the base
/// IS the sprite's top row and frame 0 reconstructs the sprite exactly — the ROM's
/// "1 UNIT IS MIN". As the step grows, the base climbs while the segments march the
/// other way, so **one fan opens UP and DOWN at the same time** (the author, who
/// played the arcade: "only explodes upwards" was the port being wrong). The fixed
/// point is the picture's **MIDDLE** — the ROM's `NWCENT` centre path — so both halves
/// are mirrored: the same strips and the same reach each way (§73).
///
/// The disassembly's `CREATE_DIRECTIONAL_EXPLOSION` header ("A = ... the enemy's TOP
/// HALF must explode, B = ... the BOTTOM HALF") describes a two-half split, but $473F
/// itself stores only `A` (as `SLOPE`) and never reads `B` — it is a stale 1982
/// comment. §67 built the port's two-half model on it and was wrong; §71 records the
/// correction.
///
/// **The pace.** `YSIZER` is a 16-bit accumulator: an explosion starts at
/// <c>$0100</c> ("1 UNIT IS MIN") and adds <c>$0100</c> a frame, so the spacing
/// (its high byte) runs 1,2,3,… — the ROM's own curve; the appear (`APSTZ`) starts
/// at <c>$1000</c> and SHRINKS, so a wave's robots assemble. `FRAMES = $10` ends an
/// explosion; an appear ends when its size would reach 1. A frame is 6/5 of a port
/// tick, so the record steps on the exact-6ths clock rather than once a tick.
///
/// The port keeps the ROM's units: X in ART PIXELS and Y in ROWS, and one unit
/// of spacing is ONE PIXEL along the fan axis in both families — §71 counted the
/// H family in byte columns (2 px) and opened its fan at twice the ROM's rate;
/// §72 corrects it (see <see cref="Layout"/>). The dead entity's frame is
/// resolved at DRAW time (the dead entity's animation is frozen, so "current
/// frame" = the frame on screen at death), keeping this entity free of baked
/// Texture2D references.
/// </summary>
public sealed class Explosion : IEntity
{
    /// <summary>Explode = YSIZER grows (the fan opens); Appear = it shrinks (it converges).</summary>
    public enum Kind
    {
        Explode,
        Appear,
    }

    private readonly Func<SpriteSet, Texture2D> _art;
    private readonly Rectangle _bounds;
    private readonly Kind _kind;
    private readonly StripFanAxis _axis;
    private readonly int _slope;          // -1 / 0 / +1 — the ROM's SLOPE sign
    private readonly StripClip _clip;
    private int _sizer;                   // YSIZER: 16-bit, the high byte is the step
    private int _frames;
    private int _fifths;                  // the ROM-frame clock in 6ths (notes §52, §67.4)

    private Explosion(
        Func<SpriteSet, Texture2D> art,
        Rectangle bounds,
        Kind kind,
        StripFanAxis axis,
        int slope,
        StripClip clip)
    {
        _art = art;
        _bounds = bounds;
        _kind = kind;
        _axis = axis;
        _slope = slope;
        _clip = clip;

        // ROM EXSTZ: YSIZER = $100 "1 UNIT IS MIN" (explosion) / APSTZ:
        // YSIZER = $1000 "START LARGE FOR APPEAR".
        _sizer = kind == Kind.Explode
            ? GameplayConstants.StripExplosionStartSizer
            : GameplayConstants.StripAppearStartSizer;

        // ROM EXSTZ: FRAMES = $10. The appear ends on the SIZE (AWRITE), not a counter.
        _frames = GameplayConstants.StripExplosionFrames;
    }

    /// <summary>
    /// ROM `MAKE_ENEMY_EXPLODE` ($5C1F) → `CREATE_EXPLOSION` ($F0D7) /
    /// `CREATE_DIRECTIONAL_EXPLOSION` ($473F), and the sources' `EXSTZ`: starts the
    /// explosion for a killed object. The killing laser's direction picks the axis
    /// and the lean (see <see cref="Dispatch"/>).
    /// </summary>
    public static Explosion StartExplosion(IExplodable dead, Direction8? direction, StripClip clip)
    {
        (StripFanAxis axis, int slope) = Dispatch(direction);
        return Start(dead.CurrentFrameArt, dead.Bounds, Kind.Explode, axis, slope, clip);
    }

    /// <summary>
    /// ROM APSTZ/APSTV/HAPSTV: starts an APPEAR — the same record with the size
    /// running DOWN, so the strips CONVERGE onto the centre. This is the ROM's
    /// "materialise" effect: RRG23's `APPEAR` makes one per frame for the objects
    /// in the robot list, so a wave's robots assemble instead of appearing.
    /// </summary>
    public static Explosion StartAppear(IArtSource source, Rectangle bounds, StripFanAxis axis, int slope, StripClip clip)
        => Start(source.CurrentFrameArt, bounds, Kind.Appear, axis, slope, clip);

    /// <summary>
    /// Shared construction: the split — the fan's fixed point along the walk axis.
    /// It is the picture's **MIDDLE**, i.e. the ROM's own centre invariant from
    /// `EXST2`'s `NWCENT` path (`LDB 1,X / LSRB / STB YOF,U` — "NO GOOD" — with
    /// `YCENT` then set to the picture's centre: `ADDB UL+1,U / STB YCENT,U`, so
    /// `YCENT − spriteTop == YOF == H/2`).
    ///
    /// §73: the port used to anchor at the *collision point* (the ROM's other path,
    /// taken when the hit's offset is inside the picture) and the author's playtest
    /// says that is wrong — *"one side of the explosion is not mirrored on the other
    /// side ... the explosion half going UP is bigger than the explosion half going
    /// DOWN, and ... going LEFT is bigger than ... RIGHT"*. A laser hits the sprite's
    /// NEAR edge, so an anchored fan is always lopsided (the up half had ~all the
    /// strips and the down half ~none). With the middle anchor both halves carry half
    /// the strips and reach equally far — and the sprite still reconstructs exactly at
    /// step 1 ("1 UNIT IS MIN"), because `YCENT − YOF` is the picture's top either way.
    /// The appears always used this centre path (§62), which is why they never looked
    /// wrong.
    /// </summary>
    private static Explosion Start(
        Func<SpriteSet, Texture2D> art,
        Rectangle bounds,
        Kind kind,
        StripFanAxis axis,
        int slope,
        StripClip clip)
        => new(art, bounds, kind, axis, slope, clip);

    /// <summary>
    /// The ROM's explosion dispatch (RRX7.ASM's <c>EXSTV</c>, reached from
    /// <c>MAKE_ENEMY_EXPLODE</c> $5C1F) — **the engine is named for the axis the
    /// pieces MOVE, which is ACROSS the shot, not along it**. The source is
    /// unambiguous:
    ///
    /// <code>
    /// EXSTV  LDA LASDIR
    ///        BNE   EXST1              ; a horizontal component → EXST1
    ///        JSR   HEXST              ; NO horizontal component (a STRAIGHT VERTICAL
    ///                                 ;   shot) → the HORIZONTAL explosion
    /// EXST1  LDB   LASDIR+1
    ///        BEQ   EXST1A             ; "NO Y COMPONENT, STRAIGHT VERTICAL" →
    ///                                 ;   the VERTICAL (V) explosion
    ///        EORA  LASDIR+1 / COMA
    ///        JSR   DXST               ; both components → the DIAGONAL engine
    /// </code>
    ///
    /// and the disassembly's routing at $5C1F matches it: a pure vertical shot
    /// ($88 = 0, no horizontal component) goes to <c>CREATE_EXPLOSION</c> ($F0D7),
    /// whose split is built from <c>$A6</c> — the HIGH byte of the collision's
    /// screen address, i.e. its COLUMN (`STU $A6` stores a big-endian address, so
    /// $A6 = column, $A7 = row), compared against the picture's WIDTH. So:
    ///
    /// <list type="bullet">
    /// <item>shoot UP or DOWN → the sprite is cut into its COLUMNS and fly apart
    /// HORIZONTALLY (the "H" family: HEXST/HOREX/HORAP);</item>
    /// <item>shoot LEFT or RIGHT → cut into its ROWS and fly apart VERTICALLY (the
    /// "V" family: EXST1A), whose split uses $A7 = the collision ROW, compared
    /// against the HEIGHT;</item>
    /// <item>a diagonal shot → the ROW split with the halves leaning opposite ways
    /// (`CREATE_DIRECTIONAL_EXPLOSION`, $473F, the same A/B header the port's split
    /// follows);</item>
    /// <item>a kill with no laser direction (the tank/brain/electrode paths, which
    /// the ROM routes through <c>HVEXV</c> with `LASDIR = $0100`) lands on EXST1A
    /// too — the VERTICAL explosion.</item>
    /// </list>
    ///
    /// §61 and §67 had these two branches SWAPPED (they took "the horizontal
    /// explosion" to mean "for a horizontal shot"); the author's question *"when you
    /// shoot enemies vertically aren't they supposed to explode horizontally?"* is
    /// what exposed it (notes §69).
    /// </summary>
    internal static (StripFanAxis Axis, int Slope) Dispatch(Direction8? direction) => direction switch
    {
        // A pure vertical shot → the HORIZONTAL explosion: cut into columns.
        Direction8.Up or Direction8.Down => (StripFanAxis.Columns, 0),

        // A pure horizontal shot (and every non-laser kill: HVEXV forces $0100 and
        // lands on EXST1A) → the VERTICAL explosion: cut into rows.
        Direction8.Left or Direction8.Right or null => (StripFanAxis.Rows, 0),

        // The diagonals: the row split with SLOPE = ~(vertical ^ horizontal).
        Direction8.UpLeft or Direction8.DownRight => (StripFanAxis.Rows, -1),
        Direction8.UpRight or Direction8.DownLeft => (StripFanAxis.Rows, 1),
        _ => (StripFanAxis.Rows, 0),
    };

    public IntVector2 Position => new(_bounds.X, _bounds.Y);

    public Rectangle Bounds => _bounds;

    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    internal Kind Mode => _kind;

    /// <summary>The current spacing (YSIZER's high byte) — test hook (notes §52, §67.4).</summary>
    internal int Spacing => Math.Max(1, _sizer >> 8);

    internal StripFanAxis Axis => _axis;

    internal int Slope => _slope;

    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        // One step per ROM FRAME, not per port tick: `NAP`-free in the ROM, the
        // records are walked once a frame, so on the exact-6ths clock a step lands
        // every 1.2 ticks (5 sixths a tick). Notes §52, §65, §67.4 — the records used
        // to advance a step a tick, i.e. 20% fast.
        _fifths += 5;
        if (_fifths < 6)
        {
            return;
        }

        _fifths -= 6;

        if (_kind == Kind.Explode)
        {
            // ROM WRITE: DEC FRAMES / BEQ KILEXP (freed before the draw), then
            // YSIZER += $100.
            if (--_frames <= 0)
            {
                LifeState = EntityLifeState.Dead;
                return;
            }

            _sizer += GameplayConstants.StripSizerStep;
            return;
        }

        // ROM AWRITE: YSIZER -= $100, and the record dies once the step would
        // fall to 1 or less ("CMPA #1 / BHI APGO").
        int next = _sizer - GameplayConstants.StripSizerStep;
        if ((next >> 8) <= 1)
        {
            LifeState = EntityLifeState.Dead;
            return;
        }

        _sizer = next;
    }

    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        Texture2D art = _art(sprites);

        // A texture's dimensions are in ART pixels: `SpriteSet.CentredIn` scales them by
        // SpecScale when it draws a sprite, so `art.Width` IS the picture's width in art
        // pixels and `art.Height` is its ROW count. Dividing by SpecScale here halved the
        // fan — the extent came out half of what the anchor was computed from, `split`
        // clamped to `extent−1`, and the top half ended up five steps long against the
        // bottom's zero: the author's "the top half is much longer than the bottom half"
        // (notes §75).
        int widthArt = art.Width;
        int heightRows = art.Height;

        foreach (Strip strip in Layout(widthArt, heightRows))
        {
            // Sources are in TEXTURE pixels (one art row / column per strip); destinations
            // are in PORT pixels (art × SpecScale).
            Rectangle source = _axis == StripFanAxis.Rows
                ? new Rectangle(0, strip.SourceIndex, widthArt, 1)
                : new Rectangle(strip.SourceIndex, 0, 1, heightRows);

            Rectangle dest = _axis == StripFanAxis.Rows
                ? new Rectangle(
                    strip.X * ScreenSize.SpecScale,
                    strip.Y * ScreenSize.SpecScale,
                    widthArt * ScreenSize.SpecScale,
                    ScreenSize.SpecScale)
                : new Rectangle(
                    strip.X * ScreenSize.SpecScale,
                    strip.Y * ScreenSize.SpecScale,
                    ScreenSize.SpecScale,
                    heightRows * ScreenSize.SpecScale);

            spriteBatch.Draw(art, dest, source, Color.White);
        }
    }

    /// <summary>
    /// Where a picture sits when it is drawn into an entity's bounds, in ART pixels:
    /// `SpriteSet.CentredIn` scales the texture by <see cref="ScreenSize.SpecScale"/> and
    /// centres it, so a picture's extent IS its texture's width/height and its top-left is
    /// the bounds' centre minus half of it. The fan is laid out from here (notes §75).
    /// </summary>
    internal static (int WidthArt, int HeightRows, int Left, int Top) PicturePlacement(
        Rectangle bounds, int textureWidth, int textureHeight)
    {
        int boundsWidth = bounds.Width / ScreenSize.SpecScale;
        int boundsHeight = bounds.Height / ScreenSize.SpecScale;

        return (
            textureWidth,
            textureHeight,
            (bounds.X / ScreenSize.SpecScale) + ((boundsWidth - textureWidth) / 2),
            (bounds.Y / ScreenSize.SpecScale) + ((boundsHeight - textureHeight) / 2));
    }

    /// <summary>
    /// The strips for the current frame (art px / rows) — the ROM's `WRITE`
    /// (RRX7/RRHX4/RRDX2), pure so the shape is unit-testable.
    ///
    /// It is **ONE fan, not two halves**, and it opens BOTH ways at once:
    ///
    /// <code>
    /// YSIZE  = YSIZER >> 8                       ; this frame's step (1, 2, 3, …)
    /// base   = YCENT − YSIZE*YOF + YSIZE/2       ; the FIRST segment's screen row
    /// segment i: row = base + i*YSIZE            ; each further one steps DOWN
    /// </code>
    ///
    /// `YCENT` is the fan's fixed point and `YOF` its offset inside the picture, so at
    /// step 1 the `YSIZE*YOF` term cancels and the base IS the sprite's top row — frame 0
    /// reconstructs the sprite exactly (the ROM's "1 UNIT IS MIN"). As the step grows the
    /// base climbs (`−YSIZE*YOF`) faster than the segments march away from it (`+YSIZE`
    /// each), so the fan tears UP and DOWN at the same time — which is the arcade's look
    /// and what the author described.
    ///
    /// The fixed point is the picture's **MIDDLE** (`split = extent/2`), the ROM's
    /// `NWCENT` centre invariant, so the two halves are MIRRORED: half the strips and the
    /// same reach each way (§73). Anchoring at the *hit* instead made the up (or left)
    /// half bigger, because a laser strikes the sprite's near edge.
    ///
    /// For a mirrored fan (a vertical shot, `StripFanAxis.Columns`) the same maths runs
    /// on columns: the base column climbs leftwards while the segments march right, and
    /// — per RRHX4's `ASLA`/`DECA`, the ROM's own "pixel width" walk — a unit of spacing
    /// is one PIXEL there too, not a byte column (notes §72).
    ///
    /// The lean (`X = ±(YSIZE>>1)`) is the DIAGONAL module's (RRDX2, notes §74) and it is
    /// measured **from the fan's fixed point**: the first strip is placed at
    /// `XCENT − YOF*XSIZE` and each next one adds `XSIZE`, so strip i shifts by
    /// `slope * 2 * (step >> 1) * (i − OFFSET)` art pixels — a column is two arcade pixels.
    /// The rows above the fixed point therefore lean one way and the rows below it the
    /// other: a mirrored chevron.
    ///
    /// A strip that falls outside the playfield is DROPPED (the ROM's clip passes), not
    /// clamped. The dead entity is drawn WHERE IT STOOD, with the fan's fixed point at
    /// the picture's middle.
    /// </summary>
    internal IReadOnlyList<Strip> Layout(int widthArt, int heightRows)
    {
        bool rows = _axis == StripFanAxis.Rows;
        int extent = rows ? heightRows : widthArt;

        var strips = new List<Strip>(extent);
        int spacing = _sizer >> 8;
        if (spacing < 1)
        {
            spacing = 1;
        }

        // The fan's fixed point is the PICTURE's middle (notes §73): `Layout` derives it
        // from the art's own extent, which is the only place the picture's size is known
        // (the ROM's `YOF` is `H/2` on the centre path, `NWCENT`, and the appear
        // converges on the same point).
        int split = extent / 2;

        // The art is drawn CENTRED inside the entity's bounds (`SpriteSet.CentredIn`), so
        // the fan must start from the art's own top-left — not the bounds' corner — or a
        // picture smaller than its box fans from the wrong place (notes §75).
        (int _, int _, int spriteLeft, int spriteTop) = PicturePlacement(_bounds, widthArt, heightRows);

        // YCENT/XCENT: the fixed point's own screen row/column.
        int centre = (rows ? spriteTop : spriteLeft) + split;

        // The ROM's unit is ONE PIXEL of the picture along the fan axis, for BOTH
        // families. RRX7's V engine counts rows (1 px each); RRHX4's H engine counts
        // PIXEL COLUMNS — `LDA WH,Y / ASLA` ("DOUBLE FOR PIXEL WIDTH") and `DECA`
        // ("NO NEED TO DO ZEROS ON LEFT COLUMN") — one strip per pixel, two pixels
        // wide, so consecutive strips OVERLAP by half and the fan reads as one bright
        // sheet (notes §72). Counting the H family in BYTE columns (2 px) instead made
        // its fan fly off at twice the ROM's rate and left it a sparse comb — the
        // author's "the vertical explosion ... doesn't last very long".
        const int unit = 1;
        int step = spacing * unit;

        // ROM WRITE: base = CENTRE − SIZE*OFFSET + SIZE/2. The "obscure bug" guard
        // (no half step when OFFSET is zero) is kept for faithfulness, but a
        // middle-anchored fan never has a zero offset — see Start.
        int half = split == 0 ? 0 : (spacing >> 1) * unit;
        int fanBase = centre - (spacing * split) + half;

        // ROM RRDX2 (the DIAGONAL module, notes §74): `XSIZE = ±(YSIZE>>1)` in COLUMNS
        // — the sign comes from SLOPE (`LDB SLOPE,Y / BPL APGG1 / NEGA`) and a column is
        // two arcade pixels — and the FIRST strip sits at `XCENT − YOFF*XSIZE`
        // (`LDA YOFF / LDB XSIZE / MUL / LDB XCENT / SUBD TEMP1`), each next one
        // `+= XSIZE`. So strip i's lateral is `(i − OFFSET) * XSIZE`: measured from the
        // fan's FIXED POINT, not from the first strip. The two halves therefore lean
        // OPPOSITE ways — a mirrored chevron, which is what the `$473F` header's A/B pair
        // describes. Leaning the WHOLE fan one way (`i * XSIZE`) shears it off to one
        // side, so the strips at one end leave the playfield and get dropped: the author's
        // "the half going UP is bigger than the half going DOWN, and … going LEFT is
        // bigger than … RIGHT".
        int drift = _slope * ((spacing >> 1) * ScreenSize.SpecScale);

        for (int i = 0; i < extent; i++)
        {
            int along = fanBase + (i * step);
            int lateral = (i - split) * drift;

            int x = rows ? spriteLeft + lateral : along;
            int y = rows ? along : spriteTop + lateral;

            if (IsInside(x, y, widthArt, heightRows))
            {
                strips.Add(new Strip(i, x, y));
            }
        }

        return strips;
    }

    /// <summary>ROM's per-strip clip: a strip outside the playfield is DROPPED.</summary>
    private bool IsInside(int x, int y, int widthArt, int heightRows)
    {
        if (_axis == StripFanAxis.Rows)
        {
            return x >= _clip.MinX && x + widthArt <= _clip.MaxX && y >= _clip.MinY && y < _clip.MaxY;
        }

        return y >= _clip.MinY && y + heightRows <= _clip.MaxY && x >= _clip.MinX && x < _clip.MaxX;
    }
}
