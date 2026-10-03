namespace Robotron2084.Tuning;

/// <summary>How Gorf crosses the screen: how fast, and how long and how high its hops are. Gorf is the author's own robot, so none of these come from the arcade.</summary>
public static class GorfTuning
{
    /// <summary>How many ROM frames Gorf waits between steps.</summary>
    public const int StepRomFrames = 2;

    /// <summary>How far Gorf goes sideways each step, in columns (2 arcade pixels each).</summary>
    public const int StepColumns = 1;

    /// <summary>How many steps one hop takes, from leaving the ground to landing.</summary>
    public const int HopSteps = 16;

    /// <summary>The highest a hop goes, in rows (one arcade pixel each). Each hop rolls its own height up to this.</summary>
    public const int MaxHopRows = 16;

    /// <summary>How many ROM frames each of Gorf's two animation frames shows for.</summary>
    public const int AnimationFrameRomFrames = 8;
}
