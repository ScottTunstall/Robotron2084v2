using Robotron2084.Core;

namespace Robotron2084.Palette;

/// <summary>
///     Keeps six of the palette's entries changing colour all the time, as the arcade does. They colour the player's score
///     digits, the spare-life icons, the electrodes and the wall.
/// </summary>
/// <remarks>
///     <para>
///         There are six colour processes, one for each of the palette entries 10 to 15 (notes §4.12). Each writes the first colour of
///         its own table as soon as it starts, goes through the table, and starts again at the start of the table when it
///         reaches the end:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>RGB, entry 11: green, red, blue, one every 8 fiftieths of a second.</description>
///         </item>
///         <item>
///             <description>DECAY, entry 12: a long fade from blue, through bright colours, down to red and black, one colour every 2 fiftieths of a second.</description>
///         </item>
///         <item>
///             <description>BPR (blue, purple, red), entry 14: one colour every fiftieth of a second.</description>
///         </item>
///         <item>
///             <description>RGOLD (red, gold), entry 15: one colour every 6 fiftieths of a second.</description>
///         </item>
///         <item>
///             <description>LASER, entry 13: the long table <see cref="LaserTab" />, one colour every 2 fiftieths of a second.</description>
///         </item>
///         <item>
///             <description>
///                 LF (the laser flash), entry 10: white for 2 fiftieths of a second, then a random colour from the first
///                 colours of <see cref="LaserTab" /> for 6 fiftieths, and then white again.
///             </description>
///         </item>
///     </list>
///     <para>
///         The cycles are counted in fiftieths of a second, and a fiftieth of a second is 6/5 of a port tick. So the timers use
///         the clock-unit counter that the entities use (notes §52): a 1-frame process comes round every 1.2 ticks, a
///         2-frame one every 2.4, and an 8-frame one every 9.6.
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 Original source: <c>RRS22.ASM</c>. <c>COLSTV</c> starts the processes, <c>TABDRI</c> runs the table-driven
///                 ones (<c>RGB</c>, <c>DECAY</c>, <c>BPR</c>, <c>RGOLD</c> and <c>LASER</c>) and writes a colour before it
///                 sleeps, <c>LF</c> is the laser flash, and <c>COLTAB</c> is the long colour table.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Disassembly: <c>asm/robomame.asm</c> at <c>$D981</c> (<c>RGB</c>), <c>$D98E</c> (<c>DECAY</c>), <c>$D9AE</c>
///                 (<c>BPR</c>), <c>$D9D2</c> (<c>RGOLD</c>), <c>$D9DF</c> (<c>LASER</c>), <c>$D9E8</c> (<c>TABDRI</c>),
///                 <c>$DA0D</c> (<c>LF</c>) and <c>$DA2C</c> (<c>COLTAB</c>). The disassembly gives no label for these, only
///                 addresses.
///             </description>
///         </item>
///     </list>
/// </remarks>
public sealed class PaletteAnimator
{
    /// <summary>The colours of the laser process, which the laser flash also picks from at random.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRS22.ASM</c> <c>COLTAB</c>.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$DA2C</c>.</description></item>
    ///     </list>
    /// </remarks>
    public static readonly byte[] LaserTab =
    [
        0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F,
        0x37, 0x2F, 0x27, 0x1F, 0x17, 0x47, 0x47, 0x87,
        0x87, 0xC7, 0xC7, 0xC6, 0xC5, 0xCC, 0xCB, 0xCA,
        0xDA, 0xE8, 0xF8, 0xF9, 0xFA, 0xFB, 0xFD, 0xFF,
        0xBF, 0x3F, 0x3E, 0x3C
    ];

    /// <summary>How many fiftieths of a second the laser flash stays white.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRS22.ASM</c> <c>LF</c>, <c>NAP 2,LF1</c>.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$DA11</c>, <c>LDA #$02</c>.</description></item>
    ///     </list>
    /// </remarks>
    private const int LaserFlashWhiteArcadeFrames = 2;

    /// <summary>How many fiftieths of a second the laser flash stays a random colour, before it goes white again.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRS22.ASM</c> <c>LF1</c>, <c>NAP 6,LF</c>.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$DA24</c>, <c>LDA #$06</c>.</description></item>
    ///     </list>
    /// </remarks>
    private const int LaserFlashRandomArcadeFrames = 6;

    /// <summary>How many of the first colours of <see cref="LaserTab" /> the laser flash picks its random colour from.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRS22.ASM</c> <c>LF1</c>, <c>LDA SEED / ANDA #$1F</c>, which picks a place from 0 to 31.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$DA1B</c>, <c>ANDA #$1F</c>.</description></item>
    ///     </list>
    /// </remarks>
    private const int LaserFlashRandomColourCount = 32;

    private readonly GamePalette _gamePalette;

    private readonly Process[] _processes;

    private readonly Random _random;

    /// <summary>What the laser flash's timer has gathered so far, in clock units.</summary>
    private int _laserFlashClockUnits;

    /// <summary>True when the laser flash goes white next, and false when it goes a random colour next.</summary>
    private bool _isLaserFlashWhiteNext = true;

    /// <summary>How many fiftieths of a second the laser flash waits before its next change. It starts at none.</summary>
    private int _laserFlashWaitArcadeFrames;

    /// <summary>Makes the six processes. Each writes the first colour of its table as soon as it is updated.</summary>
    /// <param name="gamePalette">The palette the processes write to.</param>
    /// <param name="random">Where the laser flash's random colours come from. When it is not given, a new one is made.</param>
    public PaletteAnimator(GamePalette gamePalette, Random? random = null)
    {
        _gamePalette = gamePalette;
        _random = random ?? new Random();
        _processes =
        [
            new Process { PaletteIndex = 11, ColourTable = [0x38, 0x07, 0xC0], ArcadeFramesPerCycle = 8 }, // RGB
            new Process
            {
                PaletteIndex = 12,
                ColourTable =
                [
                    0xC0, 0xC0, 0xD0, 0xE0, 0xF0, 0xF8, 0xFA, 0xBA, 0x7A, 0x3A, 0x34, 0x2D, 0x1F, 0x17, 0x0F, 0x07,
                    0x06, 0x05, 0x04, 0x03, 0x02, 0x01
                ],
                ArcadeFramesPerCycle = 2
            }, // DECAY
            new Process
            {
                PaletteIndex = 14,
                ColourTable =
                [
                    0xC0, 0xC1, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0x87, 0x87, 0x47, 0x47, 0x07, 0x07, 0x47, 0x47,
                    0x87, 0x87, 0xC7, 0xC7, 0xC6, 0xC5, 0xC4, 0xC3, 0xC2, 0xC1
                ],
                ArcadeFramesPerCycle = 1
            }, // BPR (blue, purple, red)
            new Process { PaletteIndex = 15, ColourTable = [0x07, 0x07, 0x2F], ArcadeFramesPerCycle = 6 }, // RGOLD (red, gold)
            new Process { PaletteIndex = 13, ColourTable = LaserTab, ArcadeFramesPerCycle = 2 } // LASER
        ];

        // Like the original, each process writes its first colour straight away and only then waits for its first cycle.
        foreach (var process in _processes) process.ClockUnits = ArcadeClock.ToClockUnits(process.ArcadeFramesPerCycle);
    }

    /// <summary>Moves every process on by one game tick, and changes a colour when a process's cycle is due.</summary>
    public void Update()
    {
        // The laser flash (entry 10): white for a short time, then a random colour for longer, and round again. Like the
        // original, it goes white straight away when it starts.
        _laserFlashClockUnits += ArcadeClock.UnitsPerPortTick;
        var waitClockUnits = ArcadeClock.ToClockUnits(_laserFlashWaitArcadeFrames);
        while (_laserFlashClockUnits >= waitClockUnits)
        {
            _laserFlashClockUnits -= waitClockUnits;

            byte flashColour = 0xFF;
            _laserFlashWaitArcadeFrames = LaserFlashWhiteArcadeFrames;
            if (!_isLaserFlashWhiteNext)
            {
                flashColour = LaserTab[_random.Next(LaserFlashRandomColourCount)];
                _laserFlashWaitArcadeFrames = LaserFlashRandomArcadeFrames;
            }

            _isLaserFlashWhiteNext = !_isLaserFlashWhiteNext;
            waitClockUnits = ArcadeClock.ToClockUnits(_laserFlashWaitArcadeFrames);

            // The player's death fade, or another caller, owns entry 10 for now.
            if (_gamePalette.IsPaletteIndexSuspended(10)) continue;

            _gamePalette.SetPaletteIndexValue(10, flashColour);
        }

        foreach (var process in _processes)
        {
            // A suspended entry is a process the original has ended: it does not advance either, so resuming picks it up
            // where it left off.
            if (_gamePalette.IsPaletteIndexSuspended(process.PaletteIndex)) continue;

            process.ClockUnits += ArcadeClock.UnitsPerPortTick;
            if (process.ClockUnits < ArcadeClock.ToClockUnits(process.ArcadeFramesPerCycle)) continue;

            process.ClockUnits -= ArcadeClock.ToClockUnits(process.ArcadeFramesPerCycle);
            _gamePalette.SetPaletteIndexValue(process.PaletteIndex, process.ColourTable[process.ColourIndex]);
            process.ColourIndex = (process.ColourIndex + 1) % process.ColourTable.Length;
        }
    }

    /// <summary>One of the six colour processes.</summary>
    private sealed class Process
    {
        /// <summary>How many fiftieths of a second pass between one cycle of the process and the next.</summary>
        public int ArcadeFramesPerCycle = 1;

        /// <summary>What the process's timer has gathered so far, in clock units.</summary>
        public int ClockUnits;

        /// <summary>The place in the colour table the process is at.</summary>
        public int ColourIndex;

        /// <summary>The colours the process goes through.</summary>
        public byte[] ColourTable = [];

        /// <summary>The palette entry the process writes.</summary>
        public int PaletteIndex;
    }
}
