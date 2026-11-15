namespace Robotron2084.AttractMode.Models;

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
    /// <summary>Mommy.</summary>
    Mommy,

    /// <summary>Daddy.</summary>
    Daddy,

    /// <summary>Mikey.</summary>
    Mikey,

    /// <summary>A hulk.</summary>
    Hulk,

    /// <summary>A brain.</summary>
    Brain,

    /// <summary>A grunt.</summary>
    Grunt,

    /// <summary>The electrodes.</summary>
    Electrodes,

    /// <summary>An enforcer.</summary>
    Enforcer,

    /// <summary>The player (the arcade's <c>YOU</c>).</summary>
    Player,

    /// <summary>A quark (the arcade's <c>SQUARE</c>).</summary>
    Quark,

    /// <summary>A spheroid.</summary>
    Spheroid,

    /// <summary>A tank that is still growing.</summary>
    TankGrow,

    /// <summary>A tank.</summary>
    Tank,

    /// <summary>The points a kill is worth.</summary>
    Points,

    /// <summary>The skull a dead family member leaves.</summary>
    Skull,

    /// <summary>A cruise missile (the arcade's <c>CRUSM</c>).</summary>
    Cruise
}
