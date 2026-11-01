using Robotron2084.Graphics;
using Xunit;

namespace Robotron2084.Tests.Graphics;

/// <summary>The walk of the Williams "W"s round the attract page's border (ROM <c>$896C</c>).</summary>
public sealed class WilliamsLogoPathTests
{
    private static List<(int Column, int Row, int PaletteIndex)> Walk(int count)
    {
        var williamsLogoPath = new WilliamsLogoPath();
        var places = new List<(int Column, int Row, int PaletteIndex)>();
        for (var i = 0; i < count; i++)
        {
            places.Add((williamsLogoPath.Column, williamsLogoPath.Row, PaletteIndex: williamsLogoPath.GetPaletteIndex()));
            williamsLogoPath.Step();
        }

        return places;
    }

    [Fact]
    public void TheFirstLapIsTwentyEightWsRoundTheEdge()
    {
        var places = Walk(29);

        // Along the top from the left corner, then the top-right corner.
        Assert.Equal([5, 21, 37, 53, 69, 85, 101, 117, 133], places.Take(9).Select(p => p.Column));
        Assert.All(places.Take(9), p => Assert.Equal(0x0F, p.Row));

        // Down the right edge to the bottom-right corner.
        Assert.Equal([47, 79, 111, 143, 175, 207], places.Skip(9).Take(6).Select(p => p.Row));
        Assert.All(places.Skip(9).Take(6), p => Assert.Equal(0x85, p.Column));

        // Along the bottom to the bottom-left corner, then up the left edge.
        Assert.Equal([117, 101, 85, 69, 53, 37, 21, 5], places.Skip(15).Take(8).Select(p => p.Column));
        Assert.Equal([175, 143, 111, 79, 47], places.Skip(23).Take(5).Select(p => p.Row));
        Assert.All(places.Skip(23).Take(5), p => Assert.Equal(5, p.Column));
    }

    [Fact]
    public void TheSecondLapStartsTwoBytesFurtherIn()
    {
        var places = Walk(29);

        Assert.Equal((7, 0x0F), (places[28].Column, places[28].Row));
    }

    [Fact]
    public void TheColoursStepDownThroughSevenPaletteIndicesAndRoundAgain()
    {
        var places = Walk(9);

        Assert.Equal([7, 6, 5, 4, 3, 2, 1, 7, 6], places.Select(p => p.PaletteIndex));
    }

    [Fact]
    public void ResetGoesBackToTheFirstW()
    {
        var williamsLogoPath = new WilliamsLogoPath();
        for (var i = 0; i < 40; i++) williamsLogoPath.Step();

        williamsLogoPath.Reset();

        Assert.Equal((5, 0x0F, 7), (williamsLogoPath.Column, williamsLogoPath.Row, williamsLogoPath.GetPaletteIndex()));
    }
}
