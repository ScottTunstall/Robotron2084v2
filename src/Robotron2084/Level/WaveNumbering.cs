namespace Robotron2084.Level;

/// <summary>How the port numbers its waves, against the arcade's.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Port only: wave 1 is the author's own introduction to Gorf, so every wave the arcade has comes one wave
///             later. The arcade's wave 1 is wave 2, its wave 2 is wave 3, and so on (notes §150).
///         </item>
///         <item>
///             A player's wave number (<see cref="PlayerSlot.Wave" />) is the port's. Everything that comes from the
///             arcade's tables, colours and rules is looked up by the arcade's number instead, which
///             <see cref="ToArcadeWave" /> works out.
///         </item>
///         <item>The attract demo plays the arcade's waves from the start, with the arcade's numbers.</item>
///     </list>
/// </remarks>
public static class WaveNumbering
{
    /// <summary>The wave that introduces Gorf.</summary>
    public const int GorfIntroWave = 1;

    /// <summary>The port's number for the arcade's first wave.</summary>
    public const int FirstArcadeWave = GorfIntroWave + 1;

    /// <summary>Gets whether a wave is the one that introduces Gorf.</summary>
    /// <param name="wave">The port's wave number.</param>
    public static bool IsGorfIntroWave(int wave)
    {
        return wave == GorfIntroWave;
    }

    /// <summary>Gets the arcade's number for a wave, which its tables and rules are looked up by.</summary>
    /// <param name="wave">The port's wave number.</param>
    /// <returns>The arcade's number, counted from 1. The wave that introduces Gorf has none of its own, so it gets 1.</returns>
    public static int ToArcadeWave(int wave)
    {
        return Math.Max(1, wave - GorfIntroWave);
    }
}
