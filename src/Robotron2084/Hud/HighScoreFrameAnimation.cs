namespace Robotron2084.Hud;

/// <summary>
/// RRTABLE's <c>FRAMER</c> (notes §98.5) — the two passes that DRAW the high score
/// page's hatched wall, which is why the arcade's border appears to be drawn rather
/// than to appear whole:
/// <list type="number">
/// <item>the growing pass: two strokes a ROM frame, colour `$88` (palette slot 8),
/// from the smallest rectangle, (62,125)-(89,127), out to the terminal point
/// (col 6, row 13)-(col 145, row 239);</item>
/// <item>the erase pass: the same walk again in flavour 0 (BLACK) — <c>FRCOND</c> does
/// <c>CLR PD+14,U</c> — which restarts at stroke 0 and stops at (col 14, row 29), so it
/// blacks the strokes it retraces and leaves the 8-column / 16-row band visible.</item>
/// </list>
/// Both passes draw their first TWO strokes before the first sleep
/// (<c>LDA #2 / STA PD+15,U</c>, then <c>NAP 1</c> per pair), and a stroke is half a ROM
/// frame, so this runs on the exact-6ths accumulator the entity bodies use (notes §52):
/// a stroke is 6 sixths = 0.6 ROM frames = 1.2 port ticks.
/// </summary>
public sealed class HighScoreFrameAnimation
{
    /// <summary>A port tick advances a ROM-frame clock by 5 sixths (notes §52).</summary>
    private const int SixthsPerPortTick = 5;

    /// <summary>One ROM frame in sixths of a port tick.</summary>
    private const int SixthsPerRomFrame = 6;

    /// <summary>
    /// Strokes drawn so far by the pass that is running, and which pass that is. Both
    /// passes have drawn <see cref="HighScoreTableLayout.FrameStrokesPerRomFrame"/> of
    /// them by the time the page first appears.
    /// </summary>
    private int _strokes = HighScoreTableLayout.FrameStrokesPerRomFrame;
    private bool _erasing;
    private int _fifths;

    /// <summary>True once the erase pass has reached its terminal point — the wall is complete.</summary>
    public bool IsFinished => _erasing && _strokes >= HighScoreTableLayout.FrameEraseStrokeCount;

    /// <summary>The outermost stroke drawn so far, in arcade pixels (the band's outside edge).</summary>
    public (int Left, int Top, int Right, int Bottom) OuterRect =>
        HighScoreTableLayout.FrameStroke(
            _erasing ? HighScoreTableLayout.FrameLastStroke : _strokes - 1);

    /// <summary>
    /// The inside edge of the visible band: the erase pass's frontier, which erases the
    /// strokes it retraces — before that pass starts, the band runs down to stroke 0.
    /// </summary>
    public (int Left, int Top, int Right, int Bottom) InnerRect =>
        HighScoreTableLayout.FrameStroke(
            _erasing ? _strokes - 1 : HighScoreTableLayout.FrameFirstStroke);

    /// <summary>Advances the pass by one port tick (call once per Update).</summary>
    public void Tick()
    {
        if (IsFinished)
        {
            return;
        }

        _fifths += SixthsPerPortTick;
        while (_fifths >= SixthsPerRomFrame)
        {
            _fifths -= SixthsPerRomFrame;
            Advance(HighScoreTableLayout.FrameStrokesPerRomFrame);
        }
    }

    private void Advance(int strokes)
    {
        for (int i = 0; i < strokes; i++)
        {
            if (!_erasing)
            {
                if (_strokes == HighScoreTableLayout.FrameStrokeCount)
                {
                    // FRBYE → FRCONT: the growing pass is done, so the ROM clears the
                    // flavour and jumps back to its start — the erase's first two strokes
                    // are drawn at once, exactly as the growing pass's were.
                    _erasing = true;
                    _strokes = HighScoreTableLayout.FrameStrokesPerRomFrame;
                }
                else
                {
                    _strokes++;
                }
            }
            else if (!IsFinished)
            {
                _strokes++;
            }
        }
    }
}
