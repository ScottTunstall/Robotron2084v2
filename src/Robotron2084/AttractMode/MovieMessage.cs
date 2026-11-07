namespace Robotron2084.AttractMode;

/// <summary>A name shown in the score row, such as a family member's name.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>RRSCRIPT.ASM</c> <c>MESSP</c>, with the message numbers in <c>RRFRED.ASM</c> and the text in <c>RRET.ASM</c></item>
///         <item>Disassembly: not separately labelled</item>
///     </list>
/// </remarks>
public readonly record struct MovieMessage(int X, int Y, string Text, int PaletteIndex);
