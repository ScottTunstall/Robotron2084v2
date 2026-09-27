namespace Robotron2084.Tuning;

/// <summary>The between-game screens: wave clear, game over, entries maximum and the high score pages.</summary>
public static class ScreenTuning
{
    /// <summary>ONLY5P's <c>NAP $60</c>: the "5 ENTRIES MAXIMUM" page is held for 60 ROM frames (1.2 s).</summary>
    public const int EntriesMaximumHoldRomFrames = 0x60;

    // INITIALS ENTRY (notes §116) — RRTESTC's ENDGAM/EGSUB, RRET's messages 95 (CONG) and 100
    // (ONLY5P), and RRTESTB's GETLET. The page's geometry is InitialsEntryLayout's and the input
    // model's own clocks are InitialsEntryModel's; these two are what the states need.
    /// <summary>ONLY5P's <c>COLOR $BB</c> — the page's ink, slot 11.</summary>
    public const int EntriesMaximumSlot = 11;

    // The game-over message: RRG23 PLEND prints string 40 (GOMP = "GAME OVER") in
    // the LARGE font, colour $AA, at CURSAB $3E,$80, and waits NAP 120. Its cursor is
    // the Messages block's GameOverMessageColumn/Row.
    public const int GameOverMessageRomFrames = 120;

    // The wall is NOT one slot: FRAMER's flavour starts at $88 and GETA walks it down by
    // $11 a stroke, so each of the eight visible strokes takes its own slot — see
    // HighScoreTableLayout.FrameStrokeSlot (LOOPP then cycles slots 1-8, which is why the
    // band reads as eight colours chasing at once).
    public const int GameOverTextSlot = 10;

    public const int HighScoreAllTimeHighlightSlot = 13;

    public const int HighScoreAllTimeSlot = 10;

    public const int HighScoreHeaderSlot = 7;

    // HIGH SCORE TABLE (notes §98) — RRTABLE's TABLE, RRTESTC's CMOS lists and
    // RRET's texts; every value is a ROM constant.
    public const int HighScoreHoldRomFrames = 200 * 3;

    public const int HighScoreLeaveCheckRomFrames = 4;

    public const int HighScoreLeaveChecks = 255;

    public const int HighScoreTodayHighlightSlot = 12;

    // TABLE sets TWO colour pairs: TCOL1/TCOL2 = $99/$CC ($D8/$D9 at ROM $DF4F) for
    // TODAY'S list, then $AA/$DD ($DF75) for the top entry AND the all-time list —
    // NOINTS prints the second list without changing them again.
    public const int HighScoreTodaySlot = 9;

    /// <summary>ROM RRG23 PLEND3: "PLAYER n GAME OVER" is shown for <c>NAP $60</c>.</summary>
    public const int PlayerGameOverMessageRomFrames = 0x60;

    // SCRMEP: COLOR $77 = slot 7
    /// <summary>
    /// ROM RRG23 PLS0D: the "PLAYER n" message is drawn and the game waits
    /// <c>NAP 115</c> before erasing it — 115 ROM frames at a turn start in a
    /// 2-player game (1-player games skip it: <c>LDA PLRCNT / DECA / BEQ</c>).
    /// </summary>
    public const int PlayerTurnMessageRomFrames = 115;

    // Wave clear
    public const int WaveClearDisplayTicks = 90; // 1.5s at 60Hz fixed timestep

    // LDA #200 with NAP 3 = 600 frames = 12 s

    // TAB777's NAP 4 between switch reads

    // LDA #$FF: one DEC per check THAT FINDS A SWITCH DOWN
}
