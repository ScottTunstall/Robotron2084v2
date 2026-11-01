namespace Robotron2084.Level;

/// <summary>One row of the table: the four settings for a wave, in the table's own order.</summary>
/// <param name="SpheroidDropDelay">How long a spheroid waits before dropping an enforcer.</param>
/// <param name="EnforcerFireDelay">How long an enforcer waits between sparks.</param>
/// <param name="GruntMoveDelay">The longest a grunt waits between moves, in beats.</param>
/// <param name="GruntSpeedFloor">The fewest beats the grunts' speed-ups may bring a grunt's longest wait down to.</param>
internal sealed record BozoModeRow(int SpheroidDropDelay, int EnforcerFireDelay, int GruntMoveDelay, int GruntSpeedFloor);
