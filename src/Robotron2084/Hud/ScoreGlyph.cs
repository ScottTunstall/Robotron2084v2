namespace Robotron2084.Hud;

/// <summary>A digit that is actually drawn, and the X the ROM's cursor puts it at.</summary>
/// <param name="Digit">The digit, 0-9.</param>
/// <param name="X">The cursor's X for this glyph.</param>
public readonly record struct ScoreGlyph(int Digit, int X);
