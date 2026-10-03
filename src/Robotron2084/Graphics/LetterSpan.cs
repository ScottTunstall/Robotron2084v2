namespace Robotron2084.Graphics;

/// <summary>Where one letter sits across a picture of a word: the first column it covers, and how many.</summary>
/// <param name="Start">The first column, in the picture's own pixels.</param>
/// <param name="Width">How many columns it covers.</param>
public readonly record struct LetterSpan(int Start, int Width);
