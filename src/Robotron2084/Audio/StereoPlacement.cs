using Microsoft.Xna.Framework;
using Robotron2084.Tuning;

namespace Robotron2084.Audio;

/// <summary>
///     Works out where between the speakers a sound belongs, from where the thing that made it is on the
///     playfield: something on the left is heard on the left, and so on. A port addition; the arcade's
///     speaker is mono.
/// </summary>
public static class StereoPlacement
{
    /// <summary>The place between the speakers for a sound made by something on the playfield.</summary>
    /// <param name="maker">The box of whatever made the sound.</param>
    /// <param name="playfield">The playfield's inner area.</param>
    /// <returns>
    ///     Where the sound is heard, from -1 (left) to 1 (right), narrowed by <see cref="SoundTuning.StereoWidth" />.
    /// </returns>
    public static float GetPan(Rectangle maker, Rectangle playfield)
    {
        var halfWidth = playfield.Width / 2f;
        float offsetFromCentre = maker.Center.X - playfield.Center.X;
        var pan = Math.Clamp(offsetFromCentre / halfWidth, -1f, 1f);
        return pan * SoundTuning.StereoWidth;
    }
}
