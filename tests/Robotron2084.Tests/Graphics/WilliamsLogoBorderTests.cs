using Robotron2084.Core;
using Robotron2084.Graphics;
using Xunit;

namespace Robotron2084.Tests.Rendering;

/// <summary>The attract page's border of Williams "W"s (ROM <c>$87D6</c> onward).</summary>
public sealed class WilliamsLogoBorderTests
{
    /// <summary>A one-pixel stand-in for the W, so a test can find exactly where it was drawn.</summary>
    private static WilliamsLogoBorder NewBorder() => new(SpriteMask.CreateFromPixels(28, 27, [(0, 0)]));

    private static void TickRomFrames(WilliamsLogoBorder border, int romFrames)
    {
        // A ROM frame is six clock units and a port tick five, so N frames take 6N/5 ticks, rounded up.
        int ticks = (ArcadeClock.ToClockUnits(romFrames) + ArcadeClock.UnitsPerPortTick - 1) / ArcadeClock.UnitsPerPortTick;
        for (int i = 0; i < ticks; i++)
        {
            border.Tick();
        }
    }

    private static int Count(WilliamsLogoBorder border) => border.Pixels.Count(pixel => pixel != 0);

    private static byte PixelAt(WilliamsLogoBorder border, int column, int row) =>
        border.Pixels[(row * WilliamsLogoBorder.Width) + (column * ScreenSize.ArcadePixelsPerColumn)];

    [Fact]
    public void TheFirstWIsDrawnAtOnceInSlotSeven()
    {
        WilliamsLogoBorder border = NewBorder();

        TickRomFrames(border, 1);

        Assert.Equal(7, PixelAt(border, 5, 0x0F));
        Assert.Equal(1, Count(border));
    }

    [Fact]
    public void AnotherWComesEveryFourRomFrames()
    {
        WilliamsLogoBorder border = NewBorder();

        TickRomFrames(border, 12);

        Assert.Equal(3, Count(border));
        Assert.Equal(6, PixelAt(border, 21, 0x0F));
        Assert.Equal(5, PixelAt(border, 37, 0x0F));
    }

    [Fact]
    public void TheRingHasTwentyEightWsBeforeItStartsToMove()
    {
        WilliamsLogoBorder border = NewBorder();

        TickRomFrames(border, (4 * 27) + 1); // the 28th W is drawn on the 109th ROM frame

        Assert.Equal(28, Count(border));
    }

    [Fact]
    public void ThenSixWsMoveEveryRomFrame()
    {
        WilliamsLogoBorder border = NewBorder();
        TickRomFrames(border, (4 * 28) + 1); // the moving phase begins four frames after the 28th W

        int before = Count(border);
        TickRomFrames(border, 1);

        // Each move erases its slot's W (nothing there for the first lap) and draws one, so the ring grows.
        Assert.True(Count(border) >= before);
        Assert.False(border.IsFinished);
    }

    [Fact]
    public void ItFinishesAfterSevenHundredAndFourMovingFrames()
    {
        WilliamsLogoBorder border = NewBorder();

        TickRomFrames(border, (4 * 28) + 704 + 8);

        Assert.True(border.IsFinished);
    }
}
