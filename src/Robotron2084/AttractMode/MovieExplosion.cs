namespace Robotron2084.AttractMode;

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
public readonly record struct MovieExplosion(MovieAnimation Animation, int AnimationFrameIndex, int Column, int Row);
