using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// Which way the dying sprite is cut up and fanned: <see cref="Rows"/> cuts the
/// sprite into its ROWS, which fan up and down; <see cref="Columns"/> cuts it into
/// its COLUMNS, which fan left and right.
/// </summary>
/// <remarks>The ROM has separate code for each axis (notes §61): <see cref="Rows"/> is handled by
/// the ROM's vertical-fan code (RRX7.ASM/RRDX2.ASM), <see cref="Columns"/> by its horizontal-fan
/// code (RRHX4.ASM).</remarks>
public enum StripFanAxis
{
    Rows,
    Columns,
}

/// <summary>
/// The playfield interior in the strip engine's units — X in ART PIXELS, Y in
/// ROWS. Strips are DROPPED (never scaled) when they fall outside.
/// </summary>
/// <remarks>The ROM's own playfield-edge constants (ROM: RRF.ASM) mark out its screen's
/// columns/rows; these are the port's equivalent, expressed as the wall rectangle.</remarks>
public readonly record struct StripClip(int MinX, int MaxX, int MinY, int MaxY);

/// <summary>One strip to draw: the source index (row or column) and its top-left.</summary>
public readonly record struct Strip(int SourceIndex, int X, int Y);

/// <summary>
/// The shared death-effect visual: whenever the player shoots (or otherwise kills) almost
/// any enemy — grunt, hulk, brain, spheroid, tank and more all share this one class — its
/// sprite doesn't just vanish. The dying sprite is cut into its rows (or columns) and those
/// strips (like a picture cut into slices) fan out from the picture's own middle, spreading
/// further apart each frame until the effect ends and the strips are gone. This is what the
/// player sees as an "explosion". The same class also plays the effect in reverse at the
/// start of a wave (an <see cref="Kind.Appear"/>): the spacing runs DOWN instead of up, so the
/// strips CONVERGE onto the centre instead of flying apart — robots don't just pop into
/// existence, they "materialise" as their strips fly inward and assemble the finished picture.
///
/// One explosion is a small fan that opens UP and DOWN at the same time, and the two
/// halves are mirrored: they carry the same strips and reach equally far. A diagonal
/// shot leans the halves opposite ways, making a chevron. At the first frame the
/// spacing is 1 and the strips reconstruct the sprite exactly.
///
/// The port keeps the arcade's units: X in ART PIXELS and Y in ROWS, and one unit of spacing
/// is ONE PIXEL along the fan axis, for the row cut and the column cut alike. The dead entity's
/// frame is resolved at DRAW time (its animation is frozen, so "current frame" is the frame on
/// screen at death), keeping this entity free of baked Texture2D references.
/// </summary>
/// <remarks>
/// <para>
/// See the terminology glossary on <see cref="IEntity"/> for what "ROM frame", the
/// "..Timer" clock and "notes §NN" mean generally; this class steps its own <c>_timer</c>
/// clock once per ROM frame, same as everything else, but it is not itself an entity with a
/// "beat" in the usual sense — it has no per-frame scheduler of its own, it is just a
/// short-lived visual record that ages out.
/// </para>
/// Ported from the arcade's own explosion effect (ROM: the enemy-death and directional-explosion
/// routines, plus their shared setup and per-frame layout code; notes §35.5, §61, §67).
///
/// **The shape.** The Gospel's own writer settles it (notes §67, §69, §71):
///
/// <code>
/// YSIZE  = YSIZER >> 8                     ; this frame's step (1, 2, 3, …)
/// base   = YCENT − YSIZE*YOF + YSIZE/2     ; the FIRST segment's screen row
/// segment i: row = base + i*YSIZE          ; each further one steps DOWN
/// </code>
///
/// The segments are the picture's ROWS (row-cut family) or COLUMNS (column-cut family), the step
/// is the ROM's own running total for the frame, and <c>YCENT</c>/<c>YOF</c> are the fan's fixed
/// point and its offset inside the picture. At step 1 the offset term cancels, so the base IS the
/// sprite's top row and frame 0 reconstructs the sprite exactly — the ROM's own "1 unit is the
/// minimum" rule. As the step grows, the base climbs while the segments march the other way, so
/// **one fan opens UP and DOWN at the same time**. The fixed point is the picture's **MIDDLE** —
/// the ROM's own centring logic — so both halves are mirrored: the same strips and the same reach
/// each way (§73).
///
/// The directional-explosion routine's own 1982 comment describes a two-half split, but the
/// routine itself only ever stores one of the two values and never reads the other; §71 records
/// the correction.
///
/// **The pace.** The spacing accumulator starts at a small value ("1 unit is the minimum") for an
/// explosion and grows by a fixed step every frame, so the spacing runs 1,2,3,… — the ROM's own
/// curve; an appear starts LARGE and SHRINKS instead, so a wave's robots assemble. A fixed frame
/// count ends an explosion; an appear ends when its size would shrink to 1. A ROM frame is 6/5 of
/// a port tick, so the record steps on the exact-6ths clock rather than once a tick.
///
/// The port keeps the ROM's units: X in ART PIXELS and Y in ROWS, and one unit
/// of spacing is ONE PIXEL along the fan axis in both families (notes §72; see
/// <see cref="Layout"/>). The dead entity's frame is resolved at DRAW time (the
/// dead entity's animation is frozen, so "current frame" = the frame on screen at
/// death), keeping this entity free of baked Texture2D references.
/// </remarks>
public sealed class Explosion : IEntity
{
    /// <summary>Explode = the spacing grows (the fan opens); Appear = it shrinks (it converges).</summary>
    /// <remarks>The ROM's own spacing accumulator, run forwards for an explosion and backwards
    /// for an appear.</remarks>
    public enum Kind
    {
        Explode,
        Appear,
    }

    private readonly Func<SpriteSet, Texture2D> _art;
    private readonly Rectangle _bounds;
    private readonly Kind _kind;
    private readonly StripFanAxis _axis;
    private readonly int _slope;          // the diagonal lean: -1 / 0 / +1 (ROM: SLOPE)
    private readonly StripClip _clip;
    private int _sizer;                   // the spacing accumulator; its high byte is this frame's step (ROM: YSIZER)
    private int _frames;
    private int _timer;                  // the ROM-frame clock in 6ths (notes §52, §67.4)

    /// <summary>Builds one record; the two static factories below are the only callers.</summary>
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

        // The spacing accumulator starts small for an explosion ("1 unit is the
        // minimum" — so the first frame reconstructs the sprite exactly) or
        // large for an appear, so it can shrink back down as the robot assembles.
        _sizer = kind == Kind.Explode
            ? GameplayConstants.StripExplosionStartSizer
            : GameplayConstants.StripAppearStartSizer;

        // An explosion runs for a fixed frame count; an appear instead ends when
        // its shrinking size reaches 1, so it has no frame counter of its own.
        _frames = GameplayConstants.StripExplosionFrames;
    }

    /// <summary>
    /// Starts the explosion for a killed object. The killing laser's direction picks
    /// the axis and the lean (see <see cref="Dispatch"/>), and the record's rect is
    /// <see cref="IExplodable.ExplosionBounds"/> — the picture the object is pointing at,
    /// which can be bigger than its collision box (a "prog" — the enemy that disguises
    /// itself as a rescuable human, then unmasks — has a phony human card bigger than the
    /// human box it was standing in).
    /// </summary>
    /// <param name="dead">The object being exploded; its art and explosion bounds are used.</param>
    /// <param name="direction">The killing shot's direction, or null for a kill with no laser (the vertical fan).</param>
    /// <param name="clip">The playfield interior that strips are dropped outside of.</param>
    /// <returns>The record to add to the playfield's explosion list.</returns>
    /// <remarks>Ported from the arcade's own "make an enemy explode" logic, which dispatches to
    /// either its straight or its directional explosion setup depending on the killing shot. The
    /// rect is the ROM's own top-left position with the width/height of the picture the object is
    /// pointing at.</remarks>
    public static Explosion StartExplosion(IExplodable dead, Direction8? direction, StripClip clip)
    {
        (StripFanAxis axis, int slope) = Dispatch(direction);
        return Start(dead.CurrentFrameArt, dead.ExplosionBounds, Kind.Explode, axis, slope, clip);
    }

    /// <summary>
    /// Starts an APPEAR — the same record with the size running DOWN, so the strips
    /// CONVERGE onto the centre. This is the "materialise" effect, so a wave's robots
    /// assemble instead of just appearing.
    /// </summary>
    /// <param name="source">The object materialising; its current art is used.</param>
    /// <param name="bounds">The rect the strips are laid out in.</param>
    /// <param name="axis">Which way the sprite is cut: rows or columns.</param>
    /// <param name="slope">The diagonal lean, -1 / 0 / +1.</param>
    /// <param name="clip">The playfield interior that strips are dropped outside of.</param>
    /// <returns>The record to add to the playfield's explosion list.</returns>
    /// <remarks>Ported from the arcade's own appear setup (ROM: RRG23.ASM's `APPEAR` routine makes
    /// one of these per frame for each object in the robot list).</remarks>
    public static Explosion StartAppear(IArtSource source, Rectangle bounds, StripFanAxis axis, int slope, StripClip clip)
        => Start(source.CurrentFrameArt, bounds, Kind.Appear, axis, slope, clip);

    /// <summary>
    /// Shared construction: the split — the fan's fixed point along the walk axis. It
    /// is the picture's **MIDDLE**, so both halves carry half the strips and reach
    /// equally far, and the sprite still reconstructs exactly at step 1.
    /// </summary>
    /// <param name="art">Resolves the picture to cut up, at draw time.</param>
    /// <param name="bounds">The rect the strips are laid out in.</param>
    /// <param name="kind">Explode (the spacing grows) or Appear (it shrinks).</param>
    /// <param name="axis">Which way the sprite is cut: rows or columns.</param>
    /// <param name="slope">The diagonal lean, -1 / 0 / +1.</param>
    /// <param name="clip">The playfield interior that strips are dropped outside of.</param>
    /// <returns>The new record.</returns>
    /// <remarks>
    /// The centre point matches the ROM's own centring logic: the picture's offset is half its
    /// height, and the fixed point is set to the picture's top plus that offset — i.e. its
    /// vertical middle.
    ///
    /// Anchoring at the *collision point* instead (the ROM's other, rarer path, taken when the
    /// hit's offset happens to fall inside the picture) is lopsided: a laser hits the sprite's
    /// NEAR edge, so the up half gets ~all the strips and the down half ~none. The middle anchor
    /// keeps both halves carrying half the strips and reaching equally far — and the sprite still
    /// reconstructs exactly at step 1 ("1 unit is the minimum"), because the fixed point minus its
    /// offset is the picture's top either way (§73). Don't revert to the collision-point anchor
    /// without checking this.
    /// </remarks>
    private static Explosion Start(
        Func<SpriteSet, Texture2D> art,
        Rectangle bounds,
        Kind kind,
        StripFanAxis axis,
        int slope,
        StripClip clip)
        => new(art, bounds, kind, axis, slope, clip);

    /// <summary>
    /// Maps a killing shot's direction to the explosion it produces. The axis named is the
    /// one the pieces MOVE along, which is ACROSS the shot, not along it:
    /// <list type="bullet">
    /// <item>shoot UP or DOWN → the sprite is cut into its COLUMNS, which fan apart
    /// horizontally;</item>
    /// <item>shoot LEFT or RIGHT, or kill with no laser direction, → cut into its ROWS,
    /// which fan apart vertically;</item>
    /// <item>a diagonal shot → the row split, with the halves leaning opposite ways.</item>
    /// </list>
    /// </summary>
    /// <param name="direction">The killing shot's direction, or null for a kill with no laser.</param>
    /// <returns>The fan axis and the lean: -1, 0 or +1.</returns>
    /// <remarks>
    /// Ported from the arcade's own explosion dispatch (ROM: RRX7.ASM's explosion-style routine,
    /// reached from the "make an enemy explode" entry point) — **the engine is named for the axis
    /// the pieces MOVE, which is ACROSS the shot, not along it**. The ROM's own branching is
    /// unambiguous about this: it checks the shot's horizontal component first, and only falls
    /// through to the "no horizontal component" (straight vertical shot) case when there is none;
    /// a shot with no vertical component is the "straight vertical" case for the split itself;
    /// and a shot with both components goes to the diagonal engine.
    ///
    /// The routing matches: a pure vertical shot (no horizontal component) goes to the ROM's
    /// straight explosion routine, whose split is built from the collision point's COLUMN compared
    /// against the picture's WIDTH. So:
    ///
    /// <list type="bullet">
    /// <item>shoot UP or DOWN → the sprite is cut into its COLUMNS and fly apart
    /// HORIZONTALLY (the ROM's horizontal-fan family);</item>
    /// <item>shoot LEFT or RIGHT → cut into its ROWS and fly apart VERTICALLY (the
    /// ROM's vertical-fan family), whose split uses the collision point's ROW, compared
    /// against the picture's HEIGHT;</item>
    /// <item>a diagonal shot → the ROW split with the halves leaning opposite ways
    /// (the ROM's directional-explosion routine, the same two-value setup the port's split
    /// follows);</item>
    /// <item>a kill with no laser direction (the tank/brain/electrode paths, which the ROM
    /// routes through a shared helper that forces "no direction") lands on the same case as a
    /// pure horizontal shot too — the VERTICAL explosion.</item>
    /// </list>
    ///
    /// These two branches are easy to swap by mistake — "the horizontal explosion" names the
    /// axis the pieces move along, not the shot's own axis (notes §69).
    /// </remarks>
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

    /// <summary>The dead entity's top-left, which is also where the fan is centred (its middle).</summary>
    public IntVector2 Position => new(_bounds.X, _bounds.Y);

    /// <summary>The dead entity's own box.</summary>
    public Rectangle Bounds => _bounds;

    /// <summary>Alive for the record's life: a fixed number of ROM frames for an explosion, or until the
    /// spacing would reach 1 for an appear.</summary>
    /// <remarks>The ROM's own frame counter ends an explosion; an appear ends on its shrinking size instead.</remarks>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Explode or Appear (test hook).</summary>
    internal Kind Mode => _kind;

    /// <summary>The current spacing (the sizer's high byte) — test hook.</summary>
    /// <remarks>Notes §52, §67.4.</remarks>
    internal int Spacing => Math.Max(1, _sizer >> 8);

    /// <summary>The axis the pieces fly along: Rows for a vertical fan, Columns for a horizontal one (test hook).</summary>
    /// <remarks>The ROM calls these the vertical family (rows) and the horizontal family (columns).</remarks>
    internal StripFanAxis Axis => _axis;

    /// <summary>The diagonal lean, -1 / 0 / +1 (test hook — see <see cref="Dispatch"/>).</summary>
    internal int Slope => _slope;

    /// <summary>
    /// One ROM frame of the record's life. <paramref name="field"/> is unused —
    /// the strips carry their own art and clipping — and is kept only for the
    /// playfield's call shape; the attract movie's explosions pass nothing.
    /// </summary>
    /// <param name="gameTime">Unused — the record is stepped once per ROM frame.</param>
    /// <param name="field">Unused — the strips carry their own art and clipping — and is kept only for the playfield's call shape.</param>
    public void Update(GameTime gameTime, PlayField? field = null)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        // One step per ROM frame, not per port tick: the ROM walks these records once a
        // frame with no extra delay, so (via the fixed-point fifth-ticks clock — see
        // IEntity) a step lands every 1.2 ticks, not every 1. Notes §52, §65, §67.4 — an
        // earlier version advanced a step every tick, running 20% fast.
        _timer += 5;
        if (_timer < 6)
        {
            return;
        }

        _timer -= 6;

        if (_kind == Kind.Explode)
        {
            // Count the frame down; once it reaches zero the explosion is over
            // and the record is freed before the next draw. Otherwise grow the
            // spacing by one step for next frame.
            if (--_frames <= 0)
            {
                LifeState = EntityLifeState.Dead;
                return;
            }

            _sizer += GameplayConstants.StripSizerStep;
            return;
        }

        // Shrink the spacing by one step, and the record dies once that step
        // would fall to 1 or less.
        int next = _sizer - GameplayConstants.StripSizerStep;
        if ((next >> 8) <= 1)
        {
            LifeState = EntityLifeState.Dead;
            return;
        }

        _sizer = next;
    }

    /// <summary>Draws the frame's strips, each from its own row or column of the dead entity's picture.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which resolves the dead entity's picture.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        Texture2D art = _art(sprites);

        // A texture's dimensions are in ART pixels: the sprite-drawing helper scales them
        // up when it draws a sprite, so `art.Width` IS the picture's width in art pixels
        // and `art.Height` is its ROW count. Do not scale these back down — that halves
        // the extent the anchor and split are computed from and makes the fan lopsided
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
    /// Where a picture sits when it is drawn into an entity's bounds, in ART pixels: the
    /// sprite-drawing helper scales the texture by <see cref="ScreenSize.SpecScale"/> and
    /// centres it, so a picture's extent IS its texture's width/height and its top-left is
    /// the bounds' centre minus half of it. The fan is laid out from here.
    /// </summary>
    /// <param name="bounds">The entity's bounds, in port pixels.</param>
    /// <param name="textureWidth">The picture's width in art pixels.</param>
    /// <param name="textureHeight">The picture's height in rows.</param>
    /// <returns>The picture's extent and the top-left it is drawn at, in art pixels/rows.</returns>
    /// <remarks>Notes §75.</remarks>
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
    /// The strips for the current frame (art px / rows), pure so the shape is
    /// unit-testable.
    ///
    /// It is **ONE fan, not two halves**, and it opens BOTH ways at once: the first strip
    /// starts at the sprite's top row and each further one steps DOWN, while the whole
    /// run's base climbs the other way as the spacing grows — so at spacing 1 the strips
    /// reconstruct the sprite exactly, and after that the fan tears UP and DOWN.
    ///
    /// The fixed point is the picture's **MIDDLE**, so the two halves are MIRRORED: they
    /// carry half the strips and the same reach each way. For a vertical shot the same
    /// maths runs on columns, and a unit of spacing is one PIXEL there too.
    ///
    /// A diagonal shot leans the strips from that fixed point, so the rows above it lean
    /// one way and the rows below it the other: a mirrored chevron.
    ///
    /// A strip that falls outside the playfield is DROPPED, not clamped. The dead entity
    /// is drawn WHERE IT STOOD, with the fan's fixed point at the picture's middle.
    /// </summary>
    /// <param name="widthArt">The dead picture's width in art pixels.</param>
    /// <param name="heightRows">The dead picture's height in rows.</param>
    /// <returns>The strips to draw this frame, in draw order.</returns>
    /// <remarks>
    /// Ported from the arcade's own strip-layout logic (ROM: RRX7.ASM/RRHX4.ASM/RRDX2.ASM):
    ///
    /// <code>
    /// YSIZE  = YSIZER >> 8                       ; this frame's step (1, 2, 3, …)
    /// base   = YCENT − YSIZE*YOF + YSIZE/2       ; the FIRST segment's screen row
    /// segment i: row = base + i*YSIZE            ; each further one steps DOWN
    /// </code>
    ///
    /// <c>YCENT</c> is the fan's fixed point and <c>YOF</c> its offset inside the picture, so at
    /// step 1 the offset term cancels and the base IS the sprite's top row — frame 0
    /// reconstructs the sprite exactly (the ROM's own "1 unit is the minimum" rule). As the step
    /// grows the base climbs faster than the segments march away from it, so the fan tears UP and
    /// DOWN at the same time — the arcade's own look.
    ///
    /// The fixed point is the picture's **MIDDLE** (the split point is half the picture's extent),
    /// the ROM's own centring invariant, so the two halves are MIRRORED: half the strips and the
    /// same reach each way (§73). Anchoring at the *hit* instead makes the up (or left)
    /// half bigger, because a laser strikes the sprite's near edge.
    ///
    /// For a mirrored fan (a vertical shot, <see cref="StripFanAxis.Columns"/>) the same maths runs
    /// on columns: the base column climbs leftwards while the segments march right, and — following
    /// the ROM's own "pixel width" walk in its horizontal-fan code — a unit of spacing
    /// is one PIXEL there too, not a byte column (notes §72).
    ///
    /// The diagonal lean is the ROM's own directional-explosion logic (notes §74) and it is
    /// measured **from the fan's fixed point**: the first strip is placed at an offset from the
    /// centre, and each next one steps by a further fixed amount, so strip i shifts sideways in
    /// proportion to its distance from the split point — a column is two arcade pixels. The rows
    /// above the fixed point therefore lean one way and the rows below it the other: a mirrored
    /// chevron.
    ///
    /// A strip that falls outside the playfield is DROPPED (matching the ROM's own clip check),
    /// not clamped. The dead entity is drawn WHERE IT STOOD, with the fan's fixed point at
    /// the picture's middle.
    /// </remarks>
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

        // The fan's fixed point is the PICTURE's middle (notes §73): this derives it
        // from the art's own extent, which is the only place the picture's size is known
        // (the ROM's own offset is half the picture's height on its centring path, and
        // the appear converges on the same point).
        int split = extent / 2;

        // The art is drawn CENTRED inside the entity's bounds, so the fan must start
        // from the art's own top-left — not the bounds' corner — or a picture smaller
        // than its box fans from the wrong place (notes §75).
        (int _, int _, int spriteLeft, int spriteTop) = PicturePlacement(_bounds, widthArt, heightRows);

        // The fixed point's own screen row/column.
        int centre = (rows ? spriteTop : spriteLeft) + split;

        // The ROM's unit is ONE PIXEL of the picture along the fan axis, for BOTH
        // families: the vertical-fan code counts rows (1 px each); the horizontal-fan
        // code counts PIXEL COLUMNS directly rather than byte columns — one strip per
        // pixel, two pixels wide, so consecutive strips OVERLAP by half and the fan
        // reads as one bright sheet. Counting the horizontal family in BYTE columns
        // (2 px) instead flies the fan off at twice the ROM's rate (notes §72).
        const int unit = 1;
        int step = spacing * unit;

        // The ROM computes the fan's base as centre minus (size times offset) plus
        // half a step. The "obscure bug" guard (no half step when the offset is zero)
        // is kept for faithfulness, but a middle-anchored fan never has a zero offset
        // — see Start.
        int half = split == 0 ? 0 : (spacing >> 1) * unit;
        int fanBase = centre - (spacing * split) + half;

        // The diagonal lean (ROM: RRDX2.ASM, notes §74): its size is half the current
        // step, signed by the shot's diagonal direction, and a column is two arcade
        // pixels. The FIRST strip sits offset from the centre by that lean, and each
        // next strip steps by the same amount again — so strip i's sideways shift is
        // proportional to its distance from the fixed point (the split), not from the
        // first strip. The two halves therefore lean OPPOSITE ways — a mirrored
        // chevron, matching the ROM's own two-value directional-explosion setup.
        // Leaning the WHOLE fan one way instead shears it off to one side, so the
        // strips at one end leave the playfield and get dropped.
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

    /// <summary>True when the strip still lies inside the clip rectangle; a strip outside is DROPPED,
    /// never clamped.</summary>
    /// <remarks>Matches the ROM's own per-strip clip check.</remarks>
    private bool IsInside(int x, int y, int widthArt, int heightRows)
    {
        if (_axis == StripFanAxis.Rows)
        {
            return x >= _clip.MinX && x + widthArt <= _clip.MaxX && y >= _clip.MinY && y < _clip.MaxY;
        }

        return y >= _clip.MinY && y + heightRows <= _clip.MaxY && x >= _clip.MinX && x < _clip.MaxX;
    }
}
