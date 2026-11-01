namespace Robotron2084.Audio.Synthesis;

/// <summary>What a settings table (<c>CANTB</c>, <c>HBMBTB</c>) gives <c>FNLOAD</c> besides the two flags.</summary>
/// <param name="FirstLimit">
///     The first slope limit, high byte (<c>FMAX</c>). It is shifted up by <see cref="ByteBits" /> to
///     give the starting value of <see cref="_limit" />.
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
internal readonly record struct FilteredNoiseSettings(
    byte FirstLimit,
    ushort LevelsPerShrink,
    byte SmallestSlope,
    int SetUpCycles,
    bool IsDistorted = true,
    bool Shrinks = true);
