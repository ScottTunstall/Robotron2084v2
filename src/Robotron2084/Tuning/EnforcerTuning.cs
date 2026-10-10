namespace Robotron2084.Tuning;

/// <summary>The enforcer's growth and beat tuning.</summary>
public static class EnforcerTuning
{
    /// <summary>
    /// ROM `ENFR1B`: `NAP 3` — the enforcer's logic beat is 4 fiftieths of a second (3 vblanks
    /// plus the frame it runs in), and its re-aim and shot timers count BEATS.
    /// </summary>
    public const int BeatIntervalRomFrames = 4;

    /// <summary>ROM `ENFRCE`: `NAP 8` per spawn animation frame.</summary>
    public const int GrowStepRomFrames = 9;

    /// <summary>
    /// ROM `ENFR0`: the enforcer's grow-up is FIVE spawn animation frames at `NAP 8`
    /// each = 5 x 9 fiftieths of a second = **45** (a `NAP n` beat is n+1 frames, as with the
    /// quark). The port used 40, treating each step as 8.
    /// </summary>
    public const int GrowUpRomFrames = 45;
}
