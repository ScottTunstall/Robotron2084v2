using Microsoft.Xna.Framework;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>One animation frame being beamed in on a brain wave. Its pixels come on in groups, sparkling, and go off again, until the whole animation frame is there.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRT2.ASM</c> <c>TRNSPT</c> (the three ways a step changes the pixels), <c>TMAKE</c> and <c>UPDATE</c></item>
/// <item>Disassembly: the transporter overlay's update routine (<c>RTORG</c>, <c>$4140</c>)</item>
/// </list>
/// "Transport" is the arcade's own name for beaming the robots in at the start of a brain wave.
/// The arcade keeps one of these in a spare buffer for all the robots that show the same animation frame, and draws it for each of them every ROM frame.
/// That is why it is a class of its own rather than part of a robot. A step takes the next entry of <see cref="TransporterTuning.Steps"/>, and turns one
/// group of pixels on, on with a sparkle, or off. The arcade stores two pixels to a byte, a column at a time, so a group names pixels by byte, and this class
/// maps them back to a pixel's place. Where a plain "on" meets a pixel that is still sparkling, the arcade mixes the two colour numbers;
/// here the pixel simply takes its own colour.
/// </remarks>
public sealed class TransportImage
{
    /// <summary>The value stored in <see cref="_pixels"/> for a pixel that is not showing. <see cref="GetPixel"/> returns it for such a pixel.</summary>
    public const int PixelOff = -1;

    /// <summary>The value stored in <see cref="_pixels"/> for a pixel that is showing its own colour. <see cref="GetPixel"/> returns it for such a pixel.</summary>
    public const int PixelOwnColour = -2;

    /// <summary>What each pixel is showing, a row at a time: <see cref="PixelOff"/>, <see cref="PixelOwnColour"/>, or a sparkle's palette number.</summary>
    private readonly int[] _pixels;

    /// <summary>Whether each pixel of the animation frame has a colour, and so can be turned on.</summary>
    private readonly bool[] _drawnInTemplate;

    /// <summary>Where in the sparkle colours this image's own set of them starts.</summary>
    private readonly int _sparkleSetStart;

    /// <summary>The place in the sequence of the next step.</summary>
    private int _nextStep;

    /// <summary>Starts an empty image of an animation frame.</summary>
    /// <param name="template">The animation frame's pixels, a row at a time. A pixel with no colour is a gap.</param>
    /// <param name="width">The animation frame's width in pixels. It must be even, as the arcade stores two pixels to a byte.</param>
    /// <param name="height">The animation frame's height in rows.</param>
    /// <param name="sparkleSetPick">Which of the eight sparkle picks this image uses, from 0 to 7.</param>
    public TransportImage(Color[] template, int width, int height, int sparkleSetPick)
    {
        Template = template;
        Width = width;
        Height = height;
        _pixels = new int[width * height];
        Array.Fill(_pixels, PixelOff);
        _drawnInTemplate = [.. template.Select(pixel => pixel.A > 0)];
        _sparkleSetStart = TransporterTuning.SparkleSetByPick[sparkleSetPick] * TransporterTuning.SparkleColoursPerSet;
    }

    /// <summary>The animation frame's own pixels.</summary>
    public Color[] Template { get; }

    /// <summary>The animation frame's width in pixels.</summary>
    public int Width { get; }

    /// <summary>The animation frame's height in rows.</summary>
    public int Height { get; }

    /// <summary>Says whether the sequence has run out: nothing more will change.</summary>
    public bool IsFinished() => _nextStep > TransporterTuning.StepCount;

    /// <summary>Gets what a pixel is showing.</summary>
    /// <param name="x">The pixel's column.</param>
    /// <param name="y">The pixel's row.</param>
    /// <returns><see cref="PixelOff"/>, <see cref="PixelOwnColour"/>, or a palette number from 0 to 15 for a sparkle.</returns>
    public int GetPixel(int x, int y) => _pixels[(y * Width) + x];

    /// <summary>Does one step of the sequence, which the arcade does once a frame.</summary>
    /// <param name="random">Where the sparkle's starting point comes from.</param>
    public void Step(Random random)
    {
        if (IsFinished())
        {
            return;
        }

        if (_nextStep == TransporterTuning.StepCount)
        {
            _nextStep++;
            return;
        }

        int step = TransporterTuning.Steps[_nextStep++];
        (int ByteIndex, int Half)[] group = TransporterTuning.Groups[(step & TransporterTuning.GroupMask) - 1];
        if ((step & TransporterTuning.OffBit) != 0)
        {
            TurnOff(group);
        }
        else if ((step & TransporterTuning.SparkleBit) != 0)
        {
            TurnOnSparkling(group, random);
        }
        else
        {
            TurnOnOwnColour(group);
        }
    }

    /// <summary>Turns a group of pixels off.</summary>
    /// <param name="group">The pixels, each named by its byte and which half of the byte it is.</param>
    private void TurnOff((int ByteIndex, int Half)[] group)
    {
        foreach ((int byteIndex, int half) in group)
        {
            if (!TryGetPixelIndex(byteIndex, half, out int pixel))
            {
                return;
            }

            _pixels[pixel] = PixelOff;
        }
    }

    /// <summary>Turns a group of pixels on, each in its own colour.</summary>
    /// <param name="group">The pixels, each named by its byte and which half of the byte it is.</param>
    private void TurnOnOwnColour((int ByteIndex, int Half)[] group)
    {
        foreach ((int byteIndex, int half) in group)
        {
            if (!TryGetPixelIndex(byteIndex, half, out int pixel))
            {
                return;
            }

            if (_drawnInTemplate[pixel])
            {
                _pixels[pixel] = PixelOwnColour;
            }
        }
    }

    /// <summary>Turns a group of pixels on, each sparkling in a colour from this image's set.</summary>
    /// <param name="group">The pixels, each named by its byte and which half of the byte it is.</param>
    /// <param name="random">Where the sparkle's starting point comes from.</param>
    private void TurnOnSparkling((int ByteIndex, int Half)[] group, Random random)
    {
        int sparkle = _sparkleSetStart + random.Next(8);
        foreach ((int byteIndex, int half) in group)
        {
            if (!TryGetPixelIndex(byteIndex, half, out int pixel))
            {
                return;
            }

            if (!_drawnInTemplate[pixel])
            {
                continue;
            }

            byte colourPair = TransporterTuning.SparkleColours[sparkle++ % TransporterTuning.SparkleColours.Length];
            _pixels[pixel] = half == 0xF0 ? colourPair >> 4 : colourPair & 0x0F;
        }
    }

    /// <summary>Works out where a pixel is, from the byte it is in and which half of the byte it is.</summary>
    /// <param name="byteIndex">The byte, counting a column at a time.</param>
    /// <param name="half">Which half of the byte: <c>0xF0</c> for the left pixel, otherwise the right.</param>
    /// <param name="pixel">The pixel's place, counting a row at a time.</param>
    /// <returns>False when the byte is past the end of the animation frame.</returns>
    private bool TryGetPixelIndex(int byteIndex, int half, out int pixel)
    {
        int bytesInPicture = Width / 2 * Height;
        int column = byteIndex / Height;
        int x = (column * 2) + (half == 0xF0 ? 0 : 1);
        int y = byteIndex % Height;
        pixel = (y * Width) + x;
        return byteIndex < bytesInPicture;
    }
}
