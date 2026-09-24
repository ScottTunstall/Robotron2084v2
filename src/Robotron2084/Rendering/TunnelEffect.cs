using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Tuning;

namespace Robotron2084.Rendering;

/// <summary>
/// The ROM's wave-complete effect — `DRAW_COLOUR_CYCLING_TUNNEL_EFFECT` ($5703) and its
/// ring primitive `DRAW_RECTANGULAR_PART_OF_TUNNEL` ($5A11), both read line by line
/// (notes §78.2, §79).
///
/// <para>
/// **It EXPANDS, it does not shrink.** It starts as a two-row-tall box straddling the
/// screen's centre (corners `$3B80` = column 59, row 128 and `$5A82` = column 90, row 130
/// — row 128 is the middle of the ROM's 256) and walks outward: the top-left steps one
/// COLUMN (two arcade pixels) LEFT and TWO ROWS up, the bottom-right one column right and
/// two rows down, so each ring is 4 px wider and 4 rows taller than the last. That extra
/// vertical stretch per ring is what makes nested rectangles read as a tunnel.
/// </para>
/// <para>
/// It stops when the top-left reaches `$0616` (column 6, row 22) — 53 rings, which leaves
/// the bottom-right on the screen's own edges — and then the WHOLE walk runs again with
/// the colour forced to 0, redrawing every ring in black to clear it.
/// </para>
/// <para>
/// Two rings are drawn per pass and the task's delay is 1, so it advances two rings a
/// frame. <see cref="PassFifths"/> holds how long a pass takes.
/// </para>
/// <para>
/// The colours are a packed byte — the LEFT nibble is colour 0 and the RIGHT nibble colour
/// 1, both palette slots — which steps once per ring (see <see cref="NextPair"/>). That
/// chain is the "colour cycling" in the routine's name.
/// </para>
/// <para>
/// The HATCHING is the ring primitive's own shape (notes §85): every edge is ONE ROM pixel
/// tall or wide and the walk lays its bands two rows apart, so the bands TILE — successive
/// rows carry successive palette slots, which is why the arcade reads as a smooth gradient
/// field rather than as discrete rings. The band's second line is the same row moved one
/// column right and one column narrower (`$5AAA`, so it is inset a column at each end), an
/// odd-height correction pulls the bottom row up by one to keep the band inside the ring,
/// and the vertical edges — one column, i.e. TWO ROM pixels — are blitted with the packed
/// nibbles as their two pixels' colours, MIRRORED between the left and the right edge
/// (`$5AD5`'s `$EF` against `$5ADA`'s `$FE`). There is no dash pattern to invent: the hatch
/// IS those two-pixel edges.
/// </para>
/// </summary>
public sealed class TunnelEffect
{
    // The walk is kept in the ROM's own SCREEN coordinates (X = column, Y = row; its screen
    // addresses are column*256 + row) so the numbers in the code are the ROM's, and only the
    // drawing maps them onto the port's grid.
    internal const int StartLeftColumn = 0x3B;   // $3B80
    internal const int StartTopRow = 0x80;
    internal const int StartRightColumn = 0x5A;  // $5A82
    internal const int StartBottomRow = 0x82;
    internal const int EndLeftColumn = 0x06;     // $0616 — the last ring's top-left
    internal const int EndTopRow = 0x16;

    /// <summary>ROM `LDB #$02 / STB $000E,U` — two rings per task pass.</summary>
    internal const int RingsPerPass = 2;

    /// <summary>
    /// How long a task pass lasts, in sixths of a port tick (notes §83).
    ///
    /// `ALLOCATE_TASK` ($D1E3) documents its delay as *"A x 16 Millisec"* and `$571E` passes
    /// `A = 1`, which at the ROM's own unit would put the whole effect — 54 passes over the two
    /// phases — at under a second. The author's playtest of the arcade says it runs **about two
    /// seconds at least**, so the task list is walked SLOWER than one cycle per field: a pass is
    /// **two ROM frames** (2.4 port ticks on §52's exact-6ths clock), which lands the effect at
    /// 54 x 2.4 = ~130 ticks = ~2.2 s.
    ///
    /// This is the one number in the tunnel taken from the playtest rather than the disassembly,
    /// and it is called out as such in the notes (and in the handoff) so a MAME measurement can
    /// settle it.
    /// </summary>
    internal const int PassFifths = 2 * 6;

    private int _fifths;

    /// <summary>
    /// The ROM's screen is 304 px (152 columns) by 256 rows, and the port's SCREEN is 640x400
    /// real pixels, so a ROM pixel is 640/304 across and 400/256 down (notes §84).
    /// </summary>
    private const float RomPixelToScreenX = 640f / 304f;
    private const float RomPixelToScreenY = 400f / 256f;

    /// <summary>
    /// The port pixel a ROM pixel starts at, and the port row a ROM row starts at.
    ///
    /// Every span below is drawn from its own first pixel to the NEXT pixel's first — so two
    /// adjacent rows (or columns) tile edge to edge and cannot leave a seam. Truncating each
    /// one to a whole pixel is what drew a black line between every band and made the inner
    /// rings read as thin outlines (notes §84/§85).
    /// </summary>
    internal static int PixelX(int romPixel) => (int)MathF.Round(romPixel * RomPixelToScreenX);

    internal static int RowY(int romRow) => (int)MathF.Round(romRow * RomPixelToScreenY);

    private int _left = StartLeftColumn;
    private int _top = StartTopRow;
    private int _right = StartRightColumn;
    private int _bottom = StartBottomRow;
    private int _packed = 0xEF;                  // ROM `LDA #$EF`

    /// <summary>Rings still to draw in this pass (the ROM's counter at `$000E,U`).</summary>
    private int _ringsThisPass = RingsPerPass;

    /// <summary>True once the black pass has reached the middle — the effect is over.</summary>
    public bool Finished { get; private set; }

    /// <summary>True while the tunnel is being redrawn in black to erase itself.</summary>
    internal bool Erasing => _packed == 0;

    /// <summary>The rings drawn so far (test hook) — 53 coloured then 53 black.</summary>
    internal int RingsDrawn { get; private set; }

    /// <summary>Rings still on screen (test hook): nothing is erased until the black pass reaches it.</summary>
    internal int RingsRetained => _rings.Count + _blackRings.Count;

    /// <summary>
    /// The last ring of the colouring pass (test hook) — the walk's outer extent, which must
    /// cover the playfield once the tunnel has finished growing.
    /// </summary>
    internal (int Left, int Top, int Right, int Bottom) OutermostRing
        => _rings.Count > 0
            ? (_rings[^1].Left, _rings[^1].Top, _rings[^1].Right, _rings[^1].Bottom)
            : (_left, _top, _right, _bottom);

    /// <summary>Current ring corners in the ROM's screen coordinates (test hook).</summary>
    internal (int Left, int Top, int Right, int Bottom) Corners => (_left, _top, _right, _bottom);

    /// <summary>The current packed colour pair (test hook).</summary>
    internal int Packed => _packed;

    /// <summary>One ring as drawn: its corners in ROM screen coordinates and the pair it used.</summary>
    private readonly record struct Ring(int Left, int Top, int Right, int Bottom, int Packed);

    /// <summary>
    /// Every ring drawn so far. **The ROM never erases the rings it has passed** — each pass
    /// draws one more, so the screen accumulates concentric rectangles two pixels apart and by
    /// the end the whole area is a filled hatched field. Drawing only the CURRENT ring left a
    /// handful of small rectangles instead (the author's report).
    /// </summary>
    private readonly List<Ring> _rings = new();

    /// <summary>The erase pass's rings, drawn ON TOP of the coloured ones in black.</summary>
    private readonly List<Ring> _blackRings = new();

    /// <summary>
    /// Runs one ROM task pass, which takes two ROM frames (see <see cref="PassFifths"/>). `$5726` resets
    /// the two-ring counter, then the ring is
    /// drawn, the pair advances (unless it is black), the corners step out — and all of that
    /// happens a second time before the task yields.
    /// </summary>
    public void Update()
    {
        if (Finished)
        {
            return;
        }

        // One task pass per PassFifths — the exact-6ths accumulator §52/§65 use for every ROM
        // delay, so the pace is the ROM's unit rather than a rounded frame count.
        _fifths += 5;
        if (_fifths < PassFifths)
        {
            return;
        }

        _fifths -= PassFifths;

        _ringsThisPass = RingsPerPass;
        while (_ringsThisPass > 0 && !Finished)
        {
            _ringsThisPass--;
            AdvanceRing();
        }
    }

    /// <summary>One ring: record it (the renderer draws the accumulated set), then step.</summary>
    private void AdvanceRing()
    {
        // 5731 draws the ring from the CURRENT corners and pair — so record it before stepping.
        (_packed == 0 ? _blackRings : _rings).Add(new Ring(_left, _top, _right, _bottom, _packed));
        RingsDrawn++;

        // 5734: `TSTA / BEQ $5751` — a black pass does not touch the colours.
        if (_packed != 0)
        {
            _packed = NextPair(_packed);
        }

        // 5751/5754: `CMPX #$0616 / BEQ $5764` — the top-left has reached the middle.
        if (_left == EndLeftColumn && _top == EndTopRow)
        {
            if (_packed == 0)
            {
                Finished = true;                  // 5765/576A: black and at the middle — done
                return;
            }

            // 5767: `CLRA / BRA $5710` — start the whole walk again, in black, to erase it.
            _left = StartLeftColumn;
            _top = StartTopRow;
            _right = StartRightColumn;
            _bottom = StartBottomRow;
            _packed = 0;
            return;
        }

        // 5756/575A: `LEAX -258,X` (one column left, two rows up) and `LEAY $0102,Y`.
        _left--;
        _top -= 2;
        _right++;
        _bottom += 2;
    }

    /// <summary>
    /// ROM $5737-$574F: the pair advances by `SUBA #$22` — both nibbles two slots down — with
    /// three special cases that wrap it round again.
    /// </summary>
    internal static int NextPair(int packed) => packed switch
    {
        0x12 => 0xEF,                        // 5737
        0xF1 => 0xDE,                        // 573F
        0x23 => 0xF1,                        // 5747
        _ => (packed - 0x22) & 0xFF,         // 574F
    };

    /// <summary>
    /// Draws EVERY ring the walk has reached — they accumulate, two pixels apart, which is what
    /// fills the area — with the erase pass's black rings painted over the top of them.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        foreach (Ring ring in _rings)
        {
            DrawRing(spriteBatch, sprites, ring);
        }

        foreach (Ring ring in _blackRings)
        {
            DrawRing(spriteBatch, sprites, ring);
        }
    }

    /// <summary>
    /// One ring — ROM `$5A11`: four edges, each ONE ROM pixel thick (the blitter's
    /// `LDA #$05`/`#$05` height/width fields XOR 4 are 1 byte), so successive rows and
    /// columns tile instead of leaving seams (see the class comment).
    /// </summary>
    private void DrawRing(SpriteBatch spriteBatch, SpriteSet sprites, Ring ring)
    {
        (int slot0, int slot1) = Colours(ring.Packed);          // $5A13 / $5A19
        Color colour0 = sprites.SlotColor(slot0);
        Color colour1 = sprites.SlotColor(slot1);

        int top = ring.Top;
        int bottom = ring.Bottom;

        // $5A29: `SUBB $C8 / RORB / BCC / DEC $C9` — if the height is ODD, pull the bottom up
        // by one so the two-row bands at top and bottom sit inside the ring.
        if (((bottom - top) & 1) != 0)
        {
            bottom--;
        }

        // The top edge: row `top` in colour 0 across the whole ring, then row `top+1` in colour 1
        // — but through $5AAA, which moves the blit one column right and makes it one column
        // narrower, so the second line is INSET a column at each end. The bottom edge mirrors it:
        // row `bottom` full width in colour 0, row `bottom-1` inset in colour 1.
        DrawHorizontalLine(spriteBatch, sprites, ring.Left, ring.Right, top, colour0, inset: false);
        DrawHorizontalLine(spriteBatch, sprites, ring.Left, ring.Right, top + 1, colour1, inset: true);
        DrawHorizontalLine(spriteBatch, sprites, ring.Left, ring.Right, bottom, colour0, inset: false);
        DrawHorizontalLine(spriteBatch, sprites, ring.Left, ring.Right, bottom - 1, colour1, inset: true);

        // The vertical edges run from row `top + 1` to row `bottom - 1` — $5A7B's height is
        // `bottom - top - 1` starting at `top + 1`. They are ONE COLUMN wide, which is TWO ROM
        // pixels, and the two colour-combining routines hand the blitter a packed pair whose
        // nibbles are those two pixels' colours: $5AD5 ORs them ($EF for the $EF ring) for the
        // left edge, $5ADA re-packs them the other way round ($FE) for the right one. So the
        // left edge reads colour 0 then colour 1 and the right edge is its MIRROR. Passing one
        // flat colour instead drew solid bars where the arcade has a two-pixel hatch (notes §85).
        DrawVerticalEdge(spriteBatch, sprites, ring.Left, top + 1, bottom - 1, colour0, colour1);
        DrawVerticalEdge(spriteBatch, sprites, ring.Right, top + 1, bottom - 1, colour1, colour0);
    }

    /// <summary>
    /// The palette slots a ring draws in: colour 0 (the left nibble) and colour 1 (the right).
    /// Split out so the range can be asserted — the first cut passed the ROM's
    /// <c>(colour0 &lt;&lt; 4) | colour1</c> straight to the palette, which is a blitter mask
    /// (0-255) and threw on a wave clear.
    /// </summary>
    internal static (int Colour0, int Colour1) Colours(int packed)
        => ((packed >> 4) & 0x0F, packed & 0x0F);

    /// <summary>
    /// One horizontal edge: one ROM row tall, from column <paramref name="left"/> to
    /// <paramref name="right"/> inclusive. <paramref name="inset"/> is the `$5AAA` form — the
    /// same row one column to the right and one column narrower, i.e. a column in from each end.
    /// </summary>
    private void DrawHorizontalLine(
        SpriteBatch spriteBatch, SpriteSet sprites, int left, int right, int row, Color colour, bool inset)
    {
        if (row < 0 || row > 255)
        {
            return;
        }

        int firstColumn = inset ? left + 1 : left;
        int lastColumn = inset ? right - 1 : right;
        if (lastColumn < firstColumn)
        {
            return;
        }

        int x = PixelX(firstColumn * 2);
        int width = PixelX(lastColumn * 2 + 2) - x;
        int y = RowY(row);
        DrawEdge(spriteBatch, sprites, new Rectangle(x, y, width, Math.Max(1, RowY(row + 1) - y)), colour);
    }

    /// <summary>
    /// One vertical edge: two ROM pixels (one column) wide, from row <paramref name="topRow"/>
    /// to <paramref name="bottomRow"/> inclusive. <paramref name="first"/> is the left of those
    /// two pixels, <paramref name="second"/> the right.
    /// </summary>
    private void DrawVerticalEdge(
        SpriteBatch spriteBatch, SpriteSet sprites, int column, int topRow, int bottomRow, Color first, Color second)
    {
        if (column < 0 || column > 151 || bottomRow < topRow)
        {
            return;
        }

        int y = RowY(topRow);
        int height = Math.Max(1, RowY(bottomRow + 1) - y);
        int x0 = PixelX(column * 2);
        int x1 = PixelX(column * 2 + 1);
        int x2 = PixelX(column * 2 + 2);

        DrawEdge(spriteBatch, sprites, new Rectangle(x0, y, Math.Max(1, x1 - x0), height), first);
        DrawEdge(spriteBatch, sprites, new Rectangle(x1, y, Math.Max(1, x2 - x1), height), second);
    }

    /// <summary>Blits one edge rectangle, already in the port's screen pixels.</summary>
    private static void DrawEdge(SpriteBatch spriteBatch, SpriteSet sprites, Rectangle rect, Color colour)
        => spriteBatch.Draw(
            sprites.WallPixel,
            new Rectangle(rect.X, rect.Y, Math.Max(1, rect.Width), Math.Max(1, rect.Height)),
            colour);
}
