using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Hud;
using Robotron2084.Level;
using Robotron2084.Palette;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>
///     The playfield's geometry: the inner rectangle is the canvas inset by the playfield margin, the wall
///     that encloses it is on screen, and the HUD row sits above the wall. All three went wrong when the
///     margin field was moved below the rectangle it sizes: a static field read before its own initialiser
///     has run is still its default, so the margin was zero, the wall and the HUD were drawn off canvas and
///     the player could walk to the screen's edge.
/// </summary>
public sealed class PlayfieldLayoutTests
{
    private static readonly Rectangle Canvas = new(0, 0, ScreenSize.Width, ScreenSize.Height);

    private static int Margin => ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.PlayfieldMarginArcadePixels);

    [Fact]
    public void InnerBounds_IsTheCanvasInsetByThePlayfieldMargin()
    {
        Rectangle expected = new(Margin, Margin, ScreenSize.Width - 2 * Margin, ScreenSize.Height - 2 * Margin);

        Assert.Equal(expected, PlayfieldLayout.GetInnerBounds());
    }

    [Fact]
    public void ScoresAndMenRow_IsOnScreen()
    {
        var row = ArcadeHud.GetScoresAndLivesY(PlayfieldLayout.GetInnerBounds());

        Assert.True(row >= 0, $"the score and spare-lives row ({row}) must be on the canvas");
    }

    [Fact]
    public void Wall_IsOnScreen()
    {
        var playfieldWall = new PlayfieldWall(PlayfieldLayout.GetInnerBounds(), new WallColourCycle());

        Assert.True(Canvas.Contains(playfieldWall.GetOuterBounds()),
            $"the wall {playfieldWall.GetOuterBounds()} must lie inside the {Canvas} canvas");
    }
}
