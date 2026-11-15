namespace Robotron2084.AttractMode.Models;

/// <summary>An explosion the movie asks for when a character is shot.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             The character is taken away, and the explosion is made from the sprite it was showing, at the character's
///             own corner. Its centre row is always the same.
///         </item>
///         <item>Original source: <c>RRSCRIPT.ASM</c> <c>EXP</c> and <c>EXPP</c>, with <c>RRF.ASM</c> <c>KILLOF</c> (notes §95.5)</item>
///         <item>Disassembly: the <c>EXP</c> opcode routine at $7C6F, which calls <c>MAKE_ENEMY_EXPLODE</c> ($5C1F)</item>
///     </list>
/// </remarks>
/// <param name="Animation">The sprite the character was drawn with.</param>
/// <param name="AnimationFrameIndex">The animation frame of that sprite it was showing.</param>
/// <param name="Column">The column of the character's top-left corner.</param>
/// <param name="Row">The row of the character's top-left corner.</param>
public readonly record struct MovieExplosion(MovieAnimation Animation, int AnimationFrameIndex, int Column, int Row);
