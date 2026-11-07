namespace Robotron2084.Tuning;

/// <summary>How the doors in the playfield's walls open, and what comes through them. Port only: the arcade has none.</summary>
public static class WallDoorTuning
{
    /// <summary>How many <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> a door takes to slide open.</summary>
    public const int OpenTicks = 30;

    /// <summary>How many <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> a door stays wide open.</summary>
    public const int HoldTicks = 90;

    /// <summary>How many <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> a door takes to slide shut.</summary>
    public const int CloseTicks = 30;

    /// <summary>How wide a door's opening is, in arcade pixels. It is the length of wall the two panels cover when shut.</summary>
    public const int GapArcadePixels = 40;

    /// <summary>How many doors open in a wave, one after another.</summary>
    public const int OpeningsPerWave = 3;

    /// <summary>How many grunts come through each opening, in rows as wide as the opening, one row after another.</summary>
    public const int GruntsPerOpening = 12;

    /// <summary>The gap left between grunts coming through an opening side by side, in port pixels.</summary>
    public const int GruntGapPixels = 4;

    /// <summary>How many <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> pass between one row of grunts coming through an opening and the next.</summary>
    public const int GruntIntervalTicks = 12;

    /// <summary>The fewest <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> between one door closing and the next opening.</summary>
    public const int MinTicksBetweenOpenings = 90;

    /// <summary>One more than the most <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> that can pass, over the fewest, between one door closing and the next opening.</summary>
    public const int ExtraTicksBetweenOpeningsRange = 120;

    /// <summary>How far in from each end of a wall a door's opening is kept, in arcade pixels, so it is never in a corner.</summary>
    public const int CornerMarginArcadePixels = 16;
}
