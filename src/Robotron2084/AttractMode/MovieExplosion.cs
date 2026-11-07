namespace Robotron2084.AttractMode;

/// <summary>An explosion the movie asks for when a character is shot.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             The character is taken away, and the explosion is made from the sprite it was showing, at the character's
///             own corner. Its centre row is always the same.
///         </item>
///         <item>Original source: the <c>EXP</c> opcode, <c>KILLOF</c> and <c>EXPP</c> (notes §95.5)</item>
///         <item>Disassembly: not separately labelled</item>
///     </list>
/// </remarks>
public readonly record struct MovieExplosion(MovieAnimation Animation, int AnimationFrameIndex, int Column, int Row);
