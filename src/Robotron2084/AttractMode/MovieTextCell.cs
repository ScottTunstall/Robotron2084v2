namespace Robotron2084.AttractMode;

/// <summary>One character the movie has printed: where it is, and which palette index it is in.</summary>
/// <remarks>
///     The arcade draws each character straight onto the screen. The port keeps them in a list instead, so they can be
///     drawn with the arcade font.
/// </remarks>
public readonly record struct MovieTextCell(int X, int Y, char Character, int PaletteIndex);
