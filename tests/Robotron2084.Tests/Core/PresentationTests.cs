using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The window fit: the canvas is drawn at ONE scale for both axes, centred with black bars, at any
/// client size — including one smaller than the canvas — and the two fit modes differ only in how
/// the scale is chosen. These are invariants, not 2x numbers, so they hold at any SpecScale.
/// </summary>
public sealed class PresentationTests
{
    /// <summary>Client areas to check: the author's screen, the classics, cramped, tiny and empty.</summary>
    public static TheoryData<int, int> ClientAreas => new()
    {
        { 1920, 1200 },
        { 1920, 1080 },
        { 2560, 1440 },
        { 1366, 768 },
        { 1024, 768 },
        { 800, 600 },
        { 640, 400 },
        { 400, 640 },
        { 100, 100 },
        { 1, 1 },
        { 0, 0 },
        { -5, 7 },
    };

    private static bool Fits(int scale, int width, int height) =>
        ScreenSize.Width * scale <= width && ScreenSize.Height * scale <= height;

    [Theory]
    [MemberData(nameof(ClientAreas))]
    public void IntegerModeIsAWholeMultipleAndTheLargestOneThatFits(int width, int height)
    {
        float scale = Presentation.ScaleFor(width, height, ScaleMode.Integer);

        Assert.True(scale >= 1f, "the canvas is never scaled below 1x");
        Assert.Equal(MathF.Floor(scale), scale);
        Assert.False(Fits((int)scale + 1, width, height),
            $"scale {(int)scale + 1} would also have fitted {width}x{height}");
    }

    [Theory]
    [MemberData(nameof(ClientAreas))]
    public void FillModeUsesTheWholeOfTheLimitingAxis(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return; // the empty client area is covered by the centring test
        }

        Rectangle canvas = Presentation.CanvasDestination(width, height, ScaleMode.Fill);
        bool widthLimited = (long)width * ScreenSize.Height <= (long)height * ScreenSize.Width;

        // Rounded to whole pixels, so the filled axis can fall one short.
        if (widthLimited)
        {
            Assert.InRange(canvas.Width, width - 1, width);
        }
        else
        {
            Assert.InRange(canvas.Height, height - 1, height);
        }
    }

    [Theory]
    [MemberData(nameof(ClientAreas))]
    public void CanvasIsDrawableCentredAndNeverStretched(int width, int height)
    {
        foreach (ScaleMode mode in new[] { ScaleMode.Integer, ScaleMode.Fill })
        {
            Rectangle canvas = Presentation.CanvasDestination(width, height, mode);

            Assert.True(canvas.Width >= 1 && canvas.Height >= 1, "a canvas is always drawable");

            int leftBar = canvas.X;
            int rightBar = width - (canvas.X + canvas.Width);
            int topBar = canvas.Y;
            int bottomBar = height - (canvas.Y + canvas.Height);
            Assert.True(Math.Abs(leftBar - rightBar) <= 1, $"{width}x{height} {mode}: side bars differ ({leftBar}/{rightBar})");
            Assert.True(Math.Abs(topBar - bottomBar) <= 1, $"{width}x{height} {mode}: top/bottom bars differ ({topBar}/{bottomBar})");

            // The drawn shape IS the canvas's shape, within a pixel of rounding: one scale, both axes.
            long shapeError = Math.Abs((long)canvas.Width * ScreenSize.Height - (long)canvas.Height * ScreenSize.Width);
            Assert.True(shapeError <= ScreenSize.Width * ScreenSize.Height / 100,
                $"{width}x{height} {mode}: the canvas was stretched ({canvas.Width}x{canvas.Height})");
        }
    }

    [Fact]
    public void IntegerModeLetterboxesWhereFillModeWouldFitExactly()
    {
        // 1440x900 has the canvas's own 8:5 shape, so the exact fraction fills it at EVERY scale
        // while the whole multiple leaves bars unless that shape happens to be a whole multiple too.
        Rectangle fill = Presentation.CanvasDestination(1440, 900, ScaleMode.Fill);
        Assert.Equal(new Rectangle(0, 0, 1440, 900), fill);

        int whole = ScreenSize.MaxIntegerScale(1440, 900);
        Rectangle integer = Presentation.CanvasDestination(1440, 900, ScaleMode.Integer);
        Assert.Equal(whole * ScreenSize.Width, integer.Width);
        Assert.Equal(whole * ScreenSize.Height, integer.Height);

        // 1280x720 does NOT share the canvas's shape: the whole multiple may then overflow the
        // client area (which crops the canvas) while the exact fraction always fits inside it.
        Rectangle cropped = Presentation.CanvasDestination(1280, 720, ScaleMode.Integer);
        Assert.True(cropped.Width >= ScreenSize.Width && cropped.Height >= ScreenSize.Height);

        Rectangle squashedFill = Presentation.CanvasDestination(1280, 720, ScaleMode.Fill);
        Assert.InRange(squashedFill.Width, 1, 1280);
        Assert.InRange(squashedFill.Height, 1, 720);
    }

    [Fact]
    public void BothModesAgreeWhenTheScreenIsAWholeMultiple()
    {
        // Three canvases exactly: the whole multiple and the exact fraction are the same fit.
        int width = ScreenSize.Width * 3;
        int height = ScreenSize.Height * 3;

        Assert.Equal(3f, Presentation.ScaleFor(width, height, ScaleMode.Integer));
        Assert.Equal(
            Presentation.CanvasDestination(width, height, ScaleMode.Integer),
            Presentation.CanvasDestination(width, height, ScaleMode.Fill));
    }

    [Fact]
    public void TheScaleModeCycles() =>
        Assert.Equal(ScaleMode.Fill, Presentation.Next(ScaleMode.Integer));

    [Fact]
    public void TheScaleModeCyclesBack() =>
        Assert.Equal(ScaleMode.Integer, Presentation.Next(ScaleMode.Fill));
}
