using Microsoft.Xna.Framework;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>The brain-wave transporter (ROM <c>RRT2.ASM</c>, notes §136).</summary>
public sealed class RobotTransporterTests
{
    private static TransportImage CreateImage(int width, int height, int pick = 0)
    {
        return new TransportImage(Enumerable.Repeat(Color.White, width * height).ToArray(), width, height, pick);
    }

    private static void RunToTheEnd(TransportImage transportImage)
    {
        var random = new Random(3);
        while (!transportImage.IsFinished()) transportImage.Step(random);
    }

    private static IEnumerable<int> AllPixels(TransportImage transportImage)
    {
        return Enumerable.Range(0, transportImage.Height)
            .SelectMany(y => Enumerable.Range(0, transportImage.Width).Select(x => transportImage.GetPixel(x, y)));
    }

    [Fact]
    public void TheTablesAreTheRomsOwn_EightGroupsOfTwentyNinePixelsAndOneHundredAndThirtySteps()
    {
        Assert.Equal(130, TransporterTuning.Steps.Length);
        Assert.Equal(TransporterTuning.StepCount, TransporterTuning.Steps.Length);
        Assert.Equal(8, TransporterTuning.Groups.Length);
        Assert.All(TransporterTuning.Groups, group => Assert.Equal(29, group.Length));
        Assert.Equal(140, TransporterTuning.SparkleColours.Length);
    }

    [Fact]
    public void AnImageStartsEmpty_AndIsFinishedOnTheStepAfterTheLastOne()
    {
        var transportImage = CreateImage(14, 16);
        Assert.All(AllPixels(transportImage), pixel => Assert.Equal(TransportImage.PixelOff, pixel));

        var random = new Random(1);
        for (var step = 0; step < TransporterTuning.StepCount; step++)
        {
            Assert.False(transportImage.IsFinished());
            transportImage.Step(random);
        }

        Assert.False(transportImage.IsFinished());
        transportImage.Step(random); // the 131st update takes the image off the arcade's list
        Assert.True(transportImage.IsFinished());
    }

    [Fact]
    public void TheFirstStepSparkles_AGroupOfPixelsInPaletteColours_NotTheirOwn()
    {
        var transportImage = CreateImage(14, 16);

        transportImage.Step(new Random(1));

        int[] shown = [.. AllPixels(transportImage).Where(pixel => pixel != TransportImage.PixelOff)];
        Assert.NotEmpty(shown);
        Assert.All(shown, pixel => Assert.InRange(pixel, 0, 15));
    }

    [Fact]
    public void WhenItIsDone_EveryPixelOfAWholePictureShowsItsOwnColour()
    {
        var transportImage = CreateImage(14, 16); // 112 bytes: every one is in a group

        RunToTheEnd(transportImage);

        Assert.All(AllPixels(transportImage), pixel => Assert.Equal(TransportImage.PixelOwnColour, pixel));
    }

    [Fact]
    public void APictureOfMoreThan116Bytes_NeverGetsItsLastBytes_AsInTheRom()
    {
        var transportImage = CreateImage(16, 15); // 120 bytes: the groups stop at byte 115

        RunToTheEnd(transportImage);

        // Byte 116 and up are the last column's bottom rows (byte = column x 15 + row), two pixels to a byte.
        Assert.Equal(TransportImage.PixelOff, transportImage.GetPixel(14, 11));
        Assert.Equal(TransportImage.PixelOff, transportImage.GetPixel(15, 14));
        Assert.Equal(TransportImage.PixelOwnColour, transportImage.GetPixel(0, 0));
        Assert.Equal(TransportImage.PixelOwnColour, transportImage.GetPixel(14, 10));
    }

    [Fact]
    public void AGapInThePicture_IsNeverTurnedOn()
    {
        var template = Enumerable.Repeat(Color.White, 14 * 16).ToArray();
        template[0] = Color.Transparent;
        var transportImage = new TransportImage(template, 14, 16, 0);

        RunToTheEnd(transportImage);

        Assert.Equal(TransportImage.PixelOff, transportImage.GetPixel(0, 0));
        Assert.Equal(TransportImage.PixelOwnColour, transportImage.GetPixel(1, 0));
    }

    [Fact]
    public void RobotsShowingTheSamePictureShareAnImage_SixteenToAnImage()
    {
        object grunt = new();
        object[] pictures = [.. Enumerable.Repeat(grunt, 40)];

        var images = RobotTransporter.AssignImages(pictures);

        Assert.Equal(16, images.Count(transportImage => transportImage == 0));
        Assert.Equal(16, images.Count(transportImage => transportImage == 1));
        Assert.Equal(8, images.Count(transportImage => transportImage == 2));
    }

    [Fact]
    public void ANewPictureStartsANewImage_EvenAfterTheFirstIsShownAgain()
    {
        object a = new();
        object b = new();

        Assert.Equal([0, 0, 1, 2, 2], RobotTransporter.AssignImages([a, a, b, a, a]));
    }
}
