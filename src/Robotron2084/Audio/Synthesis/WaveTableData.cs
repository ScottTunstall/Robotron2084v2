namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     The wave table sounds' data, copied from the sound board's program: the wave shapes, the pitch
///     patterns they are played through, and the settings that pair them up.
/// </summary>
/// <remarks>
///     Original source: <c>VSNDRM3.SRC</c>, tables <c>GWVTAB</c> (ROM <c>$FD32</c>), <c>GFRTAB</c> (ROM
///     <c>$FF02</c>) and <c>SVTAB</c> (ROM <c>$FE45</c>); each was found byte for byte in the sound ROM.
///     The waves and the patterns are kept as one run of bytes each, as in the ROM, because the program
///     finds a wave by stepping over the ones before it, and a pattern may run on into the next.
/// </remarks>
internal static class WaveTableData
{
    /// <summary>The longest wave in <see cref="Waves" />, in levels (<c>WVELEN</c>, the size of the RAM copy <c>GWTAB</c>).</summary>
    public const int LongestWave = 72;

    /// <summary>The place <c>BONV</c> "LASER BALL BONUS #2" has in <see cref="Vectors" />.</summary>
    public const int LaserBallBonusVector = 13;

    /// <summary>
    ///     <c>WIRDV</c>'s pattern start, which the source writes as the bare number <c>$0D</c> rather than as a
    ///     label, so it lands in <see cref="HundredPointSound" />.
    /// </summary>
    private const int WirdvPatternStart = 0x0D;

    /// <summary><c>BONSND</c> "BONUS SOUND".</summary>
    private static readonly byte[] BonusSound =
        [0xA0, 0x98, 0x90, 0x88, 0x80, 0x78, 0x70, 0x68, 0x60, 0x58, 0x50, 0x44, 0x40];

    /// <summary><c>HBTSND</c> "HUNDRED POINT SOUND".</summary>
    private static readonly byte[] HundredPointSound = [1, 1, 2, 2, 4, 4, 8, 8, 0x10, 0x10, 0x30, 0x60, 0xC0, 0xE0];

    /// <summary><c>SPNSND</c> "SPINNER SOUND".</summary>
    private static readonly byte[] SpinnerSound = [1, 1, 2, 2, 3, 4, 5, 6, 7, 8, 9, 0x0A, 0x0C];

    /// <summary><c>TRBPAT</c> "TURBINE START UP".</summary>
    private static readonly byte[] TurbineStartUp = [0x80, 0x7C, 0x78, 0x74, 0x70, 0x74, 0x78, 0x7C, 0x80];

    /// <summary><c>HBDSND</c> "HEARTBEAT DISTORTO".</summary>
    private static readonly byte[] HeartbeatDistorto =
        [1, 1, 2, 2, 4, 4, 8, 8, 0x10, 0x20, 0x28, 0x30, 0x38, 0x40, 0x48, 0x50, 0x60, 0x70, 0x80, 0xA0, 0xB0, 0xC0];

    /// <summary><c>BBSND</c> "BIGBEN SOUNDS", which also starts <c>SWPAT</c> "SWEEP PATTERN".</summary>
    private static readonly byte[] BigBenSounds =
        [8, 64, 8, 64, 8, 64, 8, 64, 8, 64, 8, 64, 8, 64, 8, 64, 8, 64, 8, 64];

    /// <summary><c>HBESND</c> "HEARTBEAT ECHO".</summary>
    private static readonly byte[] HeartbeatEcho =
        [1, 2, 4, 8, 9, 0x0A, 0x0B, 0x0C, 0x0E, 0x0F, 0x10, 0x12, 0x14, 0x16];

    /// <summary><c>SPNR</c> "SPINNER SOUND DRIP".</summary>
    private static readonly byte[] SpinnerDrip = [0x40];

    /// <summary><c>COOLDN</c> "COOL DOWNER".</summary>
    private static readonly byte[] CoolDowner = [0x10, 8, 1];

    /// <summary><c>STDSND</c> "START DISTORTO SOUND".</summary>
    private static readonly byte[] StartDistorto =
    [
        1, 1, 1, 1, 2, 2, 3, 3, 4, 4, 5, 6, 8, 0x0A, 0x0C, 0x10, 0x14, 0x18, 0x20, 0x30, 0x40, 0x50, 0x40, 0x30,
        0x20, 0x10, 0x0C, 0x0A, 8, 7, 6, 5, 4, 3, 2, 2, 1, 1, 1
    ];

    /// <summary><c>ED10FP</c> "ED'S SOUND 10".</summary>
    private static readonly byte[] EdsSoundTen = [7, 8, 9, 0x0A, 0x0C, 8];

    /// <summary><c>ED13FP</c> "ED'S SOUND 13", followed by the source's four bytes of "FILLER".</summary>
    private static readonly byte[] EdsSoundThirteen = [0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0, 0, 0, 0];

    /// <summary><c>YUKSND</c> (no comment in the source).</summary>
    private static readonly byte[] Yuksnd =
        [8, 0x80, 0x10, 0x78, 0x18, 0x70, 0x20, 0x60, 0x28, 0x58, 0x30, 0x50, 0x40, 0x48, 0];

    /// <summary><c>SP2SND</c> (no comment in the source).</summary>
    private static readonly byte[] Sp2snd = [1, 8, 0x10, 1, 8, 0x10, 1, 8, 0x10, 1, 8, 0x10, 1, 8, 0x10, 1, 8, 0x10, 0];

    /// <summary><c>SSPSND</c> (no comment in the source).</summary>
    private static readonly byte[] Sspsnd =
    [
        0x10, 0x20, 0x40, 0x10, 0x20, 0x40, 0x10, 0x20, 0x40, 0x10, 0x20, 0x40,
        0x10, 0x20, 0x40, 0x10, 0x20, 0x40, 0x10, 0x20, 0x40, 0x10, 0x20, 0x40, 0
    ];

    /// <summary><c>BWSSND</c> (no comment in the source).</summary>
    private static readonly byte[] Bwssnd =
    [
        1, 0x40, 2, 0x42, 3, 0x43, 4, 0x44, 5, 0x45, 6, 0x46, 7, 0x47, 8, 0x48, 9, 0x49, 0x0A, 0x4A, 0x0B, 0x4B, 0
    ];

    /// <summary>The pitch patterns in the order <c>GFRTAB</c> holds them.</summary>
    private static readonly byte[][] PatternsInOrder =
    [
        BonusSound, HundredPointSound, SpinnerSound, TurbineStartUp, HeartbeatDistorto, BigBenSounds, HeartbeatEcho,
        SpinnerDrip, CoolDowner, StartDistorto, EdsSoundTen, EdsSoundThirteen, Yuksnd, Sp2snd, Sspsnd, Bwssnd
    ];

    /// <summary>
    ///     <c>GWVTAB</c>: every wave shape, each one its length followed by its levels. A sound picks a wave by
    ///     its number, counting from 0.
    /// </summary>
    public static IReadOnlyList<byte> Waves { get; } =
    [
        8, 127, 217, 255, 217, 127, 36, 0, 36, // GS2
        8, 0, 64, 128, 0, 255, 0, 128, 64, // GSSQ2
        16, 127, 176, 217, 245, 255, 245, 217, 176, 127, 78, 36, 9, 0, 9, 36, 78, // GS1
        16, 127, 197, 236, 231, 191, 141, 109, 106, 127, 148, 146, 113, 64, 23, 18, 57, // GS12
        16, 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0, // GSQ22
        72, 138, 149, 160, 171, 181, 191, 200, 209, 218, 225, 232, 238, 243, 247, 251, 253, 254, 255, // GS72
        254, 253, 251, 247, 243, 238, 232, 225, 218, 209, 200, 191, 181, 171, 160, 149, 138, 127,
        117, 106, 95, 84, 74, 64, 55, 46, 37, 30, 23, 17, 12, 8, 4, 2, 1, 0,
        1, 2, 4, 8, 12, 17, 23, 30, 37, 46, 55, 64, 74, 84, 95, 106, 117, 127,
        16, 89, 123, 152, 172, 179, 172, 152, 123, 89, 55, 25, 6, 0, 6, 25, 55, // GS1.7
        8, 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0, // GSQ2
        16, 118, 255, 184, 208, 157, 230, 106, 130, 118, 234, 129, 134, 78, 156, 50, 99, // GS1234
        16, 0, 0xF4, 0, 0xE8, 0, 0xDC, 0, 0xE2, 0, 0xDC, 0, 0xE8, 0, 0xF4, 0, 0, // MW1
        72, 69, 75, 80, 86, 91, 96, 100, 105, 109, 113, 116, 119, 122, 124, 126, 127, 127, 128, // HBPAT2
        127, 127, 126, 124, 122, 119, 116, 113, 109, 105, 100, 96, 91, 86, 80, 75, 69, 64,
        59, 53, 48, 42, 37, 32, 28, 23, 19, 15, 12, 9, 6, 4, 2, 1, 1, 0,
        1, 1, 2, 4, 6, 9, 12, 15, 19, 23, 28, 32, 37, 42, 48, 53, 59, 64
    ];

    /// <summary><c>GFRTAB</c>: every pitch pattern, one after another. Each byte is the wait between two levels of the wave.</summary>
    public static IReadOnlyList<byte> Patterns { get; } = [.. PatternsInOrder.SelectMany(pattern => pattern)];

    /// <summary><c>SVTAB</c>: every wave table sound's settings. A sound number picks one by its place, counting from 0.</summary>
    public static IReadOnlyList<WaveTableVector> Vectors { get; } =
    [
        new(0x81, 0x24, 0, 0, 0, 22, StartOf(HeartbeatDistorto)), // HBDV
        new(0x12, 0x05, 0x1A, 0xFF, 0, 39, StartOf(StartDistorto)), // STDV
        new(0x11, 0x05, 0x11, 1, 15, 1, StartOf(BigBenSounds)), // DP1V (SWPAT)
        new(0x11, 0x31, 0, 1, 0, 13, StartOf(SpinnerSound)), // XBV
        new(0xF4, 0x12, 0, 0, 0, 20, StartOf(BigBenSounds)), // BBSV
        new(0x41, 0x45, 0, 0, 0, 15, StartOf(HeartbeatEcho)), // HBEV
        new(0x21, 0x35, 0x11, 0xFF, 0, 13, StartOf(SpinnerSound)), // PROTV
        new(0x15, 0x00, 0, 0xFD, 0, 1, StartOf(SpinnerDrip)), // SPNRV
        new(0x31, 0x11, 0, 1, 0, 3, StartOf(CoolDowner)), // CLDWNV
        new(0x01, 0x15, 1, 1, 1, 1, StartOf(BigBenSounds)), // SV3
        new(0xF6, 0x53, 3, 0, 2, 6, StartOf(EdsSoundTen)), // ED10
        new(0x6A, 0x10, 2, 0, 2, 6, StartOf(EdsSoundThirteen)), // ED12
        new(0x1F, 0x12, 0, 0xFF, 0x10, 4, StartOf(SpinnerDrip)), // ED17
        new(0x31, 0x11, 0, 0xFF, 0, 13, StartOf(BonusSound)), // BONV
        new(0x12, 0x06, 0, 0xFF, 1, 9, StartOf(TurbineStartUp)), // TRBV
        new(0x14, 0x17, 0, 0, 0, 14, StartOf(HundredPointSound)), // HUNV
        new(0xF4, 0x11, 0, 0, 0, 14, StartOf(HundredPointSound)), // SPD
        new(0x21, 0x30, 0, 1, 0, 13, StartOf(SpinnerSound)), // SPNV
        new(0x13, 0x10, 0, 0xFF, 0, 9, StartOf(Yuksnd)), // STRT
        new(0xF4, 0x18, 0, 0, 0, 18, StartOf(Sp2snd)), // SP1V
        new(0x82, 0x22, 0, 0, 0, 24, StartOf(Sspsnd)), // SSPV
        new(0xF2, 0x19, 0, 0, 0, 22, StartOf(Bwssnd)), // BMPV
        new(0x21, 0x30, 0, 0xFF, 0, 27, WirdvPatternStart), // WIRDV
        new(0xF1, 0x19, 0, 0, 0, 14, StartOf(Yuksnd)), // GDYUKV
        new(0x31, 0x19, 0, 1, 0, 3, StartOf(CoolDowner)), // BK8
        new(0x41, 0x02, 0xD0, 0, 0, 39, StartOf(StartDistorto)), // SF10
        new(0x03, 0x15, 0x11, 0xFF, 0, 13, StartOf(SpinnerSound)) // BIL30
    ];

    /// <summary>Where a pattern starts in <see cref="Patterns" />: the total length of the patterns before it.</summary>
    /// <param name="pattern">One of the patterns in <see cref="PatternsInOrder" />.</param>
    /// <returns>Its start.</returns>
    private static int StartOf(byte[] pattern)
    {
        return PatternsInOrder.TakeWhile(earlier => !ReferenceEquals(earlier, pattern)).Sum(earlier => earlier.Length);
    }
}
