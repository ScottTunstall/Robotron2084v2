namespace Robotron2084.Tuning;

/// <summary>The quark's movement, drop and animation tuning.</summary>
public static class QuarkTuning
{
    // Quark — from the GOSPEL (RRTK4 `SQUARE` + `SQVEL`; notes §43,
    // §51). The quark DRIFTS: it is not waypoint-seeking and its speed is not
    // proportional to any distance.
    //
    //   SQVEL:  OXV = ±RND(1..SQSPD) × 4   and   OYV = ±RND(1..SQSPD) × 8
    //           in 1/256-px-per-FRAME units, so Y is twice X per unit.
    //           SQSPD is a wave-table value (50/56/60).
    //   sign:   flipped away from the walls FIRST — X <= XMIN+5 → +,
    //           X >= XMAX-12 → -, Y <= YMIN+5 → +, Y >= YMAX-20 → - — and only
    //           otherwise taken from the seed bit (X: set = negative, Y: set =
    //           positive; the opposite polarity decorrelates the axes).
    //   beat:   NAP 3, and PD7 counts down in BEATS to the next SQVEL.
    public const int BeatRomFrames = 4;      // NAP 3 + the beat vblank

    /// <summary>How far above the bottom edge, in rows, a fleeing quark is counted as gone.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SQ3L</c>, <c>YMAX-16</c>.</remarks>
    public const int FleeExitHighRows = 16;

    /// <summary>How far below the top edge, in rows, a fleeing quark is counted as gone.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SQ3L</c>, <c>YMIN+2</c>.</remarks>
    public const int FleeExitLowRows = 2;
    public const int FleeVelocityRom = 0x0200;
    public const int ReaimMaxBeats = 32;
    public const int TotalAnimationFrames = 9;
    public const int TravelAnimationFrames = 5;
    public const int VelocityXScale = 4;    // ROM: two ASLB/ROLA pairs

    public const int VelocityYScale = 8;    // ROM: three

    // ROM PD7 = (SEED & $1F) + 1

    /// <summary>How close to the bottom edge, in rows, a quark must be before it is turned back up.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SQVEL</c>, <c>YMAX-20</c>.</remarks>
    public const int WallMarginBottomRows = 20;

    /// <summary>How close to the left edge, in columns, a quark must be before it is turned back right.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SQVEL</c>, <c>XMIN+5</c> (columns, so 10 arcade pixels).</remarks>
    public const int WallMarginLeftColumns = 5;

    /// <summary>How close to the top edge, in rows, a quark must be before it is turned back down.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SQVEL</c>, <c>YMIN+5</c>.</remarks>
    public const int WallMarginTopRows = 5;

    /// <summary>How close to the right edge, in columns, a quark must be before it is turned back left.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SQVEL</c>, <c>XMAX-12</c> (columns, so 24 arcade pixels).</remarks>
    public const int WallMarginRightColumns = 12;

    // SQ3: OXV = 0, OYV = ±$200 per frame

    // SQ3L: Y <= YMIN+2 ...

    // ... or Y >= YMAX-16 → gone

    // SQP0..SQP4 while wandering

    // SQP0..SQP8 once it starts dropping tanks
}
