namespace Robotron2084.Tuning;

/// <summary>The title, story and demo (attract mode) tuning.</summary>
public static class AttractTuning
{
    /// <summary>
    /// AI stick minimum hold: once the stick follows a new direction it keeps it
    /// this many ticks. The arcade's walk animation is a four-frame cycle at three
    /// ticks a frame (twelve ticks), so a direction that lasts fewer ticks than that
    /// can never show a complete walk (notes §97.5).
    /// </summary>
    public const int DemoDirectionHoldTicks = 9;

    /// <summary>
    /// AI stick hysteresis: a freshly computed flee/drift direction must win this
    /// many ticks IN A ROW before the stick follows it. The flee direction is
    /// `sign(player − robot)` recomputed against a moving field, so without hysteresis it flipped on
    /// ~75% of ticks; the arcade's walk animation RESETS on every facing change
    /// (R5 $3003-3009), which makes the demo's man twitch in place instead of walking
    /// (notes §97.5).
    /// </summary>
    public const int DemoDirectionSwitchTicks = 3;

    public const int DemoFireRangeArcadePixels = 120;

    public const int DemoStutterChanceDenominator = 16;

    public const int DemoThreatDistanceArcadePixels = 60;

    public const int DemoWallClearanceArcadePixels = 24;

    /// <summary>
    /// ROM `SPGSUB` ($79AF) prints string 128 at the cursor (54, 36) — column 54,
    /// row 36 — and every attract-movie screen keeps it: the page script's CLEARM
    /// only clears from row 48 down, so the story text scrolls UNDER the title.
    /// The movie's story band therefore uses the ROM's own row (notes §96.3); the
    /// title screen keeps the port's own placement of the pair.
    /// </summary>
    public const int StoryTitleRow = 36;

    public const int TitleIdleSeconds = 12;

    // AI: 1-in-N ticks of deliberate pause (feels alive, not robotic)
    /// <summary>
    /// How long the presentation page's ARCADE text pane (PRESENTED BY, DESIGNED BY VID KIDZ, FOR WILLIAMS) is shown before it swaps to the port's credit pane.
    /// It stays up twice as long as the credit pane, so the arcade's own credits can be read (notes §140.1).
    /// The page has no room for the arcade's message and credits and the port's credit
    /// and F-key menu at once — and the arcade's two message lines want an empty row between them
    /// (the ROM's own cursors, `$86`/`$96`, are 16 rows apart on an 8-row line grid) — so each pane
    /// gets the whole band to itself. Notes §107.
    /// </summary>
    public const int TitleArcadeTextSeconds = 6;

    /// <summary>How long the presentation page's PORT text pane (the port's credit line and the F-key menu) is shown before it swaps back to the arcade pane.</summary>
    public const int TitlePortTextSeconds = 3;

    // Attract mode (notes §94) — the arcade's attract cycle: idle
    // title, then the machine plays itself (CMOS "FANCY ATTRACT MODE" when on).
    public const int TitleWallSlot = 12; // ROM title screen: $79C8 LDA #$CC / STA $8F — the wall is solid slot 12

    // port choice: how long the title sits before the demo takes over
    // AI: flee a robot closer than this (arcade AI is OS-ROM-only, §94.3)

    // AI: fire at the nearest robot within this

    // AI: steer away from a wall within this
}
