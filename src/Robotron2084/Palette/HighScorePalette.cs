using Robotron2084.Core;

namespace Robotron2084.Palette;

/// <summary>
///     The colours of the high score page, and the five timers that keep them moving. Colours run round the wall, and the
///     lists and highlights pulse between a dark colour and white.
/// </summary>
/// <remarks>
///     <para>The page starts five colour processes (notes §98.5). Together they are why the page is never still:</para>
///     <list type="bullet">
///         <item>
///             <description>
///                 The wall, in palette entries 1 to 8: every 3 fiftieths of a second the next colour of the wall's table goes
///                 into entry 8, and every older colour moves down one entry. Entry 7 is the headers ("ROBOTRON HEROES"), so
///                 they trail the wall by one cycle.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Today's list, in entry 9: every 4 fiftieths of a second it pulses from red to white and back, out of step
///                 with the top entry.
///             </description>
///         </item>
///         <item>
///             <description>
///                 The top entry, "( WILLY ELKTRIX )", in entry 10: the same pulse as today's list, but in step.
///             </description>
///         </item>
///         <item>
///             <description>
///                 The "you are here" highlight, in entry 12: every 4 fiftieths of a second it pulses from white to blue and
///                 back, out of step with the top entry's highlight.
///             </description>
///         </item>
///         <item>
///             <description>The top entry's highlight, in entry 13: the same pulse, but in step.</description>
///         </item>
///     </list>
///     <para>
///         The two pulsing tables end in a zero that sends the process back to the start of the table, which is why they pulse
///         instead of stopping. The wall's table simply starts again at its end.
///     </para>
///     <para>
///         The cycles are counted in fiftieths of a second, and a fiftieth of a second is 6/5 of a port tick. So the timers
///         use the clock-unit counter that the entities use (notes §52): a 3-frame cycle comes round every 3.6 ticks, and a
///         4-frame one every 4.8.
///     </para>
///     <para>
///         The page also clears the palette first, so it comes up from black. And entries 10, 12 and 13 are taken over from
///         the in-game animator, which the original does by ending the animator's colour processes with the game
///         (notes §4.12).
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 Original source: <c>RRTABLE.ASM</c>. <c>TABLE</c> starts <c>LOOPP</c> (the wall) and then <c>DECAZ</c>,
///                 <c>COLA</c>, <c>COLC</c> and <c>COLD</c>. <c>OUTCOL</c> shifts the wall's colours, <c>COLA3</c> sends a
///                 pulsing table back to its start, <c>FRAMER</c> clears the palette, and the tables are <c>COLTAB</c>,
///                 <c>CATAB</c> and <c>CCTAB</c>.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Disassembly: <c>asm/robomame.asm</c> at <c>$E26F</c> (<c>LOOPP</c>), <c>$E289</c> (<c>OUTCOL</c>),
///                 <c>$E29A</c> (<c>COLA</c>), <c>$E2AB</c> (<c>COLA3</c>), <c>$E2DF</c> (<c>DECAZ</c>), <c>$E2E9</c>
///                 (<c>COLC</c>), <c>$E2F5</c> (<c>COLD</c>), <c>$E2FE</c> (<c>COLTAB</c>), <c>$E2C2</c> (<c>CATAB</c>) and
///                 <c>$E2D1</c> (<c>CCTAB</c>). The disassembly gives no label for these, only addresses.
///             </description>
///         </item>
///     </list>
/// </remarks>
public sealed class HighScorePalette
{
    /// <summary>The colours of the highlights: white, down to blue and back.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRTABLE.ASM</c> <c>CCTAB</c>.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$E2D1</c>.</description></item>
    ///     </list>
    /// </remarks>
    public static readonly byte[] AccentTable =
    [
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xE4,
        0xD2, 0xC0, 0xC0, 0xC0, 0xD2, 0xE4, 0x00
    ];

    /// <summary>The colours the wall goes through, one after another, and then round again.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRTABLE.ASM</c> <c>COLTAB</c>.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$E2FE</c>.</description></item>
    ///     </list>
    /// </remarks>
    public static readonly byte[] CycleTable =
    [
        0x37, 0x2F, 0x27, 0x1F, 0x17, 0x47, 0x47, 0x87,
        0x87, 0xC7, 0xC7, 0xC6, 0xC5, 0xCC, 0xCB, 0xCA,
        0xC0, 0xD0, 0x98, 0x38, 0x33
    ];

    /// <summary>The palette entries the page takes over from the in-game colour animator.</summary>
    public static readonly int[] OwnedPaletteIndices = [10, 12, 13];

    /// <summary>The colours of the two lists: red, up to white, and back.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRTABLE.ASM</c> <c>CATAB</c>.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$E2C2</c>.</description></item>
    ///     </list>
    /// </remarks>
    public static readonly byte[] RampTable =
    [
        0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
        0x57, 0xA7, 0xFF, 0xFF, 0xA7, 0x57, 0x00
    ];

    /// <summary>
    ///     Stands for the wall's process in the list of processes. The wall's process does not set one palette entry: it
    ///     shifts the colours down through entries 1 to 8.
    /// </summary>
    private const int LoopPaletteIndex = 0;

    /// <summary>
    ///     Where the four pulsing processes begin in the list of processes. Everything before it (the wall) comes up with the
    ///     page.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRTABLE.ASM</c> <c>TABLE</c>, which starts <c>DECAZ</c>, <c>COLA</c>, <c>COLC</c> and <c>COLD</c> after the page is printed.</description></item>
    ///         <item><description>Disassembly: not separately labelled.</description></item>
    ///     </list>
    /// </remarks>
    private const int RampProcessStart = 1;

    private readonly Process[] _processes;

    public HighScorePalette()
    {
        // The original starts DECAZ and COLC part-way through their tables, so that they pulse out of step with COLA and COLD.
        _processes =
        [
            new Process { PaletteIndex = LoopPaletteIndex, ColourTable = CycleTable, ArcadeFramesPerCycle = 3 },
            new Process { PaletteIndex = 9, ColourTable = RampTable, ColourIndex = 7, ArcadeFramesPerCycle = 4 },
            new Process { PaletteIndex = 10, ColourTable = RampTable, ArcadeFramesPerCycle = 4 },
            new Process { PaletteIndex = 12, ColourTable = AccentTable, ColourIndex = 7, ArcadeFramesPerCycle = 4 },
            new Process { PaletteIndex = 13, ColourTable = AccentTable, ArcadeFramesPerCycle = 4 }
        ];
    }

    /// <summary>True once the four pulsing processes have started.</summary>
    public bool RampsStarted { get; private set; }

    /// <summary>Clears the palette to black and starts the wall's colours.</summary>
    /// <param name="gamePalette">The palette to clear and write to.</param>
    /// <remarks>
    ///     <para>
    ///         The wall starts before the frame is drawn, so entries 1 to 8 come alive first and the page comes up black. The
    ///         four pulsing processes do not exist yet; see <see cref="StartRamps" />.
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Original source: <c>RRTABLE.ASM</c> <c>TABLE</c> starts <c>LOOPP</c> before <c>FRAMER</c> draws the
    ///                 frame and clears the sixteen palette entries.
    ///             </description>
    ///         </item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$E26F</c> (<c>LOOPP</c>).</description></item>
    ///     </list>
    /// </remarks>
    public void Start(GamePalette gamePalette)
    {
        for (var paletteIndex = 0; paletteIndex <= 15; paletteIndex++) gamePalette.SetPaletteIndexValue(paletteIndex, 0x00);

        foreach (var paletteIndex in OwnedPaletteIndices) gamePalette.SuspendPaletteIndex(paletteIndex);

        Apply(gamePalette, _processes[0], _processes[0].ColourTable[_processes[0].ColourIndex]);
    }

    /// <summary>Starts the four pulsing processes, once the page has finished printing.</summary>
    /// <param name="gamePalette">The palette to write to.</param>
    /// <remarks>
    ///     <para>
    ///         Until this runs, entries 9, 10, 12 and 13 are the zeroes the clear left behind, which is why the printed rows are
    ///         invisible for their first few frames. The original starts the processes just before the long hold on this page.
    ///     </para>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRTABLE.ASM</c> <c>TABLE</c>, <c>MAKP DECAZ</c>, <c>MAKP COLA</c>, <c>MAKP COLC</c> and <c>MAKP COLD</c>.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$E2DF</c>, <c>$E29A</c>, <c>$E2E9</c> and <c>$E2F5</c>.</description></item>
    ///     </list>
    /// </remarks>
    public void StartRamps(GamePalette gamePalette)
    {
        if (RampsStarted) return;

        RampsStarted = true;
        for (var i = RampProcessStart; i < _processes.Length; i++)
            Apply(gamePalette, _processes[i], _processes[i].ColourTable[_processes[i].ColourIndex]);
    }

    /// <summary>Puts the palette back to its usual colours when the page is left, and gives entries 10, 12 and 13 back to the in-game animator.</summary>
    /// <param name="gamePalette">The palette to put the usual colours back in.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Original source: the colour processes end with the page, and the page after it, <c>FAMPAG</c> in
    ///                 <c>RRSCRIPT.ASM</c>, sets its own colours.
    ///             </description>
    ///         </item>
    ///         <item><description>Disassembly: not separately labelled.</description></item>
    ///     </list>
    /// </remarks>
    public void Stop(GamePalette gamePalette)
    {
        for (var paletteIndex = 0; paletteIndex <= 15; paletteIndex++) gamePalette.SetPaletteIndexValue(paletteIndex, GamePalette.DefaultPaletteValues[paletteIndex]);

        foreach (var paletteIndex in OwnedPaletteIndices) gamePalette.ResumePaletteIndex(paletteIndex);
    }

    /// <summary>Moves every process on by one port tick, and changes a colour when a process's cycle is due.</summary>
    /// <param name="gamePalette">The palette to change.</param>
    public void Update(GamePalette gamePalette)
    {
        for (var i = 0; i < _processes.Length; i++)
        {
            // The pulsing processes do not exist until the page has finished printing.
            if (i >= RampProcessStart &&
                !RampsStarted) continue;

            var process = _processes[i];
            process.ClockUnits += ArcadeClock.UnitsPerPortTick;
            var period = ArcadeClock.ToClockUnits(process.ArcadeFramesPerCycle);
            if (process.ClockUnits < period) continue;

            process.ClockUnits -= period;
            process.ColourIndex = Advance(process.ColourTable, process.ColourIndex);
            Apply(gamePalette, process, process.ColourTable[process.ColourIndex]);
        }
    }

    /// <summary>Finds the next colour in a table. A zero in the table is skipped, so the process goes back to the start.</summary>
    /// <param name="table">The colour table.</param>
    /// <param name="index">The place in the table the process is at now.</param>
    /// <returns>The place in the table of the next colour.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRTABLE.ASM</c> <c>COLA3</c>, which sends the process back to the start of the table at a zero.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$E2AB</c>.</description></item>
    ///     </list>
    /// </remarks>
    private static int Advance(byte[] table, int index)
    {
        var next = (index + 1) % table.Length;
        for (var guard = 0; guard < table.Length && table[next] == 0; guard++) next = (next + 1) % table.Length;

        return next;
    }

    /// <summary>Writes a process's colour into the palette. The wall's process shifts the colours down instead.</summary>
    /// <param name="gamePalette">The palette to write to.</param>
    /// <param name="process">The process the colour is for.</param>
    /// <param name="value">The colour to write.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Original source: <c>RRTABLE.ASM</c> <c>OUTCOL</c>: entry 2 moves to entry 1, entry 3 to entry 2, and so
    ///                 on up to entry 8 to entry 7, and then the new colour lands in entry 8. The wall's colours walk down
    ///                 through entries 7 to 1 behind it.
    ///             </description>
    ///         </item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$E289</c>.</description></item>
    ///     </list>
    /// </remarks>
    private static void Apply(GamePalette gamePalette, Process process, byte value)
    {
        if (process.PaletteIndex != LoopPaletteIndex)
        {
            gamePalette.SetPaletteIndexValue(process.PaletteIndex, value);
            return;
        }

        for (var paletteIndex = 1; paletteIndex <= 7; paletteIndex++) gamePalette.SetPaletteIndexValue(paletteIndex, (byte)gamePalette.GetPaletteIndexValue(paletteIndex + 1));

        gamePalette.SetPaletteIndexValue(8, value);
    }

    /// <summary>One of the page's colour processes.</summary>
    private sealed class Process
    {
        /// <summary>How many fiftieths of a second pass between one cycle of the process and the next.</summary>
        public int ArcadeFramesPerCycle;

        /// <summary>What the process's timer has gathered so far, in clock units.</summary>
        public int ClockUnits;

        /// <summary>The place in the colour table the process is at.</summary>
        public int ColourIndex;

        /// <summary>The colours the process goes through.</summary>
        public byte[] ColourTable = [];

        /// <summary>The palette entry the process writes, or <see cref="LoopPaletteIndex" /> for the wall's process.</summary>
        public int PaletteIndex;
    }
}
