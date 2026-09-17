namespace Robotron2084.Rendering;

/// <summary>
/// Replays the ROM's six colour processes (decoded from RRS22.ASM —
/// arcade-fidelity-notes §4.12) as per-game-tick state over a
/// <see cref="GamePalette"/>:
///
/// | slot | process | table | step |
/// |------|---------|-------|------|
/// | 11   | RGB     | 38 07 C0 | 8 ROM frames |
/// | 12   | DECAY   | C0 C0 D0 E0 F0 F8 FA BA 7A 3A 34 2D 1F 17 0F 07 06 05 04 03 02 01 00 | 2 ROM frames |
/// | 14   | BPR     | C0 C1 C2 C3 C4 C5 C6 C7 87 87 47 47 07 07 47 47 87 87 C7 C7 C6 C5 C4 C3 C2 C1 | 1 ROM frame |
/// | 15   | RGOLD   | 07 07 2F | 6 ROM frames |
/// | 13   | LASER   | the 37-step COLTAB ramp (below) | 2 ROM frames |
/// | 10   | LF      | every 2nd ROM frame = FF (white flash); every 6th = a random COLTAB entry | 2/6 ROM frames |
///
/// **Timing.** The step counts above are the ROM's own FRAME counts, and a ROM
/// frame is 6/5 of a port tick, so these run on the same exact-6ths accumulators
/// the entity bodies use (notes §52, §65): a 1-frame process steps every 1.2
/// ticks, a 2-frame one every 2.4, an 8-frame one every 9.6. Until §65 the steps
/// were counted in PORT ticks, which made all six cycles run 20% fast — visible
/// on the player's score digits, the mini man, the posts and the wall.
///
/// Tables loop (the ROM restarts each table at index 0 on its $00 sentinel —
/// equivalent to wrap for these sequences).
/// </summary>
public sealed class PaletteAnimator
{
    /// <summary>A port tick advances a ROM-frame clock by 5 sixths (notes §52).</summary>
    private const int SixthsPerPortTick = 5;

    /// <summary>The ROM's LF flash period in ROM frames (every 3rd flash is the random hue).</summary>
    private const int LaserFlashRomFrames = 2;

    /// <summary>The LASER (slot 13) ramp — also the random-hue table for the LF flash.</summary>
    public static readonly byte[] LaserTab =
    [
        0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F,
        0x37, 0x2F, 0x27, 0x1F, 0x17, 0x47, 0x47, 0x87,
        0x87, 0xC7, 0xC7, 0xC6, 0xC5, 0xCC, 0xCB, 0xCA,
        0xDA, 0xE8, 0xF8, 0xF9, 0xFA, 0xFB, 0xFD, 0xFF,
        0xBF, 0x3F, 0x3E, 0x3C,
    ];

    private sealed class Process
    {
        public int Slot;
        public byte[] Table = [];
        public int RomFramesPerStep = 1;
        public int Fifths;
        public int Index;
    }

    private readonly GamePalette _palette;
    private readonly Random _random;
    private readonly Process[] _processes;
    private int _laserFlashFifths;
    private int _laserFlashStep;

    public PaletteAnimator(GamePalette palette, Random? random = null)
    {
        _palette = palette;
        _random = random ?? new Random();
        _processes =
        [
            new() { Slot = 11, Table = [0x38, 0x07, 0xC0], RomFramesPerStep = 8 },   // RGB
            new() { Slot = 12, Table = [0xC0, 0xC0, 0xD0, 0xE0, 0xF0, 0xF8, 0xFA, 0xBA, 0x7A, 0x3A, 0x34, 0x2D, 0x1F, 0x17, 0x0F, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00], RomFramesPerStep = 2 }, // DECAY
            new() { Slot = 14, Table = [0xC0, 0xC1, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0x87, 0x87, 0x47, 0x47, 0x07, 0x07, 0x47, 0x47, 0x87, 0x87, 0xC7, 0xC7, 0xC6, 0xC5, 0xC4, 0xC3, 0xC2, 0xC1], RomFramesPerStep = 1 }, // BLU-PURP-RED
            new() { Slot = 15, Table = [0x07, 0x07, 0x2F], RomFramesPerStep = 6 },   // RED-GOLD
            new() { Slot = 13, Table = LaserTab, RomFramesPerStep = 2 },             // LASER
        ];
    }

    /// <summary>Advances every process by one game tick (call once per Update).</summary>
    public void Update()
    {
        // LF (slot 10): a white flash every 2 ROM frames, and every 6th frame a
        // random hue from the COLTAB ramp INSTEAD of the white one.
        _laserFlashFifths += SixthsPerPortTick;
        int flashPeriod = LaserFlashRomFrames * 6;
        while (_laserFlashFifths >= flashPeriod)
        {
            _laserFlashFifths -= flashPeriod;
            _laserFlashStep++;
            if (_palette.IsSlotSuspended(10))
            {
                continue; // the death fade or a caller owns slot 10
            }

            _palette.SetSlot(
                10,
                _laserFlashStep % 3 == 0
                    ? LaserTab[_random.Next(LaserTab.Length)]
                    : (byte)0xFF);
        }

        foreach (Process p in _processes)
        {
            // A suspended slot is a KILLED process in the ROM: it does not even
            // advance, so resuming restarts it where it left off.
            if (_palette.IsSlotSuspended(p.Slot))
            {
                continue;
            }

            p.Fifths += SixthsPerPortTick;
            if (p.Fifths < p.RomFramesPerStep * 6)
            {
                continue;
            }

            p.Fifths -= p.RomFramesPerStep * 6;
            _palette.SetSlot(p.Slot, p.Table[p.Index]);
            p.Index = (p.Index + 1) % p.Table.Length;
        }
    }
}
