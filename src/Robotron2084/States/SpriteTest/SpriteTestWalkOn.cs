namespace Robotron2084.States.SpriteTest;

/// <summary>How a sprite walks to the left in the game: how far each step goes, when the steps come, and how it rises.</summary>
/// <param name="StepPortPixels">
///     How far each step goes to the left, in port pixels. When there are several, they are taken in turn. A sprite that
///     steps as its frame changes takes the step for the frame it moves on to.
/// </param>
/// <param name="StepIntervalClockUnits">
///     The time between steps, in <see cref="Robotron2084.Core.ArcadeClock.ClockUnitsPerSecond">clock units</see>, or null
///     for a sprite that takes a step each time its frame changes, as a grunt does.
/// </param>
/// <param name="GetRisePortPixels">
///     Gets how high above the ground the sprite is after a number of steps, counting from 1, in port pixels; or null for
///     a sprite that stays on the ground, which is every sprite but Gorf.
/// </param>
public sealed record SpriteTestWalkOn(
    IReadOnlyList<int> StepPortPixels,
    int? StepIntervalClockUnits = null,
    Func<int, int>? GetRisePortPixels = null)
{
    /// <summary>Gets how far a step goes to the left.</summary>
    /// <param name="stepIndex">The step, counting from 0. It is the frame's place in the cycle for a sprite that steps as its frame changes.</param>
    /// <returns>How far it goes, in port pixels.</returns>
    public int GetStepPortPixels(int stepIndex)
    {
        return StepPortPixels[stepIndex % StepPortPixels.Count];
    }
}
