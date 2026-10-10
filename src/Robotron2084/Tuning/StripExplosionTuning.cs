using Robotron2084.Entities;

namespace Robotron2084.Tuning;

/// <summary>The numbers that drive the strip explosions and the strip appears: how far apart the strips start, how fast the gap changes, and how many effects each of the arcade's three strip routines can run at once (notes §35, §61, §143).</summary>
/// <remarks>
/// The gap between strips is kept as the arcade keeps it: a 16-bit number whose high byte is the gap in rows or columns, and whose low byte is a fraction of one.
/// An explosion starts with the strips together and the gap grows. An appear starts with them far apart and the gap shrinks.
/// </remarks>
public static class StripExplosionTuning
{
    /// <summary>The gap an appear starts with, as the 16-bit number. It is the starting value of a <see cref="StripEffect"/>'s gap when the effect is an appear.</summary>
    /// <remarks>Original source: <c>RRX7.ASM</c> <c>APSTV</c>, <c>LDD #$1000</c> ("START LARGE FOR APPEAR"); the same in <c>RRHX4.ASM</c> and <c>RRDX2.ASM</c>. Disassembly: <c>$5C01</c>.</remarks>
    public const int AppearStartSizer = 0x1000;

    /// <summary>How many fiftieths of a second an explosion lasts. It is counted down once each fiftieth of a second, and the explosion is gone when it reaches nothing.</summary>
    /// <remarks>Original source: <c>RRX7.ASM</c> <c>EXST2</c>, <c>LDA #$10 / STA FRAMES,U</c>. Disassembly: <c>$5C1F</c> onwards.</remarks>
    public const int ExplosionFrames = 0x10;

    /// <summary>The gap an explosion starts with, as the 16-bit number.</summary>
    /// <remarks>Original source: <c>RRX7.ASM</c> <c>NOCKK</c>, <c>LDD #$100</c> ("1 UNIT IS MIN"). Disassembly: <c>$5C1F</c> onwards.</remarks>
    public const int ExplosionStartSizer = 0x0100;

    /// <summary>How much the gap changes each fiftieth of a second for an explosion, and for an appear run by the diagonal routine. It is a whole row, so the picture changes on 50 times a second.</summary>
    /// <remarks>Original source: <c>RRX7.ASM</c> <c>WRITE</c>, <c>ADDD #$100</c>, and <c>RRDX2.ASM</c> <c>AWRITE</c>, <c>SUBD #$100</c>. Disassembly: <c>$5D93</c>.</remarks>
    public const int SizerStep = 0x0100;

    /// <summary>How much the gap shrinks each fiftieth of a second for an appear run by the vertical or the horizontal routine. It is half a row, so the picture changes on every second fiftieth of a second and the appear takes twice as long as a diagonal one.</summary>
    /// <remarks>Original source: <c>RRX7.ASM</c> <c>AWRITE</c>, <c>SUBD #$0080</c>; the same in <c>RRHX4.ASM</c>. Disassembly: <c>$5D4A</c>.</remarks>
    public const int SlowAppearSizerStep = 0x0080;

    /// <summary>The smallest gap at which the vertical and the diagonal routines still draw an appear. When the gap would fall below it the appear is over.</summary>
    /// <remarks>Original source: <c>RRX7.ASM</c> <c>AWW2</c>, <c>CMPA #1 / BHI APGO</c>; <c>RRDX2.ASM</c> <c>AWRIT1</c>, the same test. Disassembly: <c>$5D78</c>.</remarks>
    public const int SmallestAppearSpacing = 2;

    /// <summary>The smallest gap at which the horizontal routine still draws an appear, and at which the attract mode's appear does. They draw the strips closed up before the appear is over.</summary>
    /// <remarks>Original source: <c>RRHX4.ASM</c> <c>AWRIT1</c>, <c>TSTA / BHI APGO</c>; <c>RRX7.ASM</c> <c>AWW2</c>, <c>LDA #1</c> ("FORCE SIZE OF 1") for the attract mode. Disassembly: <c>$5D83</c> for the attract mode.</remarks>
    public const int SmallestClosedAppearSpacing = 1;

    /// <summary>How many records the horizontal routine has, for its explosions and its appears together. When they are all in use, an effect that needs one is not made.</summary>
    /// <remarks>
    /// Original source: <c>RRHX4.ASM</c> <c>HXINV</c>. The source asks for more, but the branch that would loop round to link the rest of them is on the same line as the compare before it,
    /// where the assembler took it for a comment (<c>CMPX #EXEND-EXSIZE       BLO    EXIN0</c>). So the loop runs once and links this many.
    /// Disassembly: <c>$F01D</c> to <c>$F038</c>, where there is no branch after the compare at <c>$F02C</c>.
    /// </remarks>
    public const int HorizontalPoolSize = 2;

    /// <summary>How many records the diagonal routine has, for its explosions and its appears together.</summary>
    /// <remarks>Original source: <c>RRDX2.ASM</c>, <c>RMB ((10-1)*EXSIZE)</c> after the first record at <c>DXTAB</c>. Disassembly: <c>$468C</c> to <c>$46A3</c>.</remarks>
    public const int DiagonalPoolSize = 10;

    /// <summary>Gets how many effects a strip routine can run at once.</summary>
    /// <param name="engine">The strip routine.</param>
    /// <returns>The number of records the routine has, or null for the vertical routine, which takes its records from the arcade's list of free objects (<c>OFREE</c>). The port keeps no such list, so here it never runs out.</returns>
    /// <remarks>Original source: <c>RRX7.ASM</c> <c>GETBLK</c> and <c>GETAP</c>, <c>LDU OFREE</c>. Disassembly: <c>$5B6C</c> and <c>$5B84</c>.</remarks>
    public static int? GetPoolSize(StripEngine engine) => engine switch
    {
        StripEngine.Horizontal => HorizontalPoolSize,
        StripEngine.Diagonal => DiagonalPoolSize,
        _ => null,
    };
}
