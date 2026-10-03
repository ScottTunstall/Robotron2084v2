using Microsoft.Xna.Framework;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>The brain-wave transporter (ROM <c>RRT2.ASM</c>, notes §136).</summary>
public sealed class RobotTransporterTests
{
    private static TransportImage CreateImage(int width, int height, int pick = 0) =>
        new(Enumerable.Repeat(Color.White, width * height).ToArray(), width, height, pick);

    private static void RunToTheEnd(TransportImage image)
    {
        var random = new Random(3);
        while (!image.IsFinished)
        {
            image.Step(random);
        }
    }

    private static IEnumerable<int> AllPixels(TransportImage image) =>
        Enumerable.Range(0, image.Height).SelectMany(y => Enumerable.Range(0, image.Width).Select(x => image.GetPixel(x, y)));

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
        TransportImage image = CreateImage(14, 16);
        Assert.All(AllPixels(image), pixel => Assert.Equal(TransportImage.PixelOff, pixel));

        var random = new Random(1);
        for (int step = 0; step < TransporterTuning.StepCount; step++)
        {
            Assert.False(image.IsFinished);
            image.Step(random);
        }

        Assert.False(image.IsFinished);
        image.Step(random); // the 131st update takes the image off the arcade's list
        Assert.True(image.IsFinished);
    }

    [Fact]
    public void TheFirstStepSparkles_AGroupOfPixelsInPaletteColours_NotTheirOwn()
    {
        TransportImage image = CreateImage(14, 16);

        image.Step(new Random(1));

        int[] shown = [.. AllPixels(image).Where(pixel => pixel != TransportImage.PixelOff)];
        Assert.NotEmpty(shown);
        Assert.All(shown, pixel => Assert.InRange(pixel, 0, 15));
    }

    [Fact]
    public void WhenItIsDone_EveryPixelOfAWholePictureShowsItsOwnColour()
    {
        TransportImage image = CreateImage(14, 16); // 112 bytes: every one is in a group

        RunToTheEnd(image);

        Assert.All(AllPixels(image), pixel => Assert.Equal(TransportImage.PixelOwnColour, pixel));
    }

    [Fact]
    public void APictureOfMoreThan116Bytes_NeverGetsItsLastBytes_AsInTheRom()
    {
        TransportImage image = CreateImage(16, 15); // 120 bytes: the groups stop at byte 115

        RunToTheEnd(image);

        // Byte 116 and up are the last column's bottom rows (byte = column x 15 + row), two pixels to a byte.
        Assert.Equal(TransportImage.PixelOff, image.GetPixel(14, 11));
        Assert.Equal(TransportImage.PixelOff, image.GetPixel(15, 14));
        Assert.Equal(TransportImage.PixelOwnColour, image.GetPixel(0, 0));
        Assert.Equal(TransportImage.PixelOwnColour, image.GetPixel(14, 10));
    }

    [Fact]
    public void AGapInThePicture_IsNeverTurnedOn()
    {
        Color[] template = Enumerable.Repeat(Color.White, 14 * 16).ToArray();
        template[0] = Color.Transparent;
        var image = new TransportImage(template, 14, 16, 0);

        RunToTheEnd(image);

        Assert.Equal(TransportImage.PixelOff, image.GetPixel(0, 0));
        Assert.Equal(TransportImage.PixelOwnColour, image.GetPixel(1, 0));
    }

    [Fact]
    public void RobotsShowingTheSamePictureShareAnImage_SixteenToAnImage()
    {
        object grunt = new();
        object[] pictures = [.. Enumerable.Repeat(grunt, 40)];

        int[] images = RobotTransporter.AssignImages(pictures);

        Assert.Equal(16, images.Count(image => image == 0));
        Assert.Equal(16, images.Count(image => image == 1));
        Assert.Equal(8, images.Count(image => image == 2));
    }

    [Fact]
    public void ANewPictureStartsANewImage_EvenAfterTheFirstIsShownAgain()
    {
        object a = new();
        object b = new();

        Assert.Equal([0, 0, 1, 2, 2], RobotTransporter.AssignImages([a, a, b, a, a]));
    }
}
