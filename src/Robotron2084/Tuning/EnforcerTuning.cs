namespace Robotron2084.Tuning;

/// <summary>The enforcer's growth and <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> tuning.</summary>
public static class EnforcerTuning
{
    /// <summary>
    ///     ROM `ENFR1B`: `NAP 3` — the enforcer's logic <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> is 4 <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> (3 <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">vblanks</see>
    ///     plus the frame it runs in), and its re-aim and shot timers count <see cref="Robotron2084.Core.ArcadeFrameTimer">BEATS</see>.
    /// </summary>
    public const int BeatIntervalArcadeFrames = 4;

    /// <summary>
    ///     ROM `ENFRCE`: `NAP 8` per spawn animation frame, which is 9 <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> (a `NAP n` is n+1
    ///     frames, as with the quark). The port once used 8.
    /// </summary>
    public const int GrowIntervalArcadeFrames = 9;

    /// <summary>ROM `ENFR0`: the enforcer grows through FIVE spawn animation frames, so growing takes 5 x 9 = 45.</summary>
    public const int GrowSteps = 5;
}
