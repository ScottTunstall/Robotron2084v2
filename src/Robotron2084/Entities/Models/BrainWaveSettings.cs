namespace Robotron2084.Entities.Models;

/// <summary>What a wave sets for its brains: how long they wait between beats, and how often they fire a cruise missile.</summary>
public sealed record BrainWaveSettings
{
    /// <summary>Makes the settings of one wave's brains.</summary>
    /// <param name="beatWaitArcadeFrames">
    ///     How long this wave's brains wait after each <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. A bigger number makes a slower brain.
    /// </param>
    /// <param name="fireIntervalBeats">The longest this wave's brains wait between cruise missiles, in <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either is below 0.</exception>
    public BrainWaveSettings(int beatWaitArcadeFrames, int fireIntervalBeats)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(beatWaitArcadeFrames);
        ArgumentOutOfRangeException.ThrowIfNegative(fireIntervalBeats);

        BeatWaitArcadeFrames = beatWaitArcadeFrames;
        FireIntervalBeats = fireIntervalBeats;
    }

    /// <summary>How long this wave's brains wait after each beat, in arcade frames.</summary>
    public int BeatWaitArcadeFrames { get; }

    /// <summary>The longest this wave's brains wait between cruise missiles, in beats.</summary>
    public int FireIntervalBeats { get; }
}
