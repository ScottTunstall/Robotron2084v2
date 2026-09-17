using Robotron2084.Core;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// Resolution invariants. These must hold at ANY SpecScale, so the suite
/// stays green when the render scale is raised (ScreenSize is the single
/// source of truth). The 320x200 spec space IS pinned — that is the game's
/// layout per spec.txt. SpecScale is deliberately NOT pinned: it is the knob
/// a resolution increase turns.
/// </summary>
public sealed class ScreenSizeTests
{
    [Fact]
    public void SpecSpace_IsTheGameLayout_320x200()
    {
        Assert.Equal(320, ScreenSize.SpecWidth);
        Assert.Equal(200, ScreenSize.SpecHeight);
    }

    [Fact]
    public void InternalResolution_DerivesFromSpecSpace()
    {
        Assert.Equal(ScreenSize.SpecWidth * ScreenSize.SpecScale, ScreenSize.Width);
        Assert.Equal(ScreenSize.SpecHeight * ScreenSize.SpecScale, ScreenSize.Height);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    public void Scaled_MultipliesSpecPixelsBySpecScale(int specPixels) =>
        Assert.Equal(specPixels * ScreenSize.SpecScale, ScreenSize.Scaled(specPixels));

    [Fact]
    public void MaxIntegerScale_FitsThreeTimesThePlayfield_At3x() =>
        Assert.Equal(3, ScreenSize.MaxIntegerScale(ScreenSize.Width * 3, ScreenSize.Height * 3));

    [Fact]
    public void MaxIntegerScale_WidthLimitsWhenHeightHasRoom() =>
        Assert.Equal(1, ScreenSize.MaxIntegerScale(ScreenSize.Width, ScreenSize.Height * 2));

    [Fact]
    public void MaxIntegerScale_HeightLimitsWhenWidthHasRoom() =>
        Assert.Equal(1, ScreenSize.MaxIntegerScale(ScreenSize.Width * 2, ScreenSize.Height));

    [Theory]
    [InlineData(0, 100)]   // degenerate width -> 1x
    [InlineData(100, 0)]   // degenerate height -> 1x
    [InlineData(1, 1)]     // tiny display -> 1x
    public void MaxIntegerScale_NeverDownscales(int width, int height) =>
        Assert.Equal(1, ScreenSize.MaxIntegerScale(width, height));
}
