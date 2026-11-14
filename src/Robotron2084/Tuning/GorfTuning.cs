using Robotron2084.Audio;

namespace Robotron2084.Tuning;

/// <summary>
///     How Gorf crosses the screen: how fast, and how long its hops are and how high. Gorf is the author's own robot,
///     so none of these come from the arcade.
/// </summary>
public static class GorfTuning
{
    /// <summary>How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> each of Gorf's two animation frames shows for.</summary>
    public const int AnimationIntervalArcadeFrames = 8;

    /// <summary>How long one of Gorf's <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> lasts. Gorf takes one step on each <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.</summary>
    public const int BeatIntervalArcadeFrames = 2;

    /// <summary>The gap left between grunts dropped side by side, in port pixels.</summary>
    public const int DropGapPixels = 4;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> pass between Gorf being shot, with
    ///     grunts still to drop, and a new Gorf coming on to drop them.
    /// </summary>
    public const int ReturnDelayTicks = 120;

    /// <summary>
    ///     How much faster each Gorf that comes back drops its grunts, as a fraction: the steps between drops are this many
    ///     fifths of what they were (<see cref="ReturnDropIntervalFifths" /> out of 5).
    /// </summary>
    public const int ReturnDropIntervalFifths = 4;

    /// <summary>The gap left between Gorf's whole body and the player's box when it is to keep clear of the player, in port pixels.</summary>
    public const int PlayerClearancePixels = 12;

    /// <summary>
    ///     The speech lines Gorf says, one of them picked by chance, when it first appears in the first wave: "Long live
    ///     Gorf", "I am the Gorfian Empire", "Gorfian robots... attack! attack!", "You cannot escape the Gorfian robots",
    ///     "Gorfians take no prisoners!" and "You will meet a Gorfian doom".
    /// </summary>
    /// <remarks>Notes §149.</remarks>
    public static readonly IReadOnlyList<GorfLine> IntroAppearanceLines =
    [
        GorfLine.LongLiveGorf,
        GorfLine.IAmTheGorfianEmpire,
        GorfLine.GorfianRobotsAttack,
        GorfLine.CannotEscapeGorf,
        GorfLine.GorfiansTakeNoPrisoners,
        GorfLine.GorfianDoom
    ];

    /// <summary>How many electrodes the first wave, which introduces Gorf, has.</summary>
    public const int IntroWaveElectrodeCount = 10;

    /// <summary>How many places on its way across Gorf stops to drop grunts, evenly spaced.</summary>
    public const int DropStops = 3;

    /// <summary>How far a dropped grunt falls each <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>, in port pixels.</summary>
    public const int FallPixelsPerTick = 4;

    /// <summary>How high every hop goes, in rows (one arcade pixel each). They are all the same height.</summary>
    public const int HopRows = 16;

    /// <summary>How many steps one hop takes, from leaving the ground to landing.</summary>
    public const int HopSteps = 12;

    /// <summary>The most grunts Gorf drops at one stop.</summary>
    public const int MaxGruntsPerDrop = 6;

    /// <summary>
    ///     The fewest grunts a wave may hold, whatever the wave brings, so that a Gorf in a wave with no grunts can
    ///     still drop some.
    /// </summary>
    public const int MinGruntCount = 6;

    /// <summary>How far Gorf goes sideways each step, in port pixels.</summary>
    public const int StepPixels = 6;
}
