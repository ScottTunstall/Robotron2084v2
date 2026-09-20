namespace Robotron2084.Input;

/// <summary>
/// Port-only DEVELOPMENT switches (notes §97). Nothing here has an arcade
/// counterpart — these exist so the attract sequence can be inspected without
/// sitting out the title's 12-second idle, and they are read only by the
/// attract states. The keys themselves are handled in <c>RobotronGame.Update</c>:
/// <list type="bullet">
/// <item><b>F1</b> — jump straight into the attract STORYLINE movie (the family,
/// the hulk, the grunts, the spheroid/tank/enforcer scene, the brain);</item>
/// <item><b>F2</b> — jump straight into the attract DEMO game (the phony player
/// rescuing the family and being killed by the robots);</item>
/// <item><b>F3</b> — HELD, fast-forward the movie's ROM frame clock so a later
/// scene (the hulk's walk, at ROM frame ~2574) can be reached without waiting
/// out the text crawl.</item>
/// </list>
/// </summary>
public static class DevKeys
{
    /// <summary>How many times a tick the movie's frame clock runs while F3 is held.</summary>
    public const int AttractFastForwardMultiplier = 8;

    /// <summary>True while F3 is held (set by the app shell every tick).</summary>
    public static bool AttractFastForward { get; set; }
}
