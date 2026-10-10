namespace Robotron2084.Audio.Synthesis;

/// <summary>One step of a sound: the board keeps its output level for a while, then changes it.</summary>
/// <param name="CyclesBefore">How many clock cycles pass before the change, counting the instruction that makes it.</param>
/// <param name="Level">The new output level, 0 to 255.</param>
/// <param name="WriteCycles">
///     How long the instruction that writes the level takes, at the end of <paramref name="CyclesBefore" />, or 0
///     when the step only lets time pass. A sound number that arrives once that instruction has started waits
///     for it to finish, as the real processor does.
/// </param>
internal readonly record struct OutputChange(int CyclesBefore, byte Level, int WriteCycles);
