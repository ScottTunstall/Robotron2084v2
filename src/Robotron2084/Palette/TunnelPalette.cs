namespace Robotron2084.Palette;

/// <summary>
///     The colours of the wave-complete tunnel. A smooth ramp of colours slides along the tunnel's rows, with a dark seam
///     in every fifth row.
/// </summary>
/// <remarks>
///     <para>
///         This is the other half of the wave-complete effect, and the reason its bars look the way they do (notes §86). The
///         tunnel drawing only draws the tunnel. The colours it draws in come from here.
///     </para>
///     <para>
///         A ramp is a colour wheel: blue, cyan, green, yellow, red, magenta and back to blue. So fifteen colours next to each
///         other in a ramp make a smooth gradient across a third of the wheel. They fill palette entries 1 to 15, one entry for
///         each row of the tunnel, which is why the arcade shows a handful of thick bars and not fifteen stripes.
///     </para>
///     <para>
///         Three of the fifteen entries are then painted black: entries 1+A, 6+A and 11+A, where A goes 4, 3, 2, 1, 0 and round
///         on every pass. That leaves one dark entry in each group of five, five rows apart on screen. It is what the arcade
///         reference measures: a bar four rows tall with a one-row dark seam, three of them in each fifteen-row colour cycle.
///     </para>
///     <para>
///         Each pass slides the window of fifteen colours on by one colour, and wraps round at the end of the ramp. So the
///         wheel sweeps through the screen, which is the "cycling" in the effect's name.
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 Original source: <c>RRM1.ASM</c>. <c>WCPROC</c> is the process: <c>PCCALL</c> picks a ramp from
///                 <c>TABTAB</c>, <c>PCSUB</c> slides the window, <c>RCS1</c> to <c>RCS2</c> write the fifteen colours, and
///                 <c>PCS7</c> and <c>PCS4</c> paint the dark entries. The ramps are <c>DECTAB</c>, <c>DECT1</c>,
///                 <c>DECT2</c>, <c>BRTTAB</c>, <c>DCATAB</c>, <c>BPRTAB</c>, <c>GYRTAB</c>, <c>BFTAB</c>, <c>CLLTAB</c>,
///                 <c>LOGTAB</c>, <c>BFTAB2</c> and <c>DECTB4</c>.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Disassembly: <c>asm/robomame.asm</c> at <c>$576D</c> (<c>TABTAB</c>), <c>$59B1</c> (the start),
///                 <c>$59D0</c> (a pass) and <c>$5A00</c> (the dark entries). The disassembly gives no label for these, only
///                 addresses.
///             </description>
///         </item>
///     </list>
/// </remarks>
public sealed class TunnelPalette
{
    /// <summary>The first palette entry a ramp fills.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRM1.ASM</c> <c>RCS1</c>, <c>LDY #PCRAM+1</c>.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c>, <c>LDY #$9801</c>.</description></item>
    ///     </list>
    /// </remarks>
    internal const int FirstPaletteIndex = 1;

    /// <summary>How many colours the window of fifteen slides along the ramp on each pass.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Original source: <c>RRM1.ASM</c> <c>PCSUB</c>, <c>LEAX 1,X</c>, which moves the stored pointer on by one.
    ///             </description>
    ///         </item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$59D2</c>, <c>LEAX $0001,X</c>.</description></item>
    ///     </list>
    /// </remarks>
    internal const int SlidePerPass = 1;

    /// <summary>How many palette entries a ramp fills.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRM1.ASM</c> <c>RCS2</c>, <c>CMPY #PCRAM+16</c>.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c>, <c>CMPY #$9810</c>.</description></item>
    ///     </list>
    /// </remarks>
    internal const int PaletteIndexCount = 15;

    /// <summary>The entries painted black on each pass, counted from the first entry. They are five entries apart.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRM1.ASM</c> <c>PCS4</c>, <c>STB A,X / LEAX 5,X</c>.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$5A00</c>, <c>STB A,X</c>.</description></item>
    ///     </list>
    /// </remarks>
    internal static readonly int[] BlackPaletteIndices = [0, 5, 10];

    /// <summary>The ramps the tunnel can use, in the original's order. Each is a table of colours.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRM1.ASM</c> <c>TABTAB</c>, which lists the ramps; the comment on each ramp below names its table.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$576D</c> (<c>TABTAB</c>); the comment on each ramp gives its address.</description></item>
    ///     </list>
    /// </remarks>
    internal static readonly byte[][] Ramps =
    [
        // DECTAB ($5984 in the disassembly)
        [
            0x07, 0x0F, 0x17, 0x1F, 0x27, 0x2F, 0x37, 0x3F, 0x3E, 0x3D, 0x3C, 0x3B, 0x3A, 0x39, 0x38, 0x38, 0x78, 0x78,
            0xB8, 0xB8, 0xF8, 0xF8, 0xF0, 0xE8, 0xE0, 0xD8, 0xD0, 0xC8, 0xC0, 0xC1, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7,
            0xC7, 0x87, 0x87, 0x47, 0x47, 0x07
        ],
        // DECT1 ($5900 in the disassembly)
        [
            0x07, 0x0F, 0x17, 0x1F, 0x27, 0x2F, 0x37, 0x3F, 0x3F, 0x7F, 0x7F, 0xBF, 0xBF, 0xFF, 0xFF, 0xFF, 0xBF, 0xBF,
            0x7F, 0x7F, 0x3F, 0x3F, 0x3E, 0x3D, 0x3C, 0x3B, 0x3A, 0x39, 0x38, 0x38, 0x78, 0x78, 0xB8, 0xB8, 0xF8, 0xF8,
            0xF9, 0xFA, 0xFB, 0xFC, 0xFD, 0xFE, 0xFF, 0xFF, 0xFE, 0xFD, 0xFC, 0xFB, 0xFA, 0xF9, 0xF8, 0xF0, 0xE8, 0xE0,
            0xD8, 0xD0, 0xC8, 0xC0, 0xC1, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC7, 0xCF, 0xD7, 0xDF, 0xE7, 0xEF, 0xF7,
            0xFF, 0xFF, 0xF7, 0xEF, 0xE7, 0xDF, 0xD7, 0xCF, 0xC7, 0x87, 0x87, 0x47, 0x47, 0x07
        ],
        // DECT2 ($58A8 in the disassembly)
        [
            0x07, 0x0F, 0x17, 0x1F, 0x27, 0x2F, 0x37, 0x3F, 0x3F, 0x7F, 0x7F, 0xBF, 0xBF, 0xFF, 0xFF, 0xFF, 0xBF, 0xBF,
            0x7F, 0x7F, 0x3F, 0x3F, 0x3E, 0x3D, 0x3C, 0x3B, 0x3A, 0x39, 0x38, 0x38, 0x30, 0x28, 0x20, 0x08, 0x08, 0x49,
            0x52, 0xA5, 0xFB, 0xFC, 0xFD, 0xFE, 0xFF, 0xFF, 0xFE, 0xFD, 0xFC, 0xFB, 0xFA, 0xF9, 0xF8, 0xF0, 0xE8, 0xE0,
            0xD8, 0xD0, 0xC8, 0xC0, 0x80, 0x40, 0x01, 0x01, 0x01, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x4F, 0xEF, 0xF7,
            0xFF, 0xFF, 0xF7, 0xEF, 0xE7, 0xDF, 0xD7, 0xCF, 0xC7, 0x87, 0x87, 0x47, 0x47, 0x07
        ],
        // BRTTAB ($579D in the disassembly)
        [
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x0F, 0x17, 0x1F, 0x2D, 0x34, 0x3A, 0x7A, 0xBA, 0xFA, 0xF8, 0xF0,
            0xE0, 0xD0, 0xC0, 0xC0
        ],
        // DCATAB ($57B5 in the disassembly)
        [
            0xC0, 0xC0, 0xD0, 0xE0, 0xF0, 0xF8, 0xFA, 0xBA, 0x7A, 0x3A, 0x34, 0x2D, 0x1F, 0x17, 0x0F, 0x07, 0x06, 0x05,
            0x04, 0x03, 0x02, 0x01
        ],
        // BPRTAB ($57CD in the disassembly)
        [
            0xC0, 0xC1, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0x87, 0x87, 0x47, 0x47, 0x07, 0x07, 0x47, 0x47, 0x87, 0x87,
            0xC7, 0xC7, 0xC6, 0xC5, 0xC4, 0xC3, 0xC2, 0xC1
        ],
        // GYRTAB ($57E9 in the disassembly)
        [
            0x38, 0x38, 0x31, 0x3A, 0x3B, 0x3C, 0x2D, 0x2E, 0x2F, 0x27, 0x1F, 0x17, 0x17, 0x0F, 0x07, 0x07, 0x0F, 0x17,
            0x17, 0x1F, 0x27, 0x2F, 0x2E, 0x2D, 0x2C, 0x3B, 0x3A, 0x39
        ],
        // BFTAB ($5839 in the disassembly)
        [
            0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F, 0x37, 0x2F, 0x27, 0x1F, 0x17, 0x47, 0x47, 0x87, 0x87, 0xC7,
            0xC7, 0xC6, 0xC5, 0xCC, 0xCB, 0xCA, 0xDA, 0xE8, 0xF8, 0xF9, 0xFA, 0xFB, 0xFD, 0xFF, 0xBF, 0x3F, 0x3E, 0xC0,
            0xC0, 0xC0, 0x07, 0x07, 0x38, 0x38, 0x38, 0x07, 0xC0, 0x38, 0xFF, 0xFF
        ],
        // CLLTAB ($586B in the disassembly)
        [
            0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F, 0x37, 0x2F, 0x27, 0x1F, 0x17, 0x47, 0x47, 0x87, 0x87, 0xC7,
            0xC7, 0xC6, 0xC5, 0xCC, 0xCB, 0xCA, 0xDA, 0xE8, 0xF8, 0xF9, 0xFA, 0xFB, 0xFD, 0xFF, 0xBF, 0x3F, 0x3E, 0x3C
        ],
        // LOGTAB ($5891 in the disassembly)
        [
            0x37, 0x2F, 0x27, 0x1F, 0x17, 0x47, 0x47, 0x87, 0x87, 0xC7, 0xC7, 0xC6, 0xC5, 0xCC, 0xCB, 0xCA, 0xC0, 0xD0,
            0x98, 0x38, 0x33
        ],
        // BFTAB2 ($5807 in the disassembly)
        [
            0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F, 0x37, 0x2F, 0x27, 0x17, 0x0F, 0x07, 0x06, 0x05, 0x04, 0x03,
            0x02, 0x01, 0x01, 0x01, 0x49, 0xCA, 0xDA, 0xE8, 0xF8, 0xF9, 0xFA, 0xFB, 0xFD, 0xFF, 0xBF, 0x3F, 0x3E, 0xC0,
            0xC0, 0xC0, 0x07, 0x07, 0x38, 0x38, 0x38, 0x07, 0xC0, 0x38, 0xFF, 0xFF
        ],
        // DECTB4 ($5958 in the disassembly)
        [
            0x07, 0x0F, 0x17, 0x1F, 0x27, 0x2F, 0x37, 0x3F, 0x3E, 0x3D, 0x3C, 0x3B, 0x3A, 0x39, 0x38, 0x38, 0x78, 0x78,
            0xB8, 0xB8, 0xF8, 0xF8, 0xF0, 0xE8, 0xE0, 0xD8, 0xD0, 0xC8, 0xC0, 0x80, 0x41, 0x01, 0x01, 0x02, 0x03, 0x04,
            0x05, 0x06, 0x07, 0x07, 0x07, 0x07
        ]
    ];

    /// <summary>The mask that goes with each ramp. The window starts a random number of colours in, up to the mask.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRM1.ASM</c> <c>TABTAB</c>, the mask in each entry, used by <c>PCCALL</c> to start the window.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$576D</c>.</description></item>
    ///     </list>
    /// </remarks>
    internal static readonly int[] StartMasks =
        [0x1F, 0x3F, 0x3F, 0x0F, 0x0F, 0x0F, 0x0F, 0x1F, 0x1F, 0x0F, 0x1F, 0x1F];

    private readonly Random _random;
    private int _windowStartIndex;

    /// <summary>Makes the tunnel palette.</summary>
    /// <param name="random">Where the ramp and the starting place come from.</param>
    public TunnelPalette(Random random)
    {
        _random = random;
    }

    /// <summary>Which entry of each group of five is black (test hook).</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRM1.ASM</c> <c>WCPROC</c>, the value kept in <c>PD+4,U</c> ("OFFSETTER").</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c>, <c>$000B,U</c>.</description></item>
    ///     </list>
    /// </remarks>
    internal int BlackOffset { get; private set; } = 4;

    /// <summary>Which ramp is running (test hook).</summary>
    internal int RampIndex { get; private set; }

    /// <summary>Where the window of fifteen colours starts in that ramp (test hook).</summary>
    internal int WindowStartIndex => _windowStartIndex;

    /// <summary>
    ///     Does one pass: slides the window on by one colour, then moves the dark entry down one place (4, 3, 2, 1, 0 and
    ///     round).
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRM1.ASM</c> <c>PCSUB</c> and <c>PCS7</c>, with <c>NAP 1,PCSUB</c> between passes.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$59D0</c>.</description></item>
    ///     </list>
    /// </remarks>
    public void Advance()
    {
        _windowStartIndex += SlidePerPass;
        var ramp = Ramps[RampIndex];
        while (_windowStartIndex >= ramp.Length) _windowStartIndex -= ramp.Length;

        if (BlackOffset == 0) BlackOffset = 4;
        else BlackOffset--;
    }

    /// <summary>
    ///     Writes the window's fifteen colours into entries 1 to 15, in order, and wraps round to the start of the ramp at its
    ///     end. Then it paints the three dark entries black.
    /// </summary>
    /// <param name="gamePalette">The palette to write to.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Original source: <c>RRM1.ASM</c> <c>RCS1</c> to <c>RCS2</c>, where a zero sends <c>RCS3</c> back to the
    ///                 start of the table, and <c>PCS4</c> paints the dark entries.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Disassembly: <c>asm/robomame.asm</c> at <c>$59E4</c> (the wrap) and <c>$5A00</c> (the dark entries).
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    public void Apply(GamePalette gamePalette)
    {
        var ramp = Ramps[RampIndex];
        var index = _windowStartIndex;
        for (var paletteIndex = 0; paletteIndex < PaletteIndexCount; paletteIndex++)
        {
            gamePalette.SetPaletteIndexValue(FirstPaletteIndex + paletteIndex, ramp[index]);
            index++;
            if (index >= ramp.Length) index = 0; // the end of the ramp wraps round to its start
        }

        foreach (var offset in BlackPaletteIndices) gamePalette.SetPaletteIndexValue(FirstPaletteIndex + offset + BlackOffset, 0x00);
    }

    /// <summary>Picks a ramp at random, and a random place in it for the window to start.</summary>
    /// <remarks>
    ///     <para>
    ///         The place is a random number cut down by the ramp's mask. The original's first dark entry is whatever the task
    ///         block held. Here it starts at 4, which is the value the original settles into, and every pass moves it
    ///         4, 3, 2, 1, 0 and round.
    ///     </para>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRM1.ASM</c> <c>WCPROC</c> and <c>PCCALL</c>, which pick a ramp from <c>TABTAB</c> with <c>RAND</c> and mask the start.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$59B1</c>.</description></item>
    ///     </list>
    /// </remarks>
    public void Start()
    {
        RampIndex = _random.Next(Ramps.Length);
        _windowStartIndex = (_random.Next() & StartMasks[RampIndex]) % Ramps[RampIndex].Length;
        BlackOffset = 4;
    }
}
