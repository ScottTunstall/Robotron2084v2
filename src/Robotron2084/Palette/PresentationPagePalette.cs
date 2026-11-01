using Robotron2084.Core;

namespace Robotron2084.Palette;

/// <summary>
///     The colours of the presentation page that shows the welcome message, and the timers that change them. A white
///     flash runs through the page's seven colours, and the border logos change colour as well.
/// </summary>
/// <remarks>
///     <para>
///         The page uses seven colours of its own, in palette entries 1 to 7: red, blue, red-orange, green, magenta, orange
///         and yellow. The other entries keep their usual colours. The page's text is drawn in entry 6, which is orange.
///     </para>
///     <para>Two timers change the colours while the page is showing:</para>
///     <list type="bullet">
///         <item>
///             <description>
///                 The white flash turns one entry white every 3 fiftieths of a second. It moves on to the next entry each time,
///                 in the order 2, 3, 4, 5, 6, 7, 1 and then round again.
///             </description>
///         </item>
///         <item>
///             <description>
///                 The border logos change colour every 28 fiftieths of a second. They go down the entries in the order
///                 7, 6, 5, 4, 3, 2, 1 and then round again.
///             </description>
///         </item>
///     </list>
///     <para>
///         Both timers count in clock units (notes §52), where a fiftieth of a second is 6/5 of a port tick. The 28 is the
///         border ring's own rate: the ring has 28 logos and the page handles one each frame, so a given logo is painted
///         once every 28 frames.
///     </para>
///     <para>
///         <b>Why it is called the white flash.</b> The original source calls this the white process: <c>COL1</c> starts it,
///         and <c>COLL</c> keeps a white pointer that it kicks up to the next entry. The project's notes called it the
///         chase. Here it is called the white flash, because that is what you see: one entry at a time turns white, and the
///         white moves on through the seven.
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 Original source: <c>RRLOG.ASM</c>. <c>LOGG2</c> builds the page, <c>CSET</c> copies the colour table
///                 <c>LCTAB</c>, <c>COL1</c> and <c>COLL</c> run the white flash, <c>LINOUT</c> prints the welcome message,
///                 <c>FORMX</c> changes the border logos' colour, and <c>DOBALL</c> and <c>WILL55</c> run the ring of logos.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Disassembly: <c>asm/robomame.asm</c> at <c>$87A6</c> (<c>LOGG2</c>), <c>$8A3A</c> (<c>CSET</c>),
///                 <c>$8A4F</c> (<c>COL1</c>), <c>$884E</c> (<c>LINOUT</c>), <c>$89DA</c> (<c>FORMX</c>), <c>$87D9</c>
///                 (<c>DOBALL</c>), <c>$88EF</c> (<c>WILL55</c>) and <c>$8A70</c> (<c>LCTAB</c>). The disassembly gives no
///                 label for these routines, only addresses.
///             </description>
///         </item>
///     </list>
/// </remarks>
public sealed class PresentationPagePalette
{
    /// <summary>The first of the seven palette entries the page writes.</summary>
    public const int FirstPaletteIndex = 1;

    /// <summary>The last of the seven palette entries the page writes.</summary>
    public const int LastPaletteIndex = 7;

    /// <summary>The palette entry the page's text is drawn in. The page's colours make it orange.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRLOG.ASM</c> <c>LINOUT</c>, <c>LDA #$66</c> ("COLOR OF OPERATOR MESSAGE").</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$884E</c>.</description></item>
    ///     </list>
    /// </remarks>
    public const int TextPaletteIndex = 6;

    /// <summary>
    ///     The seven colours the page writes into its palette entries, in order: red, blue, red-orange, green, magenta, orange
    ///     and yellow.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRLOG.ASM</c> <c>LCTAB</c>, which <c>CSET</c> copies.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$8A70</c>.</description></item>
    ///     </list>
    /// </remarks>
    public static readonly byte[] PageColours = [0x07, 0xC0, 0x17, 0x30, 0xC7, 0x1F, 0x3F];

    /// <summary>How many fiftieths of a second pass between one cycle of the white flash and the next.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRLOG.ASM</c> <c>COLL</c>, <c>NAP 3,COLL</c>.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$8A68</c>, <c>LDA #$03</c>.</description></item>
    ///     </list>
    /// </remarks>
    private const int WhiteFlashArcadeFramesPerCycle = 3;

    /// <summary>The colour the white flash paints on an entry.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRLOG.ASM</c> <c>COLL8</c>, <c>LDA #$FF / STA ,X</c>.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$8A64</c>, <c>LDA #$FF</c>.</description></item>
    ///     </list>
    /// </remarks>
    private const byte WhiteFlashColour = 0xFF;

    /// <summary>How many fiftieths of a second pass between one colour change of the border logos and the next.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRLOG.ASM</c> <c>DOBALL</c>, <c>LDA #28</c> ("WE'LL DO 28"), with the wait in <c>WILL55</c>.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$87D9</c>, <c>LDA #$1C</c>, and <c>$88EF</c>.</description></item>
    ///     </list>
    /// </remarks>
    private const int WordmarkArcadeFramesPerCycle = 28;

    private readonly int _paletteIndexCount = LastPaletteIndex - FirstPaletteIndex + 1;

    private int _whiteFlashClockUnits;
    private int _whiteFlashPaletteIndex = FirstPaletteIndex;
    private int _wordmarkClockUnits;
    private int _wordmarkCyclePosition;

    /// <summary>
    ///     The palette entry the wordmark is drawn in right now. It goes 7, 6, 5, 4, 3, 2, 1 and then back to 7.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Original source: <c>RRLOG.ASM</c> <c>FORMX</c> (<c>SIDEX</c>, <c>SIDE8</c>): <c>BALCOL</c> goes down
    ///                 by <c>SUBA #$11</c> each time, from <c>$77</c> to <c>$11</c> and then back to <c>$77</c>.
    ///             </description>
    ///         </item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$89DA</c>, <c>SUBA #$11</c>.</description></item>
    ///     </list>
    /// </remarks>
    public int GetWordmarkPaletteIndex()
    {
        return LastPaletteIndex - _wordmarkCyclePosition;
    }

    /// <summary>
    ///     The palette entry the wordmark's rim is drawn in: the one just before the wordmark's own entry, or entry 7 when
    ///     the wordmark is in entry 1.
    /// </summary>
    /// <remarks>
    ///     The port's traced wordmark draws its rim here, so that the two-tone look of the reference screenshot stays as the
    ///     colours cycle. At the wrap-round the pair is the reference's own red body on a yellow rim.
    /// </remarks>
    public int GetWordmarkRimPaletteIndex()
    {
        var wordmarkPaletteIndex = GetWordmarkPaletteIndex();
        if (wordmarkPaletteIndex == FirstPaletteIndex) return LastPaletteIndex;

        return wordmarkPaletteIndex - 1;
    }

    /// <summary>Gives the page its seven colours, with no white on it yet.</summary>
    /// <param name="gamePalette">The palette to write the seven colours into.</param>
    /// <remarks>
    ///     <para>
    ///         The page starts with no white because the colours are copied before the white flash starts, and the white flash
    ///         moves on to the next entry before it turns anything white. So the first white lands on entry 2.
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Original source: <c>RRLOG.ASM</c> <c>LOGG2</c> calls <c>CSET</c> before it starts <c>COL1</c>, and
    ///                 <c>COLL5</c> kicks the white pointer up before <c>COLL8</c> turns that entry white.
    ///             </description>
    ///         </item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> at <c>$8A3A</c> (<c>CSET</c>) and <c>$8A4F</c> (<c>COL1</c>).</description></item>
    ///     </list>
    /// </remarks>
    public void Start(GamePalette gamePalette)
    {
        for (var paletteIndex = FirstPaletteIndex; paletteIndex <= LastPaletteIndex; paletteIndex++) gamePalette.SetPaletteIndexValue(paletteIndex, PageColours[paletteIndex - FirstPaletteIndex]);
    }

    /// <summary>Puts the page's seven palette entries back to their usual colours when the page is left.</summary>
    /// <param name="gamePalette">The palette to put the usual colours back in.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Original source: the colour processes end with the page, and the page after it, <c>FAMPAG</c> in
    ///                 <c>RRSCRIPT.ASM</c>, sets its own colours (notes §103.3).
    ///             </description>
    ///         </item>
    ///         <item><description>Disassembly: not separately labelled.</description></item>
    ///     </list>
    /// </remarks>
    public void Stop(GamePalette gamePalette)
    {
        for (var paletteIndex = FirstPaletteIndex; paletteIndex <= LastPaletteIndex; paletteIndex++) gamePalette.SetPaletteIndexValue(paletteIndex, GamePalette.DefaultPaletteValues[paletteIndex]);
    }

    /// <summary>Moves both timers on by one port tick, and changes the colours when a cycle is due.</summary>
    /// <param name="gamePalette">The palette to change.</param>
    public void Update(GamePalette gamePalette)
    {
        _whiteFlashClockUnits += ArcadeClock.UnitsPerPortTick;
        (var isWhiteFlashCycleDue, _whiteFlashClockUnits) = TakeCycleIfDue(_whiteFlashClockUnits, WhiteFlashArcadeFramesPerCycle);
        if (isWhiteFlashCycleDue)
        {
            var isOnLastPaletteIndex = _whiteFlashPaletteIndex >= LastPaletteIndex;
            if (isOnLastPaletteIndex) _whiteFlashPaletteIndex = FirstPaletteIndex;
            else _whiteFlashPaletteIndex++;

            // Like the original, copy all seven colours again first, then turn the current entry white.
            Start(gamePalette);
            gamePalette.SetPaletteIndexValue(_whiteFlashPaletteIndex, WhiteFlashColour);
        }

        _wordmarkClockUnits += ArcadeClock.UnitsPerPortTick;
        (var isWordmarkCycleDue, _wordmarkClockUnits) = TakeCycleIfDue(_wordmarkClockUnits, WordmarkArcadeFramesPerCycle);
        if (isWordmarkCycleDue)
            _wordmarkCyclePosition = (_wordmarkCyclePosition + 1) % _paletteIndexCount;
    }

    /// <summary>Takes one cycle off a timer, if the timer has gathered at least that many frames.</summary>
    /// <param name="clockUnits">What the timer has gathered, before the cycle is taken.</param>
    /// <param name="arcadeFrames">How many fiftieths of a second make one cycle.</param>
    /// <returns><c>IsDue</c> is true when a cycle was taken, and <c>ClockUnits</c> is what the timer has gathered after it.</returns>
    private static (bool IsDue, int ClockUnits) TakeCycleIfDue(int clockUnits, int arcadeFrames)
    {
        var period = ArcadeClock.ToClockUnits(arcadeFrames);
        if (clockUnits < period) return (false, clockUnits);

        return (true, clockUnits - period);
    }
}
