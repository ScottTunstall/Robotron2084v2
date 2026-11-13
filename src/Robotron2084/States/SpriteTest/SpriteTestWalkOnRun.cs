using Robotron2084.Core;

namespace Robotron2084.States.SpriteTest;

/// <summary>One walk of a sprite onto the SPRITE TEST page from the right, stepping as it does in the game.</summary>
public sealed class SpriteTestWalkOnRun
{
    private readonly SpriteTestWalkOn _walkOn;
    private int _clockUnitsToNextStep;
    private int _frameChangesSeen;
    private int _stepsTaken;

    /// <summary>Starts a walk.</summary>
    /// <param name="walkOn">How the sprite steps.</param>
    /// <param name="startX">Where the sprite's left edge starts, in port pixels from the left.</param>
    /// <param name="animator">The sprite's animator, as it is when the walk starts.</param>
    public SpriteTestWalkOnRun(SpriteTestWalkOn walkOn, int startX, SpriteTestAnimator animator)
    {
        _walkOn = walkOn;
        X = startX;
        _frameChangesSeen = animator.FrameChangeCount;
        _clockUnitsToNextStep = walkOn.StepIntervalClockUnits ?? 0;
    }

    /// <summary>Where the sprite's left edge is, in port pixels from the left.</summary>
    public int X { get; private set; }

    /// <summary>How high above the ground the sprite is, in port pixels. It is 0 for every sprite but Gorf.</summary>
    public int Rise { get; private set; }

    /// <summary>
    ///     Takes the steps due this tick. Call it after the animator has had its tick, so that a sprite that steps as its
    ///     frame changes takes the step for the frame it has just moved on to.
    /// </summary>
    /// <param name="animator">The sprite's animator.</param>
    public void Tick(SpriteTestAnimator animator)
    {
        if (_walkOn.StepIntervalClockUnits is not { } stepIntervalClockUnits)
        {
            if (animator.FrameChangeCount == _frameChangesSeen) return;

            _frameChangesSeen = animator.FrameChangeCount;
            TakeStep(animator.FrameIndex);
            return;
        }

        _clockUnitsToNextStep -= ArcadeClock.UnitsPerPortTick;
        while (_clockUnitsToNextStep <= 0)
        {
            _clockUnitsToNextStep += stepIntervalClockUnits;
            TakeStep(_stepsTaken);
        }
    }

    /// <summary>Takes one step to the left, and rises or falls to where that step takes the sprite.</summary>
    /// <param name="stepIndex">Which of the walk's steps it is (<see cref="SpriteTestWalkOn.GetStepPortPixels" />).</param>
    private void TakeStep(int stepIndex)
    {
        X -= _walkOn.GetStepPortPixels(stepIndex);
        _stepsTaken++;
        Rise = _walkOn.GetRisePortPixels?.Invoke(_stepsTaken) ?? 0;
    }
}
