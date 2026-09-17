using Robotron2084.Rendering;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The ROM's wave-complete tunnel (`DRAW_COLOUR_CYCLING_TUNNEL_EFFECT` $5703 and
/// `DRAW_RECTANGULAR_PART_OF_TUNNEL` $5A11 — notes §78.2, §79): it EXPANDS from a thin
/// line at the screen's centre to its corners, two rings a frame, cycling a packed
/// colour pair once per ring, then walks the whole thing again in black to clear it.
/// </summary>
public sealed class TunnelEffectTests
{
    [Fact]
    public void TheTunnelStartsAsACentreLineWithTheFirstColourPair()
    {
        // $570E: `LDA #$EF`, $5710: `LDX #$3B80`, $5713: `LDY #$5A82`.
        var tunnel = new TunnelEffect();

        Assert.Equal((0x3B, 0x80, 0x5A, 0x82), tunnel.Corners);
        Assert.Equal(0xEF, tunnel.Packed);
        Assert.False(tunnel.Finished);
    }

    /// <summary>Ticks the effect the way the game does — one call a tick — for <paramref name="ticks"/> ticks.</summary>
    private static void Ticks(TunnelEffect tunnel, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            tunnel.Update();
        }
    }

    /// <summary>Port ticks for one task pass on the exact-6ths clock (notes §83).</summary>
    private static int TicksPerPass => (TunnelEffect.PassFifths + 4) / 5;

    [Fact]
    public void EachPassDrawsTwoRingsAndStepsTheCornersOut()
    {
        // $572D seeds the counter with 2, and $5756/$575A step the top-left one COLUMN left
        // and TWO ROWS up, the bottom-right one column right and two rows down.
        var tunnel = new TunnelEffect();

        Ticks(tunnel, TicksPerPass); // exactly one pass

        Assert.Equal(TunnelEffect.RingsPerPass, tunnel.RingsDrawn);
        Assert.Equal((0x39, 0x7C, 0x5C, 0x86), tunnel.Corners);
        Assert.Equal(0xAB, tunnel.Packed); // $EF -> $CD -> $AB, one step per ring
    }

    [Fact]
    public void TheWholeEffectLastsAboutTwoSeconds()
    {
        // The author's playtest of the arcade: "~2 seconds at least". 54 passes over the two
        // phases at PassFifths a pass must land near two seconds of port ticks (60 Hz), i.e.
        // around 130 — this is the number the playtest fixed (notes §83).
        var tunnel = new TunnelEffect();

        int ticks = 0;
        while (!tunnel.Finished && ticks < 1000)
        {
            tunnel.Update();
            ticks++;
        }

        Assert.True(tunnel.Finished);
        Assert.InRange(ticks, 120, 140);                           // ~2.0-2.3 s
        Assert.InRange(ticks / 60.0, 2.0, 2.4);
    }

    [Fact]
    public void TheColourPairFollowsTheRomsChain()
    {
        // $5737: $12 -> $EF, $573F: $F1 -> $DE, $5747: $23 -> $F1, else $574F: `SUBA #$22`.
        Assert.Equal(0xEF, TunnelEffect.NextPair(0x12));
        Assert.Equal(0xDE, TunnelEffect.NextPair(0xF1));
        Assert.Equal(0xF1, TunnelEffect.NextPair(0x23));

        Assert.Equal(0xCD, TunnelEffect.NextPair(0xEF));
        Assert.Equal(0xAB, TunnelEffect.NextPair(0xCD));

        // The chain only ever visits the pairs the ROM lists, wrapping rather than running
        // into a negative nibble.
        int pair = 0xEF;
        for (int i = 0; i < 64; i++)
        {
            pair = TunnelEffect.NextPair(pair);
            Assert.InRange(pair, 0x00, 0xFF);
        }
    }

    [Fact]
    public void TheTunnelFinishesAfterTheBlackPass()
    {
        // 53 rings of steps to reach $0616 (column 6, row 22), plus the ring drawn at the
        // start and at the middle: 54 rings a phase, so 108 draws over 54 frames.
        var tunnel = new TunnelEffect();

        int guard = 0;
        while (!tunnel.Finished && guard++ < 500)
        {
            tunnel.Update();
        }

        Assert.True(tunnel.Finished, "the tunnel must finish");
        Assert.Equal(108, tunnel.RingsDrawn);
    }

    [Fact]
    public void EveryColourTheRingDrawsWithIsAValidPaletteSlot()
    {
        // The first cut passed the ROM's `(colour0 << 4) | colour1` — a BLITTER PLANE MASK,
        // 0-255 — straight to the palette, so a wave clear threw on slot 239 and the game
        // ended. Assert the whole chain stays inside 0-15.
        int pair = 0xEF;
        for (int i = 0; i < 200; i++)
        {
            (int colour0, int colour1) = TunnelEffect.Colours(pair);
            Assert.InRange(colour0, 0, 15);
            Assert.InRange(colour1, 0, 15);

            pair = TunnelEffect.NextPair(pair);
            if (pair == 0)
            {
                break; // the erase pass draws in black
            }
        }
    }

    [Fact]
    public void EveryRomRowAndPixelGetsItsOwnWholePortPixelSoNothingLeavesASeam()
    {
        // The ring's edges are ONE ROM pixel thick and the walk lays its bands two rows apart, so
        // the rows have to TILE: each row's span must run to the next row's first pixel. Rounding
        // each row down to a whole pixel instead left a black line between every band and made
        // the inner rings read as thin outlines (notes §84/§85).
        for (int row = 0; row < 256; row++)
        {
            Assert.True(TunnelEffect.RowY(row + 1) > TunnelEffect.RowY(row),
                $"row {row} must be at least one port pixel tall");
        }

        for (int pixel = 0; pixel < 304; pixel++)
        {
            Assert.True(TunnelEffect.PixelX(pixel + 1) > TunnelEffect.PixelX(pixel),
                $"ROM pixel {pixel} must be at least one port pixel wide");
        }

        // The ROM's 256 rows fill the port's whole 400-pixel screen height, and no more.
        Assert.Equal(0, TunnelEffect.RowY(0));
        Assert.Equal(400, TunnelEffect.RowY(256));
    }

    [Fact]
    public void EveryRingStaysOnScreenSoTheAreaFillsUp()
    {
        // The ROM never erases the rings it has passed — each pass leaves one more behind, two
        // pixels apart, and by the end the walk has covered the whole area. Drawing only the
        // CURRENT ring left a handful of small rectangles instead (the author's report).
        var tunnel = new TunnelEffect();

        Ticks(tunnel, TicksPerPass);       // two rings
        Ticks(tunnel, TicksPerPass);       // four

        Assert.Equal(4, tunnel.RingsDrawn);
        Assert.Equal(4, tunnel.RingsRetained);          // nothing has been discarded

        // The rings keep growing until the outward walk covers the screen: the last ring of the
        // colouring pass starts at $0616 (column 6, row 22) and ends on the screen's own edges.
        int guard = 0;
        while (!tunnel.Erasing && guard++ < 1000)
        {
            tunnel.Update();
        }

        Assert.Equal(54, tunnel.RingsRetained);          // every coloured ring is still there
        (int left, int top, int right, int bottom) = tunnel.OutermostRing;

        Assert.Equal(0x06, left);
        Assert.Equal(0x16, top);
        Assert.Equal(0x8F, right);                       // column 143 — the right edge
        Assert.Equal(0xEC, bottom);                      // row 236 — the bottom edge
    }

    [Fact]
    public void TheBlackPassEasesInAndKeepsTheColourAtBlack()
    {
        // $5767: `CLRA / BRA $5710` — the erase pass restarts at the outer corners with the
        // colour forced to 0, and $5734's `TSTA / BEQ` leaves it there.
        var tunnel = new TunnelEffect();

        int guard = 0;
        while (!tunnel.Erasing && guard++ < 500)
        {
            tunnel.Update();
        }

        Assert.True(tunnel.Erasing);
        Assert.Equal(0, tunnel.Packed);
        Assert.Equal((0x3B, 0x80, 0x5A, 0x82), tunnel.Corners); // back to the outermost ring

        tunnel.Update();
        Assert.Equal(0, tunnel.Packed); // still black — the pair never advances again
    }
}
