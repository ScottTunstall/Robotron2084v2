namespace Robotron2084.Tuning;

/// <summary>The spark's ballistic movement and lifetime, and the cap on sparks in play.</summary>
public static class SparkTuning
{
    // Enforcer (R5 retune, notes §17)
    public const int GlobalActiveSparkCap = 20; // R5 $1412: $14 (20) sparks max

    public const int SparkAccelRomRange = 16;

    public const int SparkAimDivisor = 64;

    // Spark flicker: the ROM SPARK process advances OPICT by one 4-byte
    // animation frame entry (SPKP0..3) on every beat pass and re-runs every 4
    // vblanks (NAP 4) — a 4-frame flash, one frame per 4 vblanks (notes 32).
    public const int SparkFrameIntervalRomFrames = 4;

    /// <summary>The widest aim wobble: from minus this to just under plus this, in columns sideways and rows up and down.</summary>
    /// <remarks>
    ///     Original source: <c>RRC11.ASM</c> <c>ENFSHT</c>, <c>ANDB #$1F / ADDB #-$10</c> on both axes. Disassembly:
    ///     <c>CREATE_SPARK</c> (<c>$1404</c>).
    /// </remarks>
    public const int SparkJitterRange = 16;

    public const int SparkLeftWallJitterColumns = 16;

    public const int SparkLifeMaxRomFrames = 140;

    public const int SparkLifeMinRomFrames = 80;

    public const int SparkMaxSpeed = 8;

    // Spark — arcade-faithful per the GOSPEL (ref/original-source/
    // RRC11.ASM: ENFSHT + SPARK + the SPKP0..3 pics; notes 41). A spark is a
    // BALLISTIC particle, NOT a random walk:
    //   ENFSHT: OXV = 4 x (player coord + jitter - spark coord) [subpixels];
    //           jitter = (SEED & $1F) - 16, i.e. -16..+15 COLUMNS per axis, and
    //           the X jitter is forced to 0 when the player is within 16 columns
    //           of the left wall (`CMPA #XMIN+$10 / BHS ENFS2 / CLRB`);
    //           PD2/PD4 = (LSEED/HSEED & $1F) - 16 = a CONSTANT per-axis
    //           acceleration in those same subpixel units;
    //           PD7 = (HSEED & $F) + $14 = 20..35 MOVES of life.
    //   SPARK:  once per move (NAP 4 = 4 vblanks): OXV += PD2, OYV += PD4. The
    //           acceleration integrates into the velocity, so the path is a
    //           PARABOLA — that is the arcade spark's "habit of going off
    //           course", and it is why a spark can start out moving AWAY from
    //           the player.
    //   Mover (RRS22.ASM OPB80: `LDD OX16,X / LDU OPICT,X / ADDD OXV,X / ... /
    //   STD OX16,X`, looping the object list): the FULL 16-bit velocity is added
    //   to the 16-bit world position ONCE PER FRAME. So OXV = 4 x delta means the
    //   spark travels delta/64 COLUMNS per FRAME, and PD2 = a bends that by
    //   a/256 columns per frame. (An axis update that would leave the playfield
    //   is REJECTED, not clamped — the object keeps its last valid coordinate on
    //   that axis while the other axis still moves.)
    // Port units: 1 ROM column = 2 arcade px = ToPortPixelsFromArcadePixels(2) port px = 4 port px,
    // so one frame advances deltaPort/64 port px and the acceleration is
    // a/64 port px per frame of velocity. Both live in 1/256-px fixed point.
    public const int SparkMoveIntervalRomFrames = 4; // NAP 4

    // ROM: the delta is covered in 64 frames

    // (seed & $1F) - 16 → -16..+15

    // XMIN+$10: no X jitter this near the wall

    // PD2/PD4 = (seed & $1F) - 16

    // port safety cap on the px/frame step
}
