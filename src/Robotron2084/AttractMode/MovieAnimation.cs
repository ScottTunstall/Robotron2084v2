namespace Robotron2084.AttractMode;

/// <summary>Which sprite a character in the attract movie is drawn with.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>The names are the arcade's own labels where there are any.</item>
///         <item>
///             <see cref="Quark" /> is the arcade's <c>SQUARE</c> ($7EA5). Its animation frame table, at $50C2, is the
///             quark's SQP animation frames.
///         </item>
///         <item><see cref="Player" /> is the arcade's <c>YOU</c>, and <see cref="Cruise" /> is its <c>CRUSM</c>.</item>
///         <item>Original source: <c>RRSCRIPT.ASM</c> descriptors such as <c>MOMMY</c> and <c>BRAIN</c> (notes §95.6)</item>
///         <item>Disassembly: the unlabelled descriptors from $7E41</item>
///     </list>
/// </remarks>
public enum MovieAnimation
{
    Mommy,
    Daddy,
    Mikey,
    Hulk,
    Brain,
    Grunt,
    Electrodes,
    Enforcer,
    Player,
    Quark,
    Spheroid,
    TankGrow,
    Tank,
    Points,
    Skull,
    Cruise
}
