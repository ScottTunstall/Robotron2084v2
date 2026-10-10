namespace Robotron2084.Tuning;

/// <summary>The spheroid's movement, escape and spawn tuning.</summary>
public static class SpheroidTuning
{
    // Spheroid (re-decoded from RRC11 CIRCLE/CIRNAC/CIRGO in
    // notes §56). There is NO constant speed: the spheroid accumulates a random
    // acceleration and damps it by a 64th every beat, so its speed is emergent.
    // The clamps below are the ROM's own velocity limits ($0100 / $0200 = 1
    // column/frame and 2 rows/frame, the same 2 arcade px/frame).
    public const int BeatIntervalRomFrames = 3; // `NAP 2` + 1

    // CIRC3L's exit test: `CMPA #XMIN+3` / `CMPA #XMAX-10` with XMIN=7 and
    // XMAX=$8F (RRF.ASM:69-70), i.e. column 10 / column 133 of the video buffer.
    public const int EscapeExitLeftColumn = 10;

    public const int EscapeExitRightColumn = 133;
    public const int MaxVelocityXSubpixels = 0x0100; // 1/256-column units per frame

    public const int MaxVelocityYSubpixels = 0x0200; // 1/256-row units per frame
    public const int MinDistanceFromPlayer = 100; // arcade pixels, spec-stated

    public const int NearWallBiasDistance = 30;
    public const int NearWallBiasPercent = 70;

    // arcade pixels
}
