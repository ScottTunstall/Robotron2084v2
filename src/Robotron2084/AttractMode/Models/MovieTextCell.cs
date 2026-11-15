namespace Robotron2084.AttractMode.Models;

/// <summary>One character the movie has printed: where it is, and which palette index it is in.</summary>
/// <remarks>
///     The arcade draws each character straight onto the screen. The port keeps them in a list instead, so they can be
///     drawn with the arcade font.
/// </remarks>
/// <param name="X">Where the character is across the screen, in arcade pixels.</param>
/// <param name="Y">The row the character is on.</param>
/// <param name="Character">The character.</param>
/// <param name="PaletteIndex">The palette index it is drawn in.</param>
public readonly record struct MovieTextCell(int X, int Y, char Character, int PaletteIndex);
