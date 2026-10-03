namespace RobotronSoundPlayer.Tests;

/// <summary>A stretch of time a board held one output level.</summary>
/// <param name="Level">The level.</param>
/// <param name="Cycles">How many clock cycles it was held.</param>
internal readonly record struct LevelRun(byte Level, long Cycles);
