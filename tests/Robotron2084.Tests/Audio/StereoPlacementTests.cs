using Microsoft.Xna.Framework;
using Robotron2084.Audio;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Audio;

public class StereoPlacementTests
{
    private static readonly Rectangle Playfield = new(40, 20, 560, 360);

    [Fact]
    public void SomethingInTheMiddle_IsHeardInTheMiddle()
    {
        var maker = new Rectangle(310, 200, 20, 20);

        Assert.Equal(0f, StereoPlacement.GetPan(maker, Playfield));
    }

    [Fact]
    public void SomethingAtTheLeftEdge_IsHeardOnTheLeft_AsFarAsTheStereoWidthAllows()
    {
        var maker = new Rectangle(30, 200, 20, 20);

        Assert.Equal(-SoundTuning.StereoWidth, StereoPlacement.GetPan(maker, Playfield));
    }

    [Fact]
    public void SomethingPastTheRightEdge_IsHeldAtTheRight()
    {
        var maker = new Rectangle(900, 200, 20, 20);

        Assert.Equal(SoundTuning.StereoWidth, StereoPlacement.GetPan(maker, Playfield));
    }

    [Fact]
    public void SomethingHalfwayToTheRight_IsHeardHalfwayToTheRight()
    {
        var maker = new Rectangle(450, 200, 20, 20);

        Assert.Equal(0.5f * SoundTuning.StereoWidth, StereoPlacement.GetPan(maker, Playfield), 3);
    }
}
