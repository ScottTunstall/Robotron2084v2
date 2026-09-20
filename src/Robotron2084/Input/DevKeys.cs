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
/// <item><b>F4</b> — jump straight to the high score TABLE (notes §98).</item>
/// </list>
///
/// They were F1/F2/F3 until the author asked for those to be the game's start keys
/// (ONE PLAYER / TWO PLAYER ALTERNATE / TWO PLAYER SIMULTANEOUS) on every attract
/// screen.
/// </summary>
public static class DevKeys
{
    /// <summary>How many times a tick the movie's frame clock runs while F3 is held.</summary>
    public const int AttractFastForwardMultiplier = 8;

    /// <summary>True while F3 is held (set by the app shell every tick).</summary>
    public static bool AttractFastForward { get; set; }
}
