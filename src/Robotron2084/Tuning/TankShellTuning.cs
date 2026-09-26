namespace Robotron2084.Tuning;

/// <summary>The tank shell's speed and lifetime.</summary>
public static class TankShellTuning
{
    // Tank shell — ROM R5 4F82-4F8A: lifespan = (RND & $1F) + $30
    // ROM ticks (48..79); the shell flies straight, aimed once at spawn, and
    // bounces off all four walls (4F94-4FCD).
    public const int Speed = 5;

    public const int LifeBaseRomFrames = 48;
}
