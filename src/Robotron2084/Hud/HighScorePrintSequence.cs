namespace Robotron2084.Hud;

/// <summary>
/// RRTABLE's <c>PRJNK</c> — the page PRINTS itself, and that order is what the arcade's
/// high score screen looks like when it appears (notes §98.6):
/// <list type="number">
/// <item><c>MAKP LOOPP</c> then <c>JSR FRAMER</c>: the wall is drawn on an EMPTY page
/// (the screen was cleared by <c>SCRCLR</c> before <c>TABORG</c>), so nothing else is on
/// screen until the frame's two passes have finished;</item>
/// <item><c>PRJNK</c> prints TODAY'S list, FOUR ROWS A ROM FRAME (<c>TOD44</c>'s
/// <c>LDA #4 / STA PD+17,U</c> with a <c>NAP 1</c> per group — the first group prints at
/// once, and the last group of a list does not sleep);</item>
/// <item>then the top "GOD" entry (printed by <c>TABLE</c> itself, no sleep), the
/// ALL-TIME list (again four rows a frame) and finally the headers
/// (<c>LDA #SCRMES / JSR WRD7V</c>), which is why they are the LAST thing to
/// appear;</item>
/// <item>only then does <c>TABLE</c> start the four ramp processes
/// (<c>MAKP DECAZ/COLA/COLC/COLD</c>) and begin its 600-frame hold.</item>
/// </list>
/// The printed rows are already in palette slots 9/10, and those slots are BLACK until
/// the ramps start (FRAMER zeroed the palette), so the text is drawn invisibly for the
/// few frames it takes to print and then comes up dark red as DECAZ/COLA write their
/// first byte — see <see cref="Rendering.HighScorePalette"/>.
/// </summary>
public sealed class HighScorePrintSequence
{
    /// <summary>The ROM's <c>LDA #4</c>: four entries per sleep.</summary>
    public const int RowsPerGroup = 4;

    /// <summary>A port tick advances a ROM-frame clock by 5 sixths (notes §52).</summary>
    private const int SixthsPerPortTick = 5;

    /// <summary>One ROM frame in sixths of a port tick.</summary>
    private const int SixthsPerRomFrame = 6;

    private enum Phase
    {
        Waiting,
        Today,
        Top,
        AllTime,
        Headers,
        Done,
    }

    private Phase _phase = Phase.Waiting;
    private int _fifths;

    /// <summary>Today's rows printed so far (0 until the frame has finished).</summary>
    public int TodayRows { get; private set; }

    /// <summary>True once the operator's top entry has been printed.</summary>
    public bool TopPrinted { get; private set; }

    /// <summary>All-time rows printed so far.</summary>
    public int AllTimeRows { get; private set; }

    /// <summary>True once the headers have been printed — the page is complete.</summary>
    public bool HeadersPrinted { get; private set; }

    /// <summary>True while the page is still being printed (nothing is drawn before it starts).</summary>
    public bool IsPrinting => _phase is not (Phase.Waiting or Phase.Done);

    /// <summary>True when the page has finished printing.</summary>
    public bool IsDone => _phase == Phase.Done;

    /// <summary>
    /// Advances the printing by one port tick. Nothing happens until
    /// <paramref name="frameFinished"/> — the ROM's <c>JSR FRAMER</c> must return first.
    /// </summary>
    public void Tick(bool frameFinished, int todayCount, int allTimeCount)
    {
        if (_phase == Phase.Waiting)
        {
            if (!frameFinished)
            {
                return;
            }

            // PRJNK's first group prints the moment it is called (TOD44 falls straight
            // into TOD33), so the first four rows appear with the frame's last stroke.
            _phase = Phase.Today;
            Advance(todayCount, allTimeCount);
            return;
        }

        if (_phase == Phase.Done)
        {
            return;
        }

        _fifths += SixthsPerPortTick;
        while (_fifths >= SixthsPerRomFrame)
        {
            _fifths -= SixthsPerRomFrame;
            Advance(todayCount, allTimeCount);
        }
    }

    /// <summary>Prints one group and walks the ROM's phase order on to the next.</summary>
    private void Advance(int todayCount, int allTimeCount)
    {
        while (true)
        {
            switch (_phase)
            {
                case Phase.Today:
                    TodayRows = System.Math.Min(todayCount, TodayRows + RowsPerGroup);
                    if (TodayRows < todayCount)
                    {
                        return;
                    }

                    _phase = Phase.Top;
                    continue;

                case Phase.Top:
                    // TABLE prints "( NAME ) SCORE" itself, between the two lists.
                    TopPrinted = true;
                    _phase = Phase.AllTime;
                    continue;

                case Phase.AllTime:
                    AllTimeRows = System.Math.Min(allTimeCount, AllTimeRows + RowsPerGroup);
                    if (AllTimeRows < allTimeCount)
                    {
                        return;
                    }

                    _phase = Phase.Headers;
                    continue;

                case Phase.Headers:
                    // SCRMES/WRD7V: the two "ROBOTRON HEROES" / "ALL TIME HEROES" lines
                    // are the very last thing the ROM prints on this page.
                    HeadersPrinted = true;
                    _phase = Phase.Done;
                    return;

                default:
                    return;
            }
        }
    }
}
