using Robotron2084.Core;

namespace Robotron2084.Palette;

/// <summary>
///     The five colour processes the high score page starts for itself — RRTABLE's
///     <c>MAKP LOOPP</c> / <c>MAKP DECAZ</c> / <c>MAKP COLA</c> / <c>MAKP COLC</c> /
///     <c>MAKP COLD</c> (notes §98.5). This is why the arcade's page is never still:
///     | process | palette index | table | step | what it does on screen |
///     |---------|------|-------|------|------------------------|
///     | LOOPP   | 1-8  | COLTAB `$E2FE` | 3 fiftieths of a second | shifts the wall's next colour into palette index 8 and pushes
///     the old one down a palette index — palette index 7 is the headers ("ROBOTRON HEROES"), so they trail the wall by one step |
///     | DECAZ   | 9    | CATAB `$E2C2` from index 7 | 4 fiftieths of a second | today's list, out of phase: `$07` to
///     white and back |
///     | COLA    | 10   | CATAB from index 0 | 4 fiftieths of a second | the top entry `( WILLY ELKTRIX )`, in phase |
///     | COLC    | 12   | CCTAB `$E2D1` from index 7 | 4 fiftieths of a second | the "you are here" highlight, out of
///     phase |
///     | COLD    | 13   | CCTAB from index 0 | 4 fiftieths of a second | the top entry's highlight, in phase |
///     The two tables that ramp end in a `$00` terminator that sends the process back to
///     the table's START (the ROM's <c>COLA3</c>/<c>RCS3</c> reload), which is what makes
///     them pulse rather than stop; `LOOPP` simply starts COLTAB again at its end.
///     **Timing.** The steps above are fiftieths of a second and a fiftieth of a second is 6/5 of a port tick, so
///     these run on the clock-unit accumulator the entity bodies use (notes §52): a
///     3-frame process steps every 3.6 ticks, a 4-frame one every 4.8. Until notes §65 the
///     in-game processes were counted in port ticks, which ran them 20% fast — this one is
///     exact from the start.
///     The page also CLEARS the palette (RRTABLE's <c>FRAMER</c> zeroes `PCRAM`..`PCRAM+15`)
///     before the processes fill their palette indices, so the arcade's page comes up from black; and
///     palette indices 10, 12 and 13 are taken over from the in-game animator, which the ROM does by
///     killing its colour processes with the game (notes §4.12).
/// </summary>
public sealed class HighScorePalette
{
    /// <summary>`CCTAB` (RRTABLE `$E2D1`): the highlight ramp — white, down to `$C0` and back.</summary>
    public static readonly byte[] AccentTable =
    [
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xE4,
        0xD2, 0xC0, 0xC0, 0xC0, 0xD2, 0xE4, 0x00
    ];

    /// <summary>`COLTAB` (RRTABLE `$E2FE`): the wall's colour walk, 21 steps then round again.</summary>
    public static readonly byte[] CycleTable =
    [
        0x37, 0x2F, 0x27, 0x1F, 0x17, 0x47, 0x47, 0x87,
        0x87, 0xC7, 0xC7, 0xC6, 0xC5, 0xCC, 0xCB, 0xCA,
        0xC0, 0xD0, 0x98, 0x38, 0x33
    ];

    /// <summary>The palette indices the page takes over from the in-game colour animator.</summary>
    public static readonly int[] OwnedPaletteIndices = [10, 12, 13];

    /// <summary>`CATAB` (RRTABLE `$E2C2`): the two list ramps — dark red, to white, and back.</summary>
    public static readonly byte[] RampTable =
    [
        0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
        0x57, 0xA7, 0xFF, 0xFF, 0xA7, 0x57, 0x00
    ];

    /// <summary>The palette index <c>LOOPP</c> shifts through — its value is the wall's own palette index.</summary>
    private const int LoopPaletteIndex = 0;

    /// <summary>
    ///     The first of the four processes the ROM starts AFTER the page is printed
    ///     (<c>MAKP DECAZ/COLA/COLC/COLD</c>); everything before it comes up with the frame.
    /// </summary>
    private const int RampProcessStart = 1;

    private readonly Process[] _processes;

    public HighScorePalette()
    {
        // The ROM's DECAZ and COLC are started with an out-of-phase table pointer
        // (`LDX #CATAB+7` "OUT OF PHASE PLEASE", `LDX #CCTAB+7").
        _processes =
        [
            new Process { PaletteIndex = LoopPaletteIndex, ColourTable = CycleTable, ArcadeFramesPerStep = 3 },
            new Process { PaletteIndex = 9, ColourTable = RampTable, ColourIndex = 7, ArcadeFramesPerStep = 4 },
            new Process { PaletteIndex = 10, ColourTable = RampTable, ArcadeFramesPerStep = 4 },
            new Process { PaletteIndex = 12, ColourTable = AccentTable, ColourIndex = 7, ArcadeFramesPerStep = 4 },
            new Process { PaletteIndex = 13, ColourTable = AccentTable, ArcadeFramesPerStep = 4 }
        ];
    }

    /// <summary>True once the four ramp processes have been started (the ROM's second MAKP group).</summary>
    public bool RampsStarted { get; private set; }

    /// <summary>
    ///     The page's <c>FRAMER</c> clear plus the wall's cycle: the ROM starts
    ///     <c>MAKP LOOPP</c> BEFORE it draws the frame, so palette indices 1-8 come alive first and the
    ///     palette comes up black (FRAMER zeroes all sixteen palette indices). The four ramps the
    ///     text uses do not exist yet — see <see cref="StartRamps" />.
    /// </summary>
    public void Start(GamePalette palette)
    {
        for (var paletteIndex = 0; paletteIndex <= 15; paletteIndex++) palette.SetPaletteIndexValue(paletteIndex, 0x00);

        foreach (var paletteIndex in OwnedPaletteIndices) palette.SuspendPaletteIndex(paletteIndex);

        Apply(palette, _processes[0], _processes[0].ColourTable[_processes[0].ColourIndex]);
    }

    /// <summary>
    ///     The ROM's second <c>MAKP</c> group — <c>DECAZ</c>/<c>COLA</c>/<c>COLC</c>/<c>COLD</c>,
    ///     started once the page has finished printing and before the 600-frame hold. Until
    ///     this runs, palette indices 9/10/12/13 are the zeroes FRAMER left behind, which is why the
    ///     printed rows are invisible for their first few frames.
    /// </summary>
    public void StartRamps(GamePalette palette)
    {
        if (RampsStarted) return;

        RampsStarted = true;
        for (var i = RampProcessStart; i < _processes.Length; i++)
            Apply(palette, _processes[i], _processes[i].ColourTable[_processes[i].ColourIndex]);
    }

    /// <summary>
    ///     The ROM's processes die with the page, and the page after it (<c>FAMPAG</c>) sets
    ///     its own palette indices. The port stands the ROM's default CRTAB values back up and lets the
    ///     in-game animator have palette indices 10-15 again.
    /// </summary>
    public void Stop(GamePalette palette)
    {
        for (var paletteIndex = 0; paletteIndex <= 15; paletteIndex++) palette.SetPaletteIndexValue(paletteIndex, GamePalette.DefaultPaletteValues[paletteIndex]);

        foreach (var paletteIndex in OwnedPaletteIndices) palette.ResumePaletteIndex(paletteIndex);
    }

    /// <summary>Advances every process by one port tick (call once per Update).</summary>
    public void Update(GamePalette palette)
    {
        for (var i = 0; i < _processes.Length; i++)
        {
            if (i >= RampProcessStart &&
                !RampsStarted) continue; // the ramps do not exist until the page has finished printing

            var process = _processes[i];
            process.ClockUnits += ArcadeClock.UnitsPerPortTick;
            var period = ArcadeClock.ToClockUnits(process.ArcadeFramesPerStep);
            if (process.ClockUnits < period) continue;

            process.ClockUnits -= period;
            process.ColourIndex = Advance(process.ColourTable, process.ColourIndex);
            Apply(palette, process, process.ColourTable[process.ColourIndex]);
        }
    }

    /// <summary>
    ///     The ROM's table walk: take the next byte, but a `$00` terminator sends the process
    ///     back to the START of the table (never forward past it).
    /// </summary>
    private static int Advance(byte[] table, int index)
    {
        var next = (index + 1) % table.Length;
        for (var guard = 0; guard < table.Length && table[next] == 0; guard++) next = (next + 1) % table.Length;

        return next;
    }

    private static void Apply(GamePalette palette, Process process, byte value)
    {
        if (process.PaletteIndex != LoopPaletteIndex)
        {
            palette.SetPaletteIndexValue(process.PaletteIndex, value);
            return;
        }

        // OUTCOL: PCRAM+1 ← +2, +2 ← +3 … +7 ← +8, then the new byte lands in +8 — a
        // shift register, so the wall's colour history walks down palette indices 7…1 behind it.
        for (var paletteIndex = 1; paletteIndex <= 7; paletteIndex++) palette.SetPaletteIndexValue(paletteIndex, (byte)palette.GetPaletteIndexValue(paletteIndex + 1));

        palette.SetPaletteIndexValue(8, value);
    }

    private sealed class Process
    {
        public int ArcadeFramesPerStep;
        public int ClockUnits;
        public int ColourIndex;
        public byte[] ColourTable = [];
        public int PaletteIndex;
    }
}
