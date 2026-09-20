namespace Robotron2084.Rendering;

/// <summary>
/// The five colour processes the high score page starts for itself — RRTABLE's
/// <c>MAKP LOOPP</c> / <c>MAKP DECAZ</c> / <c>MAKP COLA</c> / <c>MAKP COLC</c> /
/// <c>MAKP COLD</c> (notes §98.5). This is why the arcade's page is never still:
///
/// | process | slot | table | step | what it does on screen |
/// |---------|------|-------|------|------------------------|
/// | LOOPP   | 1-8  | COLTAB `$E2FE` | 3 ROM frames | shifts the wall's next colour into slot 8 and pushes the old one down a slot — slot 7 is the headers ("ROBOTRON HEROES"), so they trail the wall by one step |
/// | DECAZ   | 9    | CATAB `$E2C2` from index 7 | 4 ROM frames | today's list, out of phase: `$07` to white and back |
/// | COLA    | 10   | CATAB from index 0 | 4 ROM frames | the top entry `( WILLY ELKTRIX )`, in phase |
/// | COLC    | 12   | CCTAB `$E2D1` from index 7 | 4 ROM frames | the "you are here" highlight, out of phase |
/// | COLD    | 13   | CCTAB from index 0 | 4 ROM frames | the top entry's highlight, in phase |
///
/// The two tables that ramp end in a `$00` terminator that sends the process back to
/// the table's START (the ROM's <c>COLA3</c>/<c>RCS3</c> reload), which is what makes
/// them pulse rather than stop; `LOOPP` simply starts COLTAB again at its end.
///
/// **Timing.** The steps above are ROM frames and a ROM frame is 6/5 of a port tick, so
/// these run on the exact-6ths accumulator the entity bodies use (notes §52): a
/// 3-frame process steps every 3.6 ticks, a 4-frame one every 4.8. Until notes §65 the
/// in-game processes were counted in port ticks, which ran them 20% fast — this one is
/// exact from the start.
///
/// The page also CLEARS the palette (RRTABLE's <c>FRAMER</c> zeroes `PCRAM`..`PCRAM+15`)
/// before the processes fill their slots, so the arcade's page comes up from black; and
/// slots 10, 12 and 13 are taken over from the in-game animator, which the ROM does by
/// killing its colour processes with the game (notes §4.12).
/// </summary>
public sealed class HighScorePalette
{
    /// <summary>A port tick advances a ROM-frame clock by 5 sixths (notes §52).</summary>
    private const int SixthsPerPortTick = 5;

    /// <summary>One ROM frame in sixths of a port tick.</summary>
    private const int SixthsPerRomFrame = 6;

    /// <summary>The slot <c>LOOPP</c> shifts through — its value is the wall's own slot.</summary>
    private const int LoopSlot = 0;

    /// <summary>`COLTAB` (RRTABLE `$E2FE`): the wall's colour walk, 21 steps then round again.</summary>
    public static readonly byte[] CycleTable =
    [
        0x37, 0x2F, 0x27, 0x1F, 0x17, 0x47, 0x47, 0x87,
        0x87, 0xC7, 0xC7, 0xC6, 0xC5, 0xCC, 0xCB, 0xCA,
        0xC0, 0xD0, 0x98, 0x38, 0x33,
    ];

    /// <summary>`CATAB` (RRTABLE `$E2C2`): the two list ramps — dark red, to white, and back.</summary>
    public static readonly byte[] RampTable =
    [
        0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
        0x57, 0xA7, 0xFF, 0xFF, 0xA7, 0x57, 0x00,
    ];

    /// <summary>`CCTAB` (RRTABLE `$E2D1`): the highlight ramp — white, down to `$C0` and back.</summary>
    public static readonly byte[] AccentTable =
    [
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xE4,
        0xD2, 0xC0, 0xC0, 0xC0, 0xD2, 0xE4, 0x00,
    ];

    /// <summary>The slots the page takes over from the in-game colour animator.</summary>
    public static readonly int[] OwnedSlots = [10, 12, 13];

    /// <summary>
    /// The first of the four processes the ROM starts AFTER the page is printed
    /// (<c>MAKP DECAZ/COLA/COLC/COLD</c>); everything before it comes up with the frame.
    /// </summary>
    private const int RampProcessStart = 1;

    private sealed class Process
    {
        public int Slot;
        public byte[] Table = [];
        public int Index;
        public int RomFramesPerStep;
        public int Fifths;
    }

    private readonly Process[] _processes;

    /// <summary>True once the four ramp processes have been started (the ROM's second MAKP group).</summary>
    public bool RampsStarted { get; private set; }

    public HighScorePalette()
    {
        // The ROM's DECAZ and COLC are started with an out-of-phase table pointer
        // (`LDX #CATAB+7` "OUT OF PHASE PLEASE", `LDX #CCTAB+7").
        _processes =
        [
            new() { Slot = LoopSlot, Table = CycleTable, RomFramesPerStep = 3 },
            new() { Slot = 9, Table = RampTable, Index = 7, RomFramesPerStep = 4 },
            new() { Slot = 10, Table = RampTable, RomFramesPerStep = 4 },
            new() { Slot = 12, Table = AccentTable, Index = 7, RomFramesPerStep = 4 },
            new() { Slot = 13, Table = AccentTable, RomFramesPerStep = 4 },
        ];
    }

    /// <summary>
    /// The page's <c>FRAMER</c> clear plus the wall's cycle: the ROM starts
    /// <c>MAKP LOOPP</c> BEFORE it draws the frame, so slots 1-8 come alive first and the
    /// palette comes up black (FRAMER zeroes all sixteen slots). The four ramps the
    /// text uses do not exist yet — see <see cref="StartRamps"/>.
    /// </summary>
    public void Start(GamePalette palette)
    {
        for (int slot = 0; slot <= 15; slot++)
        {
            palette.SetSlot(slot, 0x00);
        }

        foreach (int slot in OwnedSlots)
        {
            palette.SuspendSlot(slot);
        }

        Apply(palette, _processes[0], _processes[0].Table[_processes[0].Index]);
    }

    /// <summary>
    /// The ROM's second <c>MAKP</c> group — <c>DECAZ</c>/<c>COLA</c>/<c>COLC</c>/<c>COLD</c>,
    /// started once the page has finished printing and before the 600-frame hold. Until
    /// this runs, slots 9/10/12/13 are the zeroes FRAMER left behind, which is why the
    /// printed rows are invisible for their first few frames.
    /// </summary>
    public void StartRamps(GamePalette palette)
    {
        if (RampsStarted)
        {
            return;
        }

        RampsStarted = true;
        for (int i = RampProcessStart; i < _processes.Length; i++)
        {
            Apply(palette, _processes[i], _processes[i].Table[_processes[i].Index]);
        }
    }

    /// <summary>Advances every process by one port tick (call once per Update).</summary>
    public void Update(GamePalette palette)
    {
        for (int i = 0; i < _processes.Length; i++)
        {
            if (i >= RampProcessStart && !RampsStarted)
            {
                continue; // the ramps do not exist until the page has finished printing
            }

            Process process = _processes[i];
            process.Fifths += SixthsPerPortTick;
            int period = process.RomFramesPerStep * SixthsPerRomFrame;
            if (process.Fifths < period)
            {
                continue;
            }

            process.Fifths -= period;
            process.Index = Advance(process.Table, process.Index);
            Apply(palette, process, process.Table[process.Index]);
        }
    }

    /// <summary>
    /// The ROM's processes die with the page, and the page after it (<c>FAMPAG</c>) sets
    /// its own slots. The port stands the ROM's default CRTAB values back up and lets the
    /// in-game animator have slots 10-15 again.
    /// </summary>
    public void Stop(GamePalette palette)
    {
        for (int slot = 0; slot <= 15; slot++)
        {
            palette.SetSlot(slot, GamePalette.DefaultSlots[slot]);
        }

        foreach (int slot in OwnedSlots)
        {
            palette.ResumeSlot(slot);
        }
    }

    private static void Apply(GamePalette palette, Process process, byte value)
    {
        if (process.Slot != LoopSlot)
        {
            palette.SetSlot(process.Slot, value);
            return;
        }

        // OUTCOL: PCRAM+1 ← +2, +2 ← +3 … +7 ← +8, then the new byte lands in +8 — a
        // shift register, so the wall's colour history walks down slots 7…1 behind it.
        for (int slot = 1; slot <= 7; slot++)
        {
            palette.SetSlot(slot, (byte)palette.SlotValue(slot + 1));
        }

        palette.SetSlot(8, value);
    }

    /// <summary>
    /// The ROM's table walk: take the next byte, but a `$00` terminator sends the process
    /// back to the START of the table (never forward past it).
    /// </summary>
    private static int Advance(byte[] table, int index)
    {
        int next = (index + 1) % table.Length;
        for (int guard = 0; guard < table.Length && table[next] == 0; guard++)
        {
            next = (next + 1) % table.Length;
        }

        return next;
    }
}
