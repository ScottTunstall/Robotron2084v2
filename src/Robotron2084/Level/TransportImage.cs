using Microsoft.Xna.Framework;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>One picture being beamed in on a brain wave: its pixels come on in groups, sparkling, and go off again, until the whole picture is there.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRT2.ASM</c> <c>TRNSPT</c> (the three ways a step changes the pixels), <c>TMAKE</c> and <c>UPDATE</c></item>
/// <item>Disassembly: the transporter overlay's update routine (<c>RTORG</c>, <c>$4140</c>)</item>
/// </list>
/// The arcade keeps ONE of these in a spare buffer for all the robots that are showing the same animation frame, and blits it to each
/// of them every frame, which is why it is a class of its own rather than part of a robot. A step takes the next entry of
/// <see cref="TransporterTuning.Steps"/>, and turns one group of pixels on, on with a sparkle, or off. The arcade's own picture is
/// two pixels to a byte and a column at a time, so a group names pixels by byte, and this class maps them back to a pixel's place.
/// Where a plain "on" meets a pixel that is still sparkling, the arcade mixes the two colour numbers; here the pixel simply takes its own colour.
/// </remarks>
public sealed class TransportImage
{
    /// <summary>A pixel that is not showing.</summary>
    public const int PixelOff = -1;

    /// <summary>A pixel that is showing its own colour.</summary>
    public const int PixelOwnColour = -2;

    private readonly int[] _pixels;
    private readonly bool[] _drawnInTemplate;
    private readonly int _sparkleSetStart;
    private int _nextStep;

    /// <summary>Starts an empty image of a picture.</summary>
    /// <param name="template">The picture's pixels, a row at a time. A pixel with no colour is a gap.</param>
    /// <param name="width">The picture's width in pixels. It must be even, as the arcade stores two pixels to a byte.</param>
    /// <param name="height">The picture's height in rows.</param>
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

    /// <summary>The picture's own pixels.</summary>
    public Color[] Template { get; }

    /// <summary>The picture's width in pixels.</summary>
    public int Width { get; }

    /// <summary>The picture's height in rows.</summary>
    public int Height { get; }

    /// <summary>Says whether the sequence has run out: nothing more will change.</summary>
    public bool IsFinished => _nextStep > TransporterTuning.StepCount;

    /// <summary>Gets what a pixel is showing.</summary>
    /// <param name="x">The pixel's column.</param>
    /// <param name="y">The pixel's row.</param>
    /// <returns><see cref="PixelOff"/>, <see cref="PixelOwnColour"/>, or a palette number from 0 to 15 for a sparkle.</returns>
    public int GetPixel(int x, int y) => _pixels[(y * Width) + x];

    /// <summary>Does one step of the sequence, which the arcade does once a frame.</summary>
    /// <param name="random">Where the sparkle's starting point comes from.</param>
    public void Step(Random random)
    {
        if (IsFinished)
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

            byte colours = TransporterTuning.SparkleColours[sparkle++ % TransporterTuning.SparkleColours.Length];
            _pixels[pixel] = half == 0xF0 ? colours >> 4 : colours & 0x0F;
        }
    }

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
