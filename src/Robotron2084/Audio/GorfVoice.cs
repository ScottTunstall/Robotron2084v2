namespace Robotron2084.Audio;

/// <summary>Gorf's voice: which sample says each of its lines, and how far its voice can be raised or lowered.</summary>
/// <remarks>Port only: Gorf is the author's own robot, and the arcade has no speech sound chip.</remarks>
public static class GorfVoice
{
    /// <summary>The most a line can be raised or lowered, in octaves either way.</summary>
    public const float MaximumPitchOctaves = 1f;

    /// <summary>Gets the name of a line's sample: its file name in <c>Content/Speech/GORF/</c>, in capitals.</summary>
    /// <param name="gorfLine">The line.</param>
    public static string GetLineName(GorfLine gorfLine)
    {
        return gorfLine.ToString().ToUpperInvariant();
    }
}
