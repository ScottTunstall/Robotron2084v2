namespace Robotron2084.Tuning;

/// <summary>How the title page, the story and the demo player behave in attract mode.</summary>
/// <remarks>
///     Attract mode (notes §94) is the arcade's attract cycle: an idle title, then the machine plays itself (when the
///     CMOS setting "FANCY ATTRACT MODE" is on). The demo player's rules are the port's own, because the arcade's
///     demo player lives in the operating system ROM only (notes §94.3).
/// </remarks>
public static class AttractTuning
{
    /// <summary>
    ///     The fewest <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> the demo player's stick keeps a
    ///     direction once it has followed a new one.
    /// </summary>
    /// <remarks>
    ///     Port only. The arcade's walk animation is a four-frame cycle at three ticks a frame, so a direction held for
    ///     fewer ticks than the whole cycle can never show a complete walk (notes §97.5).
    /// </remarks>
    public const int DemoDirectionHoldTicks = 9;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> in a row a newly worked-out direction
    ///     must win before the demo player's stick follows it.
    /// </summary>
    /// <remarks>
    ///     Port only. The flee direction is the sign of the player minus the robot, worked out again against a moving
    ///     field, so it flips on about three ticks in four without this delay. The arcade's walk animation restarts on
    ///     every change of facing (R5 <c>$3003</c> to <c>$3009</c>), which would make the demo's man twitch in place
    ///     instead of walking (notes §97.5).
    /// </remarks>
    public const int DemoDirectionSwitchTicks = 3;

    /// <summary>How near a robot must be for the demo player to fire at it, in arcade pixels.</summary>
    /// <remarks>Port only.</remarks>
    public const int DemoFireRangeArcadePixels = 120;

    /// <summary>
    ///     The odds against the demo player pausing on any one tick, which are one in this number. The pause makes it feel
    ///     alive and not robotic.
    /// </summary>
    /// <remarks>Port only.</remarks>
    public const int DemoStutterChanceDenominator = 16;

    /// <summary>How near a robot must be for the demo player to run from it, in arcade pixels.</summary>
    /// <remarks>Port only.</remarks>
    public const int DemoThreatDistanceArcadePixels = 60;

    /// <summary>How near a wall must be for the demo player to steer away from it, in arcade pixels.</summary>
    /// <remarks>Port only.</remarks>
    public const int DemoWallClearanceArcadePixels = 24;

    /// <summary>The row of the screen where the story page prints its "ROBOTRON 2084" title, counted in rows.</summary>
    /// <remarks>
    ///     Original source: <c>SPGSUB</c> prints string 128 at the cursor (54, 36), which is column 54 and row 36, and
    ///     every attract-movie screen keeps it. The page script's <c>CLEARM</c> clears only from row 48 down, so the story
    ///     text scrolls under the title. The movie's story band therefore uses the ROM's own row (notes §96.3), while the
    ///     title screen keeps the port's own placement of the pair. Disassembly: the unlabelled attract screen code at <c>$79AF</c>, which prints it.
    /// </remarks>
    public const int StoryTitleRow = 36;

    /// <summary>
    ///     How many seconds the presentation page's ARCADE text pane (PRESENTED BY, DESIGNED BY VID KIDZ, FOR WILLIAMS) is
    ///     shown before it swaps to the port's credit pane.
    /// </summary>
    /// <remarks>
    ///     Port only (notes §107 and §140.1). It stays up twice as long as the credit pane, so the arcade's own credits can
    ///     be read. The page has no room for the arcade's message and credits and the port's credit line and F-key menu at
    ///     once, and the arcade's two message lines want an empty row between them (the ROM's own cursors, <c>$86</c> and
    ///     <c>$96</c>, are 16 rows apart on an 8-row line grid), so each pane gets the whole band to itself.
    /// </remarks>
    public const int TitleArcadeTextSeconds = 6;

    /// <summary>How many seconds the title sits idle before the demo takes over.</summary>
    /// <remarks>Port only.</remarks>
    public const int TitleIdleSeconds = 12;

    /// <summary>
    ///     How many seconds the presentation page's PORT text pane (the port's credit line and the F-key menu) is shown
    ///     before it swaps back to the arcade pane.
    /// </summary>
    /// <remarks>Port only.</remarks>
    public const int TitlePortTextSeconds = 3;

    /// <summary>
    ///     How many seconds the presentation page's bottom row shows the copyright line before the prompt for the menu page
    ///     takes its place, and how long the prompt stays before the copyright line is back.
    /// </summary>
    /// <remarks>Port only.</remarks>
    public const int TitleFooterSeconds = 2;

    /// <summary>The palette index of the wall on the title screen.</summary>
    /// <remarks>
    ///     Original source: the title screen routine at ROM <c>$79C8</c>, <c>LDA #$CC / STA $8F</c>, which makes the wall
    ///     one solid palette index. Disassembly: the unlabelled attract screen code at <c>$79C8</c>.
    /// </remarks>
    public const int TitleWallPaletteIndex = 12;
}
