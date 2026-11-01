namespace Robotron2084.Level;

/// <summary>The start of one record in the table of settings.</summary>
/// <remarks>Disassembly: the records at <c>$2C20</c>.</remarks>
/// <param name="Multiplier">
///     The byte that says how big a step to take, and whether the value falls as the difficulty
///     rises.
/// </param>
/// <param name="Min">The smallest value the setting may have.</param>
/// <param name="Max">The biggest value the setting may have.</param>
internal readonly record struct DifficultyTableHeader(int Multiplier, int Min, int Max);
