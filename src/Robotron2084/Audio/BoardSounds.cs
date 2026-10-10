namespace Robotron2084.Audio;

/// <summary>One of the sound board's 63 sounds, as the sound ROM's source names it.</summary>
/// <param name="Number">The sound number the main board sends (the original source's <c>SND#</c>), 1 to 63.</param>
/// <param name="Name">The label the source gives the sound's routine or vector (<c>HBDV</c>, <c>CANNON</c>, ...).</param>
/// <param name="Description">The words the source puts beside the label or the sound's data, or empty when it has none.</param>
public sealed record BoardSound(int Number, string Name, string Description);

/// <summary>
/// Every sound the board can play, in sound-number order, named as the sound ROM's source names them. It is the
/// list the sound test page steps through.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>VSNDRM3.SRC</c> ("ROBOTRON SOUNDS VERSION 1.0 3-8-82"): the handler <c>IRQ</c> decides
/// which routine a number reaches, so the order here is the handler's: numbers 1 to 13 are the first
/// thirteen wave table vectors of <c>SVTAB</c>; 14 to 28 are <c>JMPTBL</c>; 29 to 31 are the first three
/// <c>VVECT</c> square waves; 32 to 43 are the next twelve <c>SVTAB</c> vectors; 44 to 62 are <c>JMPTB1</c>; and 63
/// is the fifth <c>VVECT</c>, which the handler reaches by subtracting $39.</item>
/// <item>Disassembly: none in this repo (the sound ROM is not disassembled).</item>
/// </list>
/// The source's wave table vectors are named but not described; the description given for them is the name of
/// the pitch pattern the vector plays, which is the only words the source has for them.
/// </remarks>
public static class BoardSounds
{
    /// <summary>The first sound number.</summary>
    public const int First = 1;

    /// <summary>The last sound number: the board has six sound lines.</summary>
    public const int Last = 63;

    /// <summary>The 63 sounds, in sound-number order.</summary>
    public static IReadOnlyList<BoardSound> All { get; } =
    [
        new(0x01, "HBDV", "HEARTBEAT DISTORTO"),
        new(0x02, "STDV", "START DISTORTO SOUND"),
        new(0x03, "DP1V", "SWEEP PATTERN"),
        new(0x04, "XBV", "SPINNER SOUND"),
        new(0x05, "BBSV", "BIGBEN SOUNDS"),
        new(0x06, "HBEV", "HEARTBEAT ECHO"),
        new(0x07, "PROTV", "SPINNER SOUND"),
        new(0x08, "SPNRV", "SPINNER SOUND DRIP"),
        new(0x09, "CLDWNV", "COOL DOWNER"),
        new(0x0A, "SV3", "BIGBEN SOUNDS"),
        new(0x0B, "ED10", "ED'S SOUND 10"),
        new(0x0C, "ED12", "ED'S SOUND 13"),
        new(0x0D, "ED17", "SPINNER SOUND DRIP"),
        new(0x0E, "SP1", "SPINNER #1 SOUND"),
        new(0x0F, "BG1", "BACKGROUND 1 ROUTINE"),
        new(0x10, "BG2INC", "BACKGROUND SOUND #2 INCREMENT"),
        new(0x11, "LITE", "LIGHTNING"),
        new(0x12, "BON2", "LASER BALL BONUS #2"),
        new(0x13, "BGEND", "BACKGROUND END ROUTINE"),
        new(0x14, "TURBO", string.Empty),
        new(0x15, "APPEAR", string.Empty),
        new(0x16, "THRUST", string.Empty),
        new(0x17, "CANNON", "DEFENDER SND #$17"),
        new(0x18, "RADIO", string.Empty),
        new(0x19, "HYPER", string.Empty),
        new(0x1A, "SCREAM", string.Empty),
        new(0x1B, "ORGANT", "ORGAN TUNE"),
        new(0x1C, "ORGANN", "ORGAN NOTE"),
        new(0x1D, "SAW", "VARI VECTOR"),
        new(0x1E, "FOSHIT", "VARI VECTOR"),
        new(0x1F, "QUASAR", "VARI VECTOR"),
        new(0x20, "HUNV", "HUNDRED POINT SOUND"),
        new(0x21, "SPD", "HUNDRED POINT SOUND"),
        new(0x22, "SPNV", "SPINNER SOUND"),
        new(0x23, "STRT", string.Empty),
        new(0x24, "SP1V", string.Empty),
        new(0x25, "SSPV", string.Empty),
        new(0x26, "BMPV", string.Empty),
        new(0x27, "WIRDV", string.Empty),
        new(0x28, "GDYUKV", string.Empty),
        new(0x29, "BK8", "COOL DOWNER"),
        new(0x2A, "SF10", "START DISTORTO SOUND"),
        new(0x2B, "BIL30", "SPINNER SOUND"),
        new(0x2C, "SND2", "SOUND 2"),
        new(0x2D, "SND5", "SOUND 5"),
        new(0x2E, "THNDR", "THUNDER SOUND"),
        new(0x2F, "HSTD", "SINGLE OSCILLATOR SOUND CALLS"),
        new(0x30, "ATARI", "SINGLE OSCILLATOR SOUND CALLS"),
        new(0x31, "SIREN", "SINGLE OSCILLATOR SOUND CALLS"),
        new(0x32, "ORRRR", "SINGLE OSCILLATOR SOUND CALLS"),
        new(0x33, "PERK$$", "SINGLE OSCILLATOR SOUND CALLS"),
        new(0x34, "SQRT", "RANDOM SQUIRTS"),
        new(0x35, "START", "FUNNY ELECTRIC SOUND"),
        new(0x36, "PLANE", "DIVING PLANE SOUND"),
        new(0x37, "SND16", string.Empty),
        new(0x38, "SND17", string.Empty),
        new(0x39, "LAUNCH", string.Empty),
        new(0x3A, "CDR", "CROWD ROAR"),
        new(0x3B, "KNOCK", "KNOCKER ROUTINE"),
        new(0x3C, "ZIREN", "SIREN   AIR RAID"),
        new(0x3D, "WHIST", "THE BOMB OOOOOH NOOOOO!"),
        new(0x3E, "HBOMB", string.Empty),
        new(0x3F, "MOSQTO", "VARI VECTOR"),
    ];
}
