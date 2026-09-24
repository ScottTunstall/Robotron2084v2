namespace Robotron2084.Input;

/// <summary>
/// Port-only DEVELOPMENT switches (notes §97, re-keyed in §101). Nothing here has an
/// arcade counterpart — these exist so the attract sequence can be inspected without
/// sitting out the title's 12-second idle, and they are read only by the attract
/// states. The keys themselves are handled in <c>RobotronGame.Update</c>:
/// <list type="bullet">
/// <item><b>F5</b> — jump straight into the attract STORYLINE movie (the family,
/// the hulk, the grunts, the spheroid/tank/enforcer scene, the brain);</item>
/// <item><b>F6</b> — jump straight into the attract DEMO game (the phony player
/// rescuing the family and being killed by the robots);</item>
/// <item><b>F7</b> — HELD, fast-forward the movie's ROM frame clock so a later
/// scene (the hulk's walk, at ROM frame ~2574) can be reached without waiting
/// out the text crawl;</item>
/// <item><b>F4</b> — jump straight to the high score TABLE (notes §98);</item>
/// <item><b>F9</b> — jump straight to the END OF A GAME: the GAME OVER page and
/// the score ceremony that follows it (the initials screen and the table), carrying
/// a score high enough to qualify (notes §116).</item>
/// </list>
///
/// They were F1/F2/F3 until the author asked for those to be the game's start keys
/// (ONE PLAYER / TWO PLAYER ALTERNATE / TWO PLAYER SIMULTANEOUS) on every attract
/// screen.
/// </summary>
public static class DevKeys
{
    /// <summary>How many times a tick the movie's frame clock runs while the attract fast-forward key is
    /// held (see <see cref="RobotronGame.HandleAttractDevKeys"/>).</summary>
    public const int AttractFastForwardMultiplier = 8;

    /// <summary>
    /// The score the F9 end-game jump carries: it beats TODAY's lowest entry (CJM 24110) and the
    /// all-time list's blank tail, so both list checks pass and the initials screen always appears.
    /// </summary>
    public const int QualifyingScore = 45000;

    /// <summary>True while the attract fast-forward key is held (set by the app shell every tick).</summary>
    public static bool AttractFastForward { get; set; }
}
