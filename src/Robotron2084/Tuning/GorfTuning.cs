namespace Robotron2084.Tuning;

/// <summary>
///     How Gorf crosses the screen: how fast, and how long its hops are and how high. Gorf is the author's own robot,
///     so none of these come from the arcade.
/// </summary>
public static class GorfTuning
{
    /// <summary>How many fiftieths of a second Gorf waits between steps.</summary>
    public const int StepRomFrames = 2;

    /// <summary>How far Gorf goes sideways each step, in columns (2 arcade pixels each).</summary>
    public const int StepColumns = 1;

    /// <summary>How many steps one hop takes, from leaving the ground to landing.</summary>
    public const int HopSteps = 16;

    /// <summary>How high every hop goes, in rows (one arcade pixel each). They are all the same height, 16 for now.</summary>
    public const int HopRows = 16;

    /// <summary>How many places on its way across Gorf stops to drop grunts, evenly spaced.</summary>
    public const int DropStops = 3;

    /// <summary>The most grunts Gorf drops at one stop.</summary>
    public const int MaxGruntsPerDrop = 6;

    /// <summary>
    ///     The fewest grunts a level may hold, whatever the wave brings, so that a Gorf in a wave with no grunts can
    ///     still drop some.
    /// </summary>
    public const int MinimumGruntCap = 6;

    /// <summary>How far a dropped grunt falls each tick, in port pixels.</summary>
    public const int FallPixelsPerTick = 4;

    /// <summary>The gap left between grunts dropped side by side, in port pixels.</summary>
    public const int DropGapPixels = 4;

    /// <summary>How many fiftieths of a second each of Gorf's two animation frames shows for.</summary>
    public const int AnimationFrameRomFrames = 8;
}
