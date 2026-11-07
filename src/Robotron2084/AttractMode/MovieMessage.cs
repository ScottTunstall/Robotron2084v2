namespace Robotron2084.AttractMode;

/// <summary>A name shown in the score row, such as a family member's name.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: the <c>MESS</c> action and the arcade's message strings</item>
///         <item>Disassembly: not separately labelled</item>
///     </list>
/// </remarks>
public readonly record struct MovieMessage(int X, int Y, string Text, int PaletteIndex);
