namespace Robotron2084.Tuning;

/// <summary>How Gorf crosses the screen: how fast, and how big and how long its wave is. Gorf is the author's own robot, so none of these come from the arcade.</summary>
public static class GorfTuning
{
    /// <summary>How many ROM frames Gorf waits between steps.</summary>
    public const int StepRomFrames = 2;

    /// <summary>How far Gorf goes sideways each step, in columns (2 arcade pixels each).</summary>
    public const int StepColumns = 1;

    /// <summary>How many steps one whole wave takes: up, back down through the middle, below it, and back up.</summary>
    public const int WavePeriodSteps = 80;

    /// <summary>How far, in rows, the wave goes above and below the line Gorf is flying along.</summary>
    public const int AmplitudeRows = 30;

    /// <summary>How many ROM frames each of Gorf's two animation frames shows for.</summary>
    public const int AnimationFrameRomFrames = 8;
}
