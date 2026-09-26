namespace Robotron2084.Hud;

/// <summary>One digit position: its value, and whether the ROM blanks it.</summary>
/// <param name="Value">The digit, 0-9.</param>
/// <param name="Suppressed">True when the ROM blanks this position (a leading zero, or the masked ten-millions digit).</param>
public readonly record struct ScoreDigit(int Value, bool Suppressed);
