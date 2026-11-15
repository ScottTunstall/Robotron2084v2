namespace Robotron2084.AttractMode.Models;

/// <summary>A name shown in the score row, such as a family member's name.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>RRSCRIPT.ASM</c> <c>MESSP</c>, with the message numbers in <c>RRFRED.ASM</c> and the text in <c>RRET.ASM</c></item>
///         <item>Disassembly: <c>PRINT_CHARACTER_NAME_FROM_SCRIPT</c> ($7A60), with the message pointer table at $6377</item>
///     </list>
/// </remarks>
/// <param name="X">Where the text starts across the screen, in arcade pixels.</param>
/// <param name="Y">The row the text is printed on.</param>
/// <param name="Text">The words to show.</param>
/// <param name="PaletteIndex">The palette index the text is drawn in.</param>
public readonly record struct MovieMessage(int X, int Y, string Text, int PaletteIndex);
