namespace Robotron2084.Entities.Models;

/// <summary>What a wave sets for its quarks: how many tanks they drop, how long they wait between drops, and how fast they drift.</summary>
public sealed record QuarkWaveSettings
{
    /// <summary>The limit used when a wave does not set one.</summary>
    internal const int DefaultMaxDropsX2 = 10;

    /// <summary>The most beats a quark waits between drops, when a wave does not set one.</summary>
    internal const int DefaultDropIntervalMaxBeats = 12;

    /// <summary>The top drifting speed, when a wave does not set one.</summary>
    internal const int DefaultMaxSpeed = 50;

    /// <summary>Makes the settings of one wave's quarks.</summary>
    /// <param name="maxDropsX2">
    ///     This wave's limit for picking how many tanks the quark drops. The most it can drop is half of
    ///     this.
    /// </param>
    /// <param name="dropIntervalMaxBeats">The most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> this wave's quark waits between tank drops.</param>
    /// <param name="maxSpeed">The quark's top drifting speed on this wave (ROM: <c>SQSPD</c>).</param>
    /// <exception cref="ArgumentOutOfRangeException">Any of them is below 0.</exception>
    public QuarkWaveSettings(
        int maxDropsX2 = DefaultMaxDropsX2,
        int dropIntervalMaxBeats = DefaultDropIntervalMaxBeats,
        int maxSpeed = DefaultMaxSpeed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxDropsX2);
        ArgumentOutOfRangeException.ThrowIfNegative(dropIntervalMaxBeats);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSpeed);

        MaxDropsX2 = maxDropsX2;
        DropIntervalMaxBeats = dropIntervalMaxBeats;
        MaxSpeed = maxSpeed;
    }

    /// <summary>This wave's limit for picking how many tanks the quark drops.</summary>
    public int MaxDropsX2 { get; }

    /// <summary>The most beats this wave's quark waits between tank drops.</summary>
    public int DropIntervalMaxBeats { get; }

    /// <summary>The quark's top drifting speed on this wave.</summary>
    public int MaxSpeed { get; }
}
