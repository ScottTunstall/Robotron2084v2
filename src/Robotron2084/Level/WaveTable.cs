namespace Robotron2084.Level;

/// <summary>The arcade's table of waves: for each of the first forty, how many of each robot there are, and how fast and how often they act.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c> <c>GETWV</c>, which reads the table
/// when a wave begins</item>
/// <item>Disassembly: the counts at <c>$2E24</c> and the settings at <c>$2C20</c>, read by
/// <c>$2B7C</c></item>
/// </list> The values are copied byte for byte from the arcade's ROM and checked
/// against <c>ref/robowaves.md</c> (notes §11.1 and §11.2). The counts are nine columns, one for each
/// kind of thing, of forty waves each. The settings are twelve records, each a multiplier, a minimum
/// and a maximum followed by forty values. The arrays here hold the values for the recommended
/// difficulty. A wave after the fortieth takes away twenty, again and again, until it is forty or
/// less, so the second half of the table repeats for ever.</item>
/// </list>
/// </remarks>
public static class WaveTable
{
    /// <summary>How many fiftieths of a second a brain waits after each beat, for each wave. A smaller number is a faster brain.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>The interval between beats is this plus the frame the beat itself takes. Original source:
    /// <c>RRB10.ASM</c> <c>BRNSPD</c>.</item>
    /// <item>Disassembly: <c>$BE63</c>.</item>
    /// </list>
    /// </remarks>
    public static readonly int[] BrainBeatWaitRomFrames =
    [
        8, 8, 8, 8, 8, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6
    ];

    /// <summary>The longest a brain waits between cruise missiles, for each wave.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>BSHTIM</c>.</item>
    /// <item>Disassembly: <c>$BE62</c>.</item>
    /// </list>
    /// </remarks>
    public static readonly int[] BrainFireDelay =
    [
        64, 64, 64, 64, 64, 40, 40, 38, 38, 38, 38, 38, 38, 38, 38, 38, 36, 36, 36, 36, 32, 32, 32, 32, 32, 32, 32, 30, 30, 30, 30, 30, 25, 25, 25, 25, 25, 25, 25, 25
    ];

    /// <summary>How many brains each wave has.</summary>
    /// <remarks>Disassembly: the counts at <c>$2E24</c>.</remarks>
    public static readonly int[] Brains =
    [
        0, 0, 0, 0, 15, 0, 0, 0, 0, 20, 0, 0, 0, 0, 20, 0, 0, 0, 0, 20, 0, 0, 0, 0, 21, 0, 0, 0, 0, 22, 0, 0, 0, 0, 23, 0, 0, 0, 0, 25
    ];

    /// <summary>How many Daddies each wave has.</summary>
    /// <remarks>Disassembly: the counts at <c>$2E24</c>.</remarks>
    public static readonly int[] Daddies =
    [
        1, 1, 2, 2, 0, 3, 4, 3, 3, 22, 3, 3, 3, 5, 0, 3, 3, 3, 3, 8, 3, 3, 3, 3, 0, 3, 3, 3, 3, 25, 3, 3, 3, 3, 0, 3, 3, 3, 3, 10
    ];

    /// <summary>How many electrodes each wave has.</summary>
    /// <remarks>Disassembly: the counts at <c>$2E24</c>.</remarks>
    public static readonly int[] Electrodes =
    [
        5, 15, 25, 25, 20, 25, 0, 25, 0, 20, 25, 0, 25, 5, 20, 25, 0, 25, 0, 20, 25, 0, 25, 0, 20, 25, 0, 25, 0, 20, 25, 0, 25, 0, 15, 25, 0, 25, 0, 15
    ];

    /// <summary>How long an enforcer waits between sparks, for each wave. A smaller number is faster fire.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>ENSTIM</c>.</item>
    /// <item>Disassembly: <c>$BE5F</c>.</item>
    /// </list>
    /// </remarks>
    public static readonly int[] EnforcerFireDelay =
    [
        30, 28, 26, 24, 22, 20, 18, 18, 16, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 15, 15, 15, 15, 15, 15, 15, 15, 15, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14
    ];

    /// <summary>The longest a grunt waits between moves, in beats, for each wave. A smaller number is a faster grunt.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Each grunt waits a random time up to this, so the grunts do not all move at once (notes §29).
    /// Original source: <c>RRP8.ASM</c> <c>ROBSPD</c>.</item>
    /// <item>Disassembly: <c>$BE5C</c>.</item>
    /// </list>
    /// </remarks>
    public static readonly int[] GruntMoveDelay =
    [
        20, 15, 15, 15, 15, 15, 15, 15, 15, 15, 14, 14, 14, 14, 14, 13, 13, 13, 13, 13, 14, 14, 14, 14, 14, 14, 13, 13, 13, 13, 13, 13, 12, 12, 12, 12, 12, 12, 15, 12
    ];

    /// <summary>How many grunts each wave has.</summary>
    /// <remarks>Disassembly: the counts at <c>$2E24</c>.</remarks>
    public static readonly int[] Grunts =
    [
        15, 17, 22, 34, 20, 32, 0, 35, 60, 25, 35, 0, 35, 27, 25, 35, 0, 35, 70, 25, 35, 0, 35, 0, 25, 35, 0, 35, 75, 25, 35, 0, 35, 30, 27, 35, 0, 35, 80, 30
    ];

    /// <summary>The fewest beats that the grunts' speed-ups may bring a grunt's longest wait down to, for each wave.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRP8.ASM</c> <c>RMXSPD</c>.</item>
    /// <item>Disassembly: <c>$BE5D</c>.</item>
    /// </list>
    /// </remarks>
    public static readonly int[] GruntSpeedFloor =
    [
        9, 7, 6, 5, 5, 5, 5, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 3, 3, 4, 3, 3, 3, 3, 3, 3, 3, 3, 3, 4, 3
    ];

    /// <summary>How many hulks each wave has.</summary>
    /// <remarks>Disassembly: the counts at <c>$2E24</c>.</remarks>
    public static readonly int[] Hulks =
    [
        0, 5, 6, 7, 0, 7, 12, 8, 4, 0, 8, 13, 8, 20, 2, 3, 14, 8, 3, 2, 8, 15, 8, 13, 1, 8, 16, 8, 4, 1, 8, 16, 8, 25, 2, 8, 16, 8, 6, 2
    ];

    /// <summary>How many fiftieths of a second pass between a hulk's beats, for each wave. A smaller number is a faster hulk.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HLKSPD</c>.</item>
    /// <item>Disassembly: <c>$BE61</c>.</item>
    /// </list>
    /// </remarks>
    public static readonly int[] HulkBeatIntervalRomFrames =
    [
        8, 8, 7, 7, 7, 7, 7, 6, 6, 6, 6, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5
    ];

    /// <summary>Twice the most that a spheroid or quark may drop, for each wave. Each rolls a number up to this when it is made, and drops half of it, rounded up.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>ENFNUM</c>.</item>
    /// <item>Disassembly: <c>$BE5E</c>.</item>
    /// </list>
    /// </remarks>
    public static readonly int[] MaxDropsX2 =
    [
        10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11
    ];

    /// <summary>How many Mikeys each wave has.</summary>
    /// <remarks>Disassembly: the counts at <c>$2E24</c>.</remarks>
    public static readonly int[] Mikeys =
    [
        0, 1, 2, 2, 1, 3, 4, 3, 3, 0, 3, 3, 3, 5, 22, 3, 3, 3, 3, 8, 3, 3, 3, 3, 1, 3, 3, 3, 3, 0, 3, 3, 3, 3, 25, 3, 3, 3, 3, 10
    ];

    /// <summary>How many Mommies each wave has.</summary>
    /// <remarks>Disassembly: the counts at <c>$2E24</c>.</remarks>
    public static readonly int[] Mommies =
    [
        1, 1, 2, 2, 15, 3, 4, 3, 3, 0, 3, 3, 3, 5, 0, 3, 3, 3, 3, 8, 3, 3, 3, 3, 25, 3, 3, 3, 3, 0, 3, 3, 3, 3, 0, 3, 3, 3, 3, 10
    ];

    /// <summary>How long a quark waits before dropping a tank, for each wave. It waits a random time up to this at first, and up to half of it plus one after each drop.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRTK4.ASM</c> <c>TDPTIM</c>.</item>
    /// <item>Disassembly: <c>$BE66</c>.</item>
    /// </list>
    /// </remarks>
    public static readonly int[] QuarkDropDelay =
    [
        16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 15, 15, 15, 15, 15, 15, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14
    ];

    /// <summary>How many quarks each wave has.</summary>
    /// <remarks>Disassembly: the counts at <c>$2E24</c>.</remarks>
    public static readonly int[] Quarks =
    [
        0, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 12, 0, 0, 0, 0, 12, 0, 0, 0, 0, 12, 0, 7, 0, 0, 12, 1, 1, 1, 1, 13, 1, 2, 2, 2, 14, 2, 1, 1
    ];

    /// <summary>How fast a quark may drift, for each wave. A bigger number is a faster quark.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRTK4.ASM</c> <c>SQSPD</c>.</item>
    /// <item>Disassembly: <c>$BE67</c>.</item>
    /// </list>
    /// </remarks>
    public static readonly int[] QuarkSpeedCap =
    [
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 56, 56, 56, 56, 56, 56, 56, 56, 56, 56, 56, 56, 56, 56, 56, 56, 60, 60, 60, 60, 60, 60, 60, 60, 60, 60, 60, 60
    ];

    /// <summary>How fast a tank shell flies, and how well it is aimed, for each wave. A bigger number is a faster shell (notes §11.5).</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRTK4.ASM</c> <c>SHLSPD</c>.</item>
    /// <item>Disassembly: <c>$BE65</c>.</item>
    /// </list>
    /// </remarks>
    public static readonly int[] ShellSpeed =
    [
        176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 176, 184, 184, 184, 184, 184, 184, 184, 184, 184, 184, 192, 192, 192, 192, 192, 192, 192, 192, 192, 192
    ];

    /// <summary>How long a spheroid waits before dropping an enforcer, for each wave. It waits a random time up to this each time.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CDPTIM</c>.</item>
    /// <item>Disassembly: <c>$BE60</c>.</item>
    /// </list>
    /// </remarks>
    public static readonly int[] SpheroidDropDelay =
    [
        30, 28, 26, 24, 30, 20, 18, 16, 18, 25, 12, 12, 12, 25, 25, 12, 12, 12, 18, 20, 14, 14, 14, 14, 14, 25, 14, 14, 18, 25, 12, 12, 12, 12, 25, 12, 12, 12, 18, 20
    ];

    /// <summary>How many spheroids each wave has.</summary>
    /// <remarks>Disassembly: the counts at <c>$2E24</c>.</remarks>
    public static readonly int[] Spheroids =
    [
        0, 1, 3, 4, 1, 4, 0, 5, 5, 1, 5, 0, 5, 2, 1, 5, 0, 5, 5, 2, 5, 0, 5, 6, 1, 5, 0, 5, 5, 1, 5, 0, 5, 2, 1, 5, 0, 5, 5, 1
    ];

    /// <summary>How many beats a tank waits between shells, for each wave. A smaller number is faster fire.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>A random number of beats is added to it before a tank's first shell. Original source:
    /// <c>RRTK4.ASM</c> <c>TNKSHT</c>.</item>
    /// <item>Disassembly: <c>$BE64</c>.</item>
    /// </list>
    /// </remarks>
    public static readonly int[] TankFireDelay =
    [
        32, 32, 32, 32, 32, 32, 32, 30, 30, 30, 30, 30, 30, 28, 28, 28, 28, 28, 28, 28, 30, 30, 30, 30, 30, 30, 28, 28, 28, 28, 28, 26, 26, 26, 26, 26, 24, 24, 24, 24
    ];

    /// <summary>How many waves a wave after the last one goes back by, each time it is too big for the table.</summary>
    private const int RepeatedCount = 20;

    /// <summary>How many waves the table has.</summary>
    private const int WaveCount = 40;

    /// <summary>Every array in the table, so they can be checked together.</summary>
    private static readonly int[][] AllTables =
    [
        Grunts, Electrodes, Mommies, Daddies, Mikeys, Hulks, Brains, Spheroids, Quarks,
        GruntMoveDelay, GruntSpeedFloor, MaxDropsX2, EnforcerFireDelay, SpheroidDropDelay,
        HulkBeatIntervalRomFrames, BrainFireDelay, BrainBeatWaitRomFrames, TankFireDelay, ShellSpeed, QuarkDropDelay, QuarkSpeedCap,
    ];

    /// <summary>Checks that every array has exactly one value for each wave.</summary>
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

    /// <summary>Gets the table's row for a wave. A wave after the fortieth gets the row of the matching wave in the second half.</summary>
    /// <param name="waveNumber">The wave number, starting at 1.</param>
    public static WaveParameters GetParameters(int waveNumber)
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
            HulkBeatIntervalRomFrames[i],
            BrainFireDelay[i],
            BrainBeatWaitRomFrames[i],
            TankFireDelay[i],
            ShellSpeed[i],
            QuarkDropDelay[i],
            QuarkSpeedCap[i]);
    }

    /// <summary>Works out which row of the table a wave uses: its own up to the fortieth, then the second half over and over.</summary>
    /// <param name="waveNumber">The wave number, starting at 1.</param>
    /// <returns>The wave number from 1 to 40.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>GETWV</c>, which subtracts 20 while the wave is over 40.</item>
    /// <item>Disassembly: <c>$2B7C</c>.</item>
    /// </list>
    /// </remarks>
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
