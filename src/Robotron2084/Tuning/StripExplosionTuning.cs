namespace Robotron2084.Tuning;

/// <summary>The strip explosion and appear effects' size accumulators (notes §35, §61).</summary>
public static class StripExplosionTuning
{
    // StripEffect / appear — the RRDX2 "DIAGONAL EXPLOSIONS" engine (notes §35.5,
    // §61). The records come from ONE pool of 10 (`EX` at DXTAB, `RMB
    // ((10-1)*EXSIZE)`) shared by explosions and appears, so that is the cap on
    // the pair TOGETHER (notes §35.5).
    //
    // The sizes are the ROM's 16-bit fixed-point accumulators: the HIGH byte is
    // the step (rows for a row fan, columns for a column fan), so:
    //   explosion  YSIZER $0100 ("1 UNIT IS MIN"), +$0100 a frame, FRAMES $10
    //              -> the steps are 2,3,...,16 over 15 draws
    //   appear     YSIZER $1000 ("START LARGE FOR APPEAR"), -$0100 a frame,
    //              ending when the step would reach 1
    public const int ExplosionStartSizer = 0x0100;

    public const int AppearStartSizer = 0x1000;

    public const int SizerStep = 0x0100;

    public const int ExplosionFrames = 0x10;

    public const int MaxConcurrent = 10;
}
