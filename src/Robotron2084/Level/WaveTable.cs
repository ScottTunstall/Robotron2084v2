namespace Robotron2084.Level;

/// <summary>
/// The arcade's per-wave data, extracted byte-for-byte from the Release-5
/// ROM (robotron64k.bin) and verified 2026-09-12 (arcade-fidelity-notes
/// §11.1/§11.2):
///
/// <list type="bullet">
/// <item>Entity counts @ $2E24: 9 columns (grunts, electrodes, mommies,
/// daddies, mikeys, hulks, brains, spheroids, quarks) × 40 waves, verified
/// identical to the decoded table in ref/robowaves.md (all 360 values).</item>
/// <item>Difficulty-setting values @ $2C20: 12 consecutive 43-byte records
/// ([multiplier][min][max][40 values]); at the default (recommended)
/// difficulty the game stores the raw table value, which is what these
/// arrays hold.</item>
/// </list>
///
/// Wave numbering (ROM $2B7C): waves 1-40 are unique; a wave number above 40
/// repeatedly subtracts 20 until it is 40 or less — i.e. waves 41+ replay
/// waves 21-40 forever (wave 41 = wave 21, wave 60 = wave 40, wave 61 =
/// wave 21, ...).
/// </summary>
public static class WaveTable
{
    private const int RepeatedCount = 20;
    private const int WaveCount = 40;

    static WaveTable()
    {
        // Every table must hold exactly one value per wave 1-40.
        foreach (int[] table in AllTables)
        {
            if (table.Length != WaveCount)
            {
                throw new InvalidOperationException(
                    $"wave table must have exactly {WaveCount} entries, got {table.Length}");
            }
        }
    }


    // ---- Entity counts (ROM $2E24+, column-major 40-byte blocks) ----

    public static readonly int[] Grunts =
    [
        15, 17, 22, 34, 20, 32, 0, 35, 60, 25, 35, 0, 35, 27, 25, 35, 0, 35, 70, 25, 35, 0, 35, 0, 25, 35, 0, 35, 75, 25, 35, 0, 35, 30, 27, 35, 0, 35, 80, 30
    ];

    public static readonly int[] Electrodes =
    [
        5, 15, 25, 25, 20, 25, 0, 25, 0, 20, 25, 0, 25, 5, 20, 25, 0, 25, 0, 20, 25, 0, 25, 0, 20, 25, 0, 25, 0, 20, 25, 0, 25, 0, 15, 25, 0, 25, 0, 15
    ];

    public static readonly int[] Mommies =
    [
        1, 1, 2, 2, 15, 3, 4, 3, 3, 0, 3, 3, 3, 5, 0, 3, 3, 3, 3, 8, 3, 3, 3, 3, 25, 3, 3, 3, 3, 0, 3, 3, 3, 3, 0, 3, 3, 3, 3, 10
    ];

    public static readonly int[] Daddies =
    [
        1, 1, 2, 2, 0, 3, 4, 3, 3, 22, 3, 3, 3, 5, 0, 3, 3, 3, 3, 8, 3, 3, 3, 3, 0, 3, 3, 3, 3, 25, 3, 3, 3, 3, 0, 3, 3, 3, 3, 10
    ];

    public static readonly int[] Mikeys =
    [
        0, 1, 2, 2, 1, 3, 4, 3, 3, 0, 3, 3, 3, 5, 22, 3, 3, 3, 3, 8, 3, 3, 3, 3, 1, 3, 3, 3, 3, 0, 3, 3, 3, 3, 25, 3, 3, 3, 3, 10
    ];

    public static readonly int[] Hulks =
    [
        0, 5, 6, 7, 0, 7, 12, 8, 4, 0, 8, 13, 8, 20, 2, 3, 14, 8, 3, 2, 8, 15, 8, 13, 1, 8, 16, 8, 4, 1, 8, 16, 8, 25, 2, 8, 16, 8, 6, 2
    ];

    public static readonly int[] Brains =
    [
        0, 0, 0, 0, 15, 0, 0, 0, 0, 20, 0, 0, 0, 0, 20, 0, 0, 0, 0, 20, 0, 0, 0, 0, 21, 0, 0, 0, 0, 22, 0, 0, 0, 0, 23, 0, 0, 0, 0, 25
    ];

    public static readonly int[] Spheroids =
    [
        0, 1, 3, 4, 1, 4, 0, 5, 5, 1, 5, 0, 5, 2, 1, 5, 0, 5, 5, 2, 5, 0, 5, 6, 1, 5, 0, 5, 5, 1, 5, 0, 5, 2, 1, 5, 0, 5, 5, 1
    ];

    public static readonly int[] Quarks =
    [
        0, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 12, 0, 0, 0, 0, 12, 0, 0, 0, 0, 12, 0, 7, 0, 0, 12, 1, 1, 1, 1, 13, 1, 2, 2, 2, 14, 2, 1, 1
    ];

    // ---- Difficulty-setting values (ROM $2C20+ 43-byte records, values only) ----

    /// <summary>ROBSPD @ $BE5C — grunt stagger re-roll limit: step countdown = RND(1..N) 4-vblank bodies (notes §29).</summary>
    public static readonly int[] GruntMoveDelay =
    [
        20, 15, 15, 15, 15, 15, 15, 15, 15, 15, 14, 14, 14, 14, 14, 13, 13, 13, 13, 13, 14, 14, 14, 14, 14, 14, 13, 13, 13, 13, 13, 13, 12, 12, 12, 12, 12, 12, 15, 12
    ];

    /// <summary>RMXSPD @ $BE5D — floor for the per-grunt-kill speedup (min move delay).</summary>
    public static readonly int[] GruntSpeedFloor =
    [
        9, 7, 6, 5, 5, 5, 5, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 3, 3, 4, 3, 3, 3, 3, 3, 3, 3, 3, 3, 4, 3
    ];

    /// <summary>ENFNUM @ $BE5E — drops per spheroid/quark: spawn-time RND(ENFNUM) halved (rounded up).</summary>
    public static readonly int[] MaxDropsX2 =
    [
        10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11
    ];

    /// <summary>ENSTIM @ $BE5F — enforcer spark fire delay.</summary>
    public static readonly int[] EnforcerFireDelay =
    [
        30, 28, 26, 24, 22, 20, 18, 18, 16, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 15, 15, 15, 15, 15, 15, 15, 15, 15, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14
    ];

    /// <summary>CDPTIM @ $BE60 — spheroid enforcer-drop delay (RND of this at spawn/re-arm).</summary>
    public static readonly int[] SpheroidDropDelay =
    [
        30, 28, 26, 24, 30, 20, 18, 16, 18, 25, 12, 12, 12, 25, 25, 12, 12, 12, 18, 20, 14, 14, 14, 14, 14, 25, 14, 14, 18, 25, 12, 12, 12, 12, 25, 12, 12, 12, 18, 20
    ];

    /// <summary>HLKSPD @ $BE61 — hulk update rate (lower = faster).</summary>
    public static readonly int[] HulkSpeed =
    [
        8, 8, 7, 7, 7, 7, 7, 6, 6, 6, 6, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5
    ];

    /// <summary>BSHTIM @ $BE62 — brain cruise-missile fire delay.</summary>
    public static readonly int[] BrainFireDelay =
    [
        64, 64, 64, 64, 64, 40, 40, 38, 38, 38, 38, 38, 38, 38, 38, 38, 36, 36, 36, 36, 32, 32, 32, 32, 32, 32, 32, 30, 30, 30, 30, 30, 25, 25, 25, 25, 25, 25, 25, 25
    ];

    /// <summary>BRNSPD @ $BE63 — brain speed (update rate; lower = faster).</summary>
    public static readonly int[] BrainSpeed =
    [
        8, 8, 8, 8, 8, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6
    ];

    /// <summary>TNKSHT @ $BE64 — tank shell fire rate (added to RND(0..31) at each re-arm).</summary>
    public static readonly int[] TankFireDelay =
    [
        32, 32, 32, 32, 32, 32, 32, 30, 30, 30, 30, 30, 30, 28, 28, 28, 28, 28, 28, 28, 30, 30, 30, 30, 30, 30, 28, 28, 28, 28, 28, 26, 26, 26, 26, 26, 24, 24, 24, 24
    ];

    /// <summary>SHLSPD @ $BE65 — shell speed/accuracy setting (used by shell creation; see notes §11.5).</summary>
    public static readonly int[] ShellSpeed =
    [
        176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 184, 184, 184, 184, 184, 184, 184, 184, 184, 184, 192, 192, 192, 192, 192, 192, 192, 192, 192, 192
    ];

    /// <summary>TDPTIM @ $BE66 — quark tank-drop delay (RND of this at spawn; RND(this/2+1) after each drop).</summary>
    public static readonly int[] QuarkDropDelay =
    [
        16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 15, 15, 15, 15, 15, 15, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14
    ];

    /// <summary>SQSPD @ $BE67 — quark movement (destination random bound).</summary>
    public static readonly int[] QuarkMove =
    [
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 56, 56, 56, 56, 56, 56, 56, 56, 56, 56, 56, 56, 56, 56, 56, 56, 60, 60, 60, 60, 60, 60, 60, 60, 60, 60, 60, 60
    ];


    private static readonly int[][] AllTables =
    [
        Grunts, Electrodes, Mommies, Daddies, Mikeys, Hulks, Brains, Spheroids, Quarks,
        GruntMoveDelay, GruntSpeedFloor, MaxDropsX2, EnforcerFireDelay, SpheroidDropDelay,
        HulkSpeed, BrainFireDelay, BrainSpeed, TankFireDelay, ShellSpeed, QuarkDropDelay, QuarkMove,
    ];

    /// <summary>One wave's full parameter row (waves 41+ repeat 21-40, ROM rule).</summary>
    public static WaveParameters ForWave(int waveNumber)
    {
        int wave = ResolveWave(waveNumber);
        int i = wave - 1;
        return new WaveParameters(
            wave,
            Grunts[i],
            Electrodes[i],
            Mommies[i],
            Daddies[i],
            Mikeys[i],
            Hulks[i],
            Brains[i],
            Spheroids[i],
            Quarks[i],
            MaxDropsX2[i],
            GruntMoveDelay[i],
            GruntSpeedFloor[i],
            EnforcerFireDelay[i],
            SpheroidDropDelay[i],
            HulkSpeed[i],
            BrainFireDelay[i],
            BrainSpeed[i],
            TankFireDelay[i],
            ShellSpeed[i],
            QuarkDropDelay[i],
            QuarkMove[i]);
    }

    /// <summary>Resolved wave number after the ROM's 41+ → repeat-21-40 rule.</summary>
    public static int ResolveWave(int waveNumber)
    {
        int wave = Math.Max(1, waveNumber);
        while (wave > WaveCount)
        {
            wave -= RepeatedCount;
        }

        return wave;
    }
}

