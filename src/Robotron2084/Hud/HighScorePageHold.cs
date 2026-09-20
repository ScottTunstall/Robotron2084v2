using Robotron2084.Tuning;

namespace Robotron2084.Hud;

/// <summary>
/// RRTABLE's <c>TABLE</c> hold and exit (notes §98.8) — the two loops at the end of the
/// high score page, which are NOT what the port first shipped:
///
/// <code>
///        LDA    #200
///        STA    PD,U           NUMBER OF FRAMES TO FREEZE
/// TAB888 NAP    3,TABLE6        ; 200 x NAP 3 = 600 frames, and NO switch check at all
/// TABLE6 DEC    PD,U
///        BNE    TAB888
///        LDA    #$FF
///        STA    PD,U
/// TAB777 NAP    4,TAB999        ; from here on the ROM looks at the switches
/// TAB999 LDA    PIA3
///        ANDA   #$3      ONLY 2 SWS HERE
///        ORAA   PIA2     ANY PRESSED?
///        BEQ    TABLE7   NONE PRESSED   -> RETURN
///        DEC    PD,U     1 LESS COUNT
///        BNE    TAB777
/// TABLE7 JMP    [PD+18,U]      RETURN
/// </code>
///
/// So the page is played for its full 600 frames (12 s) <b>whatever the player is
/// doing</b> — a press can never shorten it — and only then does the ROM leave, and it
/// leaves the moment the switches are <b>CLEAR</b>. A switch that is still held DELAYS
/// the exit (that is the point: the page is followed by the title/attract, which must
/// not be skipped because a button is bent), for at most <c>$FF</c> checks of four
/// frames each.
///
/// The switch bits are ACTIVE HIGH — a set bit is a pressed switch. The movement
/// descriptor table at $3031 settles it: <c>MOVE_PLAYER</c> reads PIA-A's four stick
/// bits and indexes that table, and entry 1 (bit 0 set = PIA-A bit 0 = "Move up") is the
/// up delta <c>00 FF</c>. So <c>ORAA PIA2 / BEQ</c> means "no switch at all is down".
///
/// The timing runs on the exact-6ths clock the entity bodies use (notes §52): a port
/// tick advances a ROM-frame clock by 5 sixths.
/// </summary>
public sealed class HighScorePageHold
{
    /// <summary>A port tick advances a ROM-frame clock by 5 sixths (notes §52).</summary>
    private const int SixthsPerPortTick = 5;

    /// <summary>One ROM frame in sixths of a port tick.</summary>
    private const int SixthsPerRomFrame = 6;

    private int _holdSixths;
    private int _checkSixths;
    private int _checks;

    /// <summary>True once the 600-frame hold has run out and the switches are being read.</summary>
    public bool HoldIsOver { get; private set; }

    /// <summary>
    /// How many of the ROM's <c>$FF</c> post-hold checks have found a switch down — the
    /// page leaves when this reaches <see cref="GameplayConstants.HighScoreLeaveChecks"/>.
    /// </summary>
    public int ChecksWithSwitchDown => _checks;

    /// <summary>
    /// Advances the page one port tick. <paramref name="anySwitchHeld"/> is the ROM's read
    /// of PIA2 <c>OR</c> PIA3 (active high — any of the sticks, the fire buttons or the
    /// START buttons). Returns true when the ROM would leave the page.
    /// </summary>
    public bool Tick(bool anySwitchHeld)
    {
        if (!HoldIsOver)
        {
            _holdSixths += SixthsPerPortTick;
            if (_holdSixths < SixthsPerRomFrame * GameplayConstants.HighScoreHoldRomFrames)
            {
                return false;
            }

            // The hold is over and TAB777's first NAP 4 starts now, so the first switch
            // read is four ROM frames away.
            HoldIsOver = true;
            _checkSixths = 0;
            return false;
        }

        _checkSixths += SixthsPerPortTick;
        if (_checkSixths < SixthsPerRomFrame * GameplayConstants.HighScoreLeaveCheckRomFrames)
        {
            return false;
        }

        _checkSixths -= SixthsPerRomFrame * GameplayConstants.HighScoreLeaveCheckRomFrames;

        if (!anySwitchHeld)
        {
            // TAB999's BEQ: nothing is down, so the page returns.
            return true;
        }

        _checks++;
        return _checks >= GameplayConstants.HighScoreLeaveChecks;
    }
}
