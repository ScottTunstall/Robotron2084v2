namespace Robotron2084.Audio.Synthesis.Models;

/// <summary>What a settings table (<c>CANTB</c>, <c>HBMBTB</c>) gives <c>FNLOAD</c> besides the two flags.</summary>
/// <param name="FirstLimit">
///     The first slope limit, high byte (<c>FMAX</c>). It is shifted up by <see cref="FilteredNoise.ByteBits" /> to
///     give the starting value of <see cref="FilteredNoise._limit" />.
/// </param>
/// <param name="LevelsPerShrink">The levels between each shrink of the limit (<c>SAMPC</c>).</param>
/// <param name="SmallestSlope">The smallest slope (<c>LOFRQ</c>), added after the random distortion.</param>
/// <param name="SetUpCycles">The time from the sound's first instruction to <c>FNOIS0</c>.</param>
/// <param name="IsDistorted">
///     Whether the random high byte distorts the slope (<c>DSFLG</c>); it does for every sound that
///     shrinks.
/// </param>
/// <param name="Shrinks">
///     Whether the limit shrinks, so the sound dies away (<c>FDFLG</c>); the thrust and the background
///     never do.
/// </param>
/// <exception cref="ArgumentOutOfRangeException"><paramref name="SetUpCycles" /> is below 0.</exception>
internal readonly record struct FilteredNoiseParams(
    byte FirstLimit,
    ushort LevelsPerShrink,
    byte SmallestSlope,
    int SetUpCycles,
    bool IsDistorted = true,
    bool Shrinks = true)
{
    /// <summary>The time from the sound's first instruction to <c>FNOIS0</c>, in the board's clock cycles.</summary>
    public int SetUpCycles { get; } = SetUpCycles >= 0
        ? SetUpCycles
        : throw new ArgumentOutOfRangeException(nameof(SetUpCycles), SetUpCycles, "The set-up time cannot be below 0.");
}
