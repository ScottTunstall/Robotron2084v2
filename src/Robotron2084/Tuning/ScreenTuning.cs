namespace Robotron2084.Tuning;

/// <summary>How long the between-game screens stay up and which palette indices their text uses: wave clear, game over, entries maximum and the high score pages.</summary>
/// <remarks>
///     The initials entry page (notes §116) comes from <c>RRTESTC.ASM</c> <c>ENDGAM</c> and <c>EGSUB</c>, the messages
///     95 (<c>CONG</c>) and 100 (<c>ONLY5P</c>) in <c>RRET.ASM</c>, and <c>RRTESTB.ASM</c> <c>GETLET</c>. The page's
///     geometry belongs to <c>InitialsEntryLayout</c> and the input model's own clocks to <c>InitialsEntryModel</c>.
///     The high score table (notes §98) comes from <c>RRTABLE.ASM</c> <c>TABLE</c>, the CMOS lists in
///     <c>RRTESTC.ASM</c> and the texts in <c>RRET.ASM</c>. Every value in the table section is a ROM constant.
/// </remarks>
public static class ScreenTuning
{
    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> the "5 ENTRIES
    ///     MAXIMUM" page is held for.
    /// </summary>
    /// <remarks>Original source: <c>RRET.ASM</c> <c>ONLY5P</c>, <c>NAP $60</c>.</remarks>
    public const int EntriesMaximumHoldArcadeFrames = 0x60;

    /// <summary>The palette index of the ink of the "5 ENTRIES MAXIMUM" page.</summary>
    /// <remarks>Original source: <c>RRET.ASM</c> <c>ONLY5P</c>, <c>COLOR $BB</c>.</remarks>
    public const int EntriesMaximumPaletteIndex = 11;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> the "GAME OVER"
    ///     message is held for.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>RRG23.ASM</c> <c>PLEND</c>, which prints string 40 (<c>GOMP</c>, "GAME OVER") in the large
    ///     font at <c>CURSAB $3E,$80</c> and then waits <c>NAP 120</c>. The cursor is the messages block's
    ///     <c>GameOverMessageColumn</c> and row.
    /// </remarks>
    public const int GameOverMessageArcadeFrames = 120;

    /// <summary>The palette index of the "GAME OVER" text.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>PLEND</c>, colour <c>$AA</c>.</remarks>
    public const int GameOverTextPaletteIndex = 10;

    /// <summary>The palette index of the highlighted entries in the all-time high score list.</summary>
    public const int HighScoreAllTimeHighlightPaletteIndex = 13;

    /// <summary>The palette index of the all-time high score list.</summary>
    /// <remarks>
    ///     Original source: <c>RRTABLE.ASM</c> <c>TABLE</c>. It sets two colour pairs. <c>TCOL1</c> and <c>TCOL2</c> are
    ///     <c>$99</c> and <c>$CC</c> (<c>$D8</c> and <c>$D9</c> at ROM <c>$DF4F</c>) for today's list, then <c>$AA</c> and
    ///     <c>$DD</c> (ROM <c>$DF75</c>) for the top entry and the all-time list. <c>NOINTS</c> prints the second list
    ///     without changing them again.
    /// </remarks>
    public const int HighScoreAllTimePaletteIndex = 10;

    /// <summary>The palette index of the heading of the high score pages.</summary>
    /// <remarks>Original source: <c>SCRMEP</c>, <c>COLOR $77</c>.</remarks>
    public const int HighScoreHeaderPaletteIndex = 7;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> the high score page
    ///     is held for.
    /// </summary>
    /// <remarks>Original source: <c>RRTABLE.ASM</c> <c>TABLE</c>, <c>LDA #200</c> with <c>NAP 3</c>.</remarks>
    public const int HighScoreHoldArcadeFrames = 200 * 3;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> pass between one
    ///     look at the switches and the next, while the high score page waits to be left.
    /// </summary>
    /// <remarks>Original source: <c>TAB777</c>, <c>NAP 4</c> between switch reads.</remarks>
    public const int HighScoreLeaveCheckArcadeFrames = 4;

    /// <summary>How many looks at the switches that find one held down it takes to leave the high score page.</summary>
    /// <remarks>Original source: <c>LDA #$FF</c>, with one <c>DEC</c> for each check that finds a switch down.</remarks>
    public const int HighScoreLeaveChecks = 255;

    /// <summary>The palette index of the highlighted entries in today's high score list.</summary>
    public const int HighScoreTodayHighlightPaletteIndex = 12;

    /// <summary>The palette index of today's high score list.</summary>
    /// <remarks>Original source: <c>RRTABLE.ASM</c> <c>TABLE</c>, <c>TCOL1</c> and <c>TCOL2</c> as described on <see cref="HighScoreAllTimePaletteIndex" />.</remarks>
    public const int HighScoreTodayPaletteIndex = 9;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> the
    ///     "PLAYER n GAME OVER" message is shown for.
    /// </summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>PLEND3</c>, <c>NAP $60</c>.</remarks>
    public const int PlayerGameOverMessageArcadeFrames = 0x60;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> the "PLAYER n"
    ///     message is shown for at the start of a turn.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>RRG23.ASM</c> <c>PLS0D</c>, <c>NAP 115</c>. The message is shown only in a two-player game
    ///     (<c>LDA PLRCNT / DECA / BEQ</c>).
    /// </remarks>
    public const int PlayerTurnMessageArcadeFrames = 115;

    /// <summary>How many <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">port ticks</see> the wave-clear tunnel is held for.</summary>
    /// <remarks>Port only: the arcade runs the tunnel until its effect finishes.</remarks>
    public const int WaveClearDisplayTicks = 90;
}
