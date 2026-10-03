namespace Robotron2084.Tuning;

/// <summary>How Gorf crosses the screen: how fast, and how long its hops are and how high. Gorf is the author's own robot, so none of these come from the arcade.</summary>
public static class GorfTuning
{
    /// <summary>How many ROM frames Gorf waits between steps.</summary>
    public const int StepRomFrames = 2;

    /// <summary>How far Gorf goes sideways each step, in columns (2 arcade pixels each).</summary>
    public const int StepColumns = 1;

    /// <summary>How many steps one hop takes, from leaving the ground to landing.</summary>
    public const int HopSteps = 16;

    /// <summary>How high every hop goes, in rows (one arcade pixel each). They are all the same height, 16 for now.</summary>
    public const int HopRows = 16;

    /// <summary>How many ROM frames each of Gorf's two animation frames shows for.</summary>
    public const int AnimationFrameRomFrames = 8;
}
