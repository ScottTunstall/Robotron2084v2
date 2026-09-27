using Robotron2084.Rendering;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The ROM's colour-cycling palette for the wave-complete tunnel (notes §86): twelve ramp
/// tables that live beside the walker's task data at $576D, fifteen values of one written
/// into palette slots 1-15, one slot blacked in each group of five, and the window slid on
/// one value a pass. This is what makes the arcade's tunnel read as thick bars with dark
/// seams instead of fifteen thin stripes.
/// </summary>
public sealed class TunnelPaletteTests
{
    [Fact]
    public void TheRomShipsTwelveRampsAndTheirStartMasks()
    {
        // $576D: twelve 4-byte descriptors (address, mask, 0) — `$59B9`'s `CMPA #$0C` retries
        // until the random index is under twelve.
        Assert.Equal(12, TunnelPalette.Ramps.Length);
        Assert.Equal(12, TunnelPalette.StartMasks.Length);

        foreach (byte[] ramp in TunnelPalette.Ramps)
        {
            // A ramp runs to its zero terminator ($59E2 skips a zero and wraps), so a ramp the
            // port stores never CONTAINS one, and the fifteen-value window always fits.
            Assert.True(ramp.Length >= TunnelPalette.SlotCount, $"ramp of {ramp.Length} is too short");
            Assert.DoesNotContain((byte)0x00, ramp);
        }

        Assert.Equal([0x1F, 0x3F, 0x3F, 0x0F, 0x0F, 0x0F, 0x0F, 0x1F, 0x1F, 0x0F, 0x1F, 0x1F],
            TunnelPalette.StartMasks);
    }

    [Fact]
    public void FillingThePaletteBlacksExactlyOneSlotInEachGroupOfFive()
    {
        // $59F0-$5A07: `LDB #$00` then `STB A,X` at $9801+A, $9806+A, $980B+A with `LEAX $0005,X`
        // — one black slot per five, which is the dark seam the arcade reference shows every
        // five rows (the walk leaves one palette slot per row).
        var palette = new GamePalette();
        var tunnel = new TunnelPalette(new Random(1));
        tunnel.Start();
        tunnel.Apply(palette);

        for (int group = 0; group < 3; group++)
        {
            int first = TunnelPalette.FirstSlot + group * 5;
            int blacks = 0;
            for (int slot = first; slot < first + 5; slot++)
            {
                if (palette.SlotValue(slot) == 0x00)
                {
                    blacks++;
                }
            }

            Assert.Equal(1, blacks);
        }

        // Slot 0 is not part of the ramp (the ROM starts at $9801) and is left alone.
        Assert.Equal(GamePalette.DefaultSlots[0], palette.SlotValue(0));
    }

    [Fact]
    public void TheLitSlotsAreConsecutiveRampValuesSoTheyReadAsAGradient()
    {
        // $59E0: `LDA ,X+ / STA ,Y+` — fifteen CONSECUTIVE ramp values, in slot order. The walk
        // gives one slot per ROW, so consecutive rows differ by one value of the ramp: that is
        // the smooth gradient inside each bar, and why a bar is not fifteen stripes.
        var palette = new GamePalette();
        var tunnel = new TunnelPalette(new Random(7));
        tunnel.Start();

        byte[] ramp = TunnelPalette.Ramps[tunnel.RampIndex];
        int index = tunnel.Pointer;

        tunnel.Apply(palette);

        for (int slot = 0; slot < TunnelPalette.SlotCount; slot++)
        {
            byte expected = ramp[index % ramp.Length];
            index++;

            if (BlackedSlots(tunnel).Contains(TunnelPalette.FirstSlot + slot))
            {
                continue;
            }

            Assert.Equal(expected, palette.SlotValue(TunnelPalette.FirstSlot + slot));
        }
    }

    /// <summary>The three slots this pass blits black — `$9801+A`, `$9806+A`, `$980B+A`.</summary>
    private static int[] BlackedSlots(TunnelPalette tunnel)
        => TunnelPalette.BlackSlots
            .Select(offset => TunnelPalette.FirstSlot + offset + tunnel.BlackOffset)
            .ToArray();

    [Fact]
    public void EachPassSlidesTheWindowOneValueAndWalksTheBlackSlotDown()
    {
        // $59D2: the stored pointer — the window's first value — goes up ONE a pass, and
        // $59F2's `DECA / CMPA #$05 / BCS` walks the black offset 4,3,2,1,0,4...
        var tunnel = new TunnelPalette(new Random(3));
        tunnel.Start();

        int pointer = tunnel.Pointer;
        Assert.Equal(4, tunnel.BlackOffset);

        tunnel.Advance();
        Assert.Equal(pointer + 1, tunnel.Pointer);
        Assert.Equal(3, tunnel.BlackOffset);

        for (int i = 0; i < 3; i++)
        {
            tunnel.Advance();
        }

        Assert.Equal(0, tunnel.BlackOffset);

        tunnel.Advance();
        Assert.Equal(4, tunnel.BlackOffset);            // 0 wraps back to 4, never to 5
        Assert.Equal(pointer + 5, tunnel.Pointer);
    }

    [Fact]
    public void TheWindowAlwaysFitsInsideTheRamp()
    {
        // The window is fifteen values and wraps at the ramp's close ($59E4), so a pass can never
        // read past the table however long the walk runs.
        var tunnel = new TunnelPalette(new Random(5));
        tunnel.Start();

        byte[] ramp = TunnelPalette.Ramps[tunnel.RampIndex];

        for (int pass = 0; pass < 1000; pass++)
        {
            Assert.InRange(tunnel.Pointer, 0, ramp.Length - 1);
            tunnel.Advance();
        }
    }
}
