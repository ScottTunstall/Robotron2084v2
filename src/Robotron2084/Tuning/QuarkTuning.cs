namespace Robotron2084.Tuning;

/// <summary>The quark's movement, drop and picture tuning.</summary>
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

    public const int VelocityXScale = 4;    // ROM: two ASLB/ROLA pairs

    public const int VelocityYScale = 8;    // ROM: three


    public const int ReaimMaxBeats = 32;   // ROM PD7 = (SEED & $1F) + 1

    public const int WallMarginLowArcadePixels = 5;     // XMIN+5 / YMIN+5

    public const int WallMarginRightArcadePixels = 12;  // XMAX-12

    public const int WallMarginBottomArcadePixels = 20; // YMAX-20

    public const int FleeVelocityRom = 0x0200; // SQ3: OXV = 0, OYV = ±$200 per frame

    public const int FleeExitLowArcadePixels = 2;   // SQ3L: Y <= YMIN+2 ...

    public const int FleeExitHighArcadePixels = 16; // ... or Y >= YMAX-16 → gone

    public const int TravelAnimationFrames = 5;  // SQP0..SQP4 while wandering

    public const int TotalAnimationFrames = 9;   // SQP0..SQP8 once it starts dropping tanks
}
