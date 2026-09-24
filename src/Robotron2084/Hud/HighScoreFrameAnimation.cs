using Robotron2084.Core;

namespace Robotron2084.Hud;

/// <summary>
/// RRTABLE's <c>FRAMER</c> (notes §98.5) — the two passes that DRAW the high score
/// page's hatched wall, which is why the arcade's border appears to be drawn rather
/// than to appear whole:
/// <list type="number">
/// <item>the growing pass: two strokes a ROM frame, out from the smallest rectangle,
/// (62,125)-(89,127), to the terminal point (col 6, row 13)-(col 145, row 239); its flavour
/// starts at `$88` and `GETA` walks it DOWN by `$11` a stroke, so each stroke takes its own
/// palette slot (see <see cref="HighScoreTableLayout.FrameStrokeSlot"/>) and the eight visible
/// ones are slots 8…1;</item>
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

    /// <summary>
    /// The growing pass's frontier: the outermost stroke it has drawn, or -1 before it has
    /// drawn any (notes §98.5 — the first two strokes are drawn before the first sleep).
    /// </summary>
    public int DrawnStroke => _erasing ? HighScoreTableLayout.FrameLastStroke : _strokes - 1;

    /// <summary>
    /// The erase pass's frontier: the innermost stroke it has BLACKED, or -1 while that pass
    /// has not started. Everything at or below this stroke is gone; the wall is what is left
    /// between it and <see cref="DrawnStroke"/>.
    /// </summary>
    public int ErasedStroke => _erasing ? _strokes - 1 : -1;

    /// <summary>Advances the pass by one port tick (call once per Update).</summary>
    public void Tick()
    {
        if (IsFinished)
        {
            return;
        }

        _fifths += ArcadeClock.UnitsPerPortTick;
        while (_fifths >= ArcadeClock.UnitsPerRomFrame)
        {
            _fifths -= ArcadeClock.UnitsPerRomFrame;
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
