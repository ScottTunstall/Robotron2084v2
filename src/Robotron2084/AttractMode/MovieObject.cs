using Robotron2084.Core;

namespace Robotron2084.AttractMode;

/// <summary>One character, laser bolt or other thing in the attract movie.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>It holds a sprite, a place, a speed and, for the walking characters, how it is walking.</item>
///         <item>It is plain data. The program that moves it is in <see cref="AttractObjectMachine" />.</item>
///         <item>
///             Places are kept the arcade's way: <see cref="XSubpixels" /> counts 1/256ths of a column and
///             <see cref="YSubpixels" /> counts 1/256ths of a row. So the whole column is <c>XSubpixels / 256</c> (a
///             column is 2 arcade pixels) and the whole row is <c>YSubpixels / 256</c>. The arcade packs the two into its
///             screen address as <c>column * 256 + row</c>.
///         </item>
///         <item>Original source: <c>RRF.ASM</c> the object fields <c>OX16</c> and <c>OY16</c> (notes §95.3)</item>
///         <item>Disassembly: not separately labelled</item>
///     </list>
/// </remarks>
public sealed record MovieObject
{
    /// <summary>Creates an object showing one animation frame, at a place given in the arcade's units.</summary>
    public MovieObject(MovieDescriptor? descriptor, int animationFrameIndex, int xSubpixels, int ySubpixels)
    {
        Descriptor = descriptor;
        AnimationFrameIndex = animationFrameIndex;
        XSubpixels = xSubpixels;
        YSubpixels = ySubpixels;
    }

    /// <summary>The texture set. Null for the movie's laser bolts (see <see cref="IsLaser" />).</summary>
    public MovieDescriptor? Descriptor { get; set; }

    /// <summary>A left/right laser bolt fired by LFIRE/RFIRE — drawn as the laser, killed on its timer.</summary>
    public bool IsLaser { get; set; }

    /// <summary>fiftieths of a second a laser bolt stays alive (LFIRE/RFIRE's first operand).</summary>
    public int LaserArcadeFramesLeft { get; set; }

    /// <summary>Which animation frame of the descriptor's animation the object is showing.</summary>
    public int AnimationFrameIndex { get; set; }

    /// <summary>X in 1/256 columns (256 = one column = 2 arcade px).</summary>
    public int XSubpixels { get; set; }

    /// <summary>Y in 1/256 rows (256 = one row = 1 arcade px).</summary>
    public int YSubpixels { get; set; }

    /// <summary>The sideways speed, in 1/256ths of a column in each 50th of a second.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>OXV</c></item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    public int XVelocitySubpixels { get; set; }

    /// <summary>The up-and-down speed, in 1/256ths of a row in each 50th of a second.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>OYV</c></item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    public int YVelocitySubpixels { get; set; }

    /// <summary>Whether the object is on the object list, so that it is drawn and moved.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             HIB takes the object off the list. It is still the same object, but it is neither drawn nor moved.
    ///             REBORN puts it back. MONO takes its object off the list too.
    ///         </item>
    ///         <item>Original source: <c>RRSCRIPT.ASM</c> <c>HIB</c>, <c>REBORN</c> and <c>MONO</c></item>
    ///         <item>Disassembly: the opcode routines at $7C54 (HIB), $7C5C (REBORN) and $7B90 (MONO)</item>
    ///     </list>
    /// </remarks>
    public bool IsOnList { get; set; } = true;

    /// <summary>The object's process(es) have all ended.</summary>
    public bool IsDead { get; set; }

    /// <summary>MONO: the box is filled in this palette index (0 = no box).</summary>
    public int MonoBoxPaletteIndex { get; set; }

    /// <summary>MONO: the object is drawn as a solid silhouette in this palette index.</summary>
    public int MonoSilhouettePaletteIndex { get; set; }

    /// <summary>MONO: whether the brain is also drawn inside the box.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>DMAON</c>, called by <c>RRSCRIPT.ASM</c> <c>MONOP</c> ("brain in the square")</item>
    ///         <item>Disassembly: the <c>MONO</c> opcode routine at $7B90</item>
    ///     </list>
    /// </remarks>
    public bool BrainVisible { get; set; }

    /// <summary>MONO is running (the renderer draws the box + solid silhouette).</summary>
    public bool IsMonoActive { get; set; }

    /// <summary>RPROG: how many rows the shake has moved the object up or down.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>The arcade changes <c>OBJY</c> directly to do this.</item>
    ///         <item>Original source: <c>RRSCRIPT.ASM</c> <c>RPROG</c>, with <c>RRF.ASM</c> <c>OBJY</c></item>
    ///         <item>Disassembly: the <c>RPROG</c> opcode routine at $7CD9</item>
    ///     </list>
    /// </remarks>
    public int ShakeRowOffset { get; set; }

    /// <summary>Gets the whole column the object is in.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>OBJX</c></item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    public int GetColumn()
    {
        return XSubpixels >> 8;
    }

    /// <summary>Gets the whole row the object is in, with the shake added.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>OBJY</c></item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    public int GetRow()
    {
        return (YSubpixels >> 8) + ShakeRowOffset;
    }

    /// <summary>The column as arcade pixels.</summary>
    public int GetArcadeX()
    {
        return GetColumn() * ScreenSize.ArcadePixelsPerByte;
    }

    /// <summary>The row as arcade pixels.</summary>
    public int GetArcadeY()
    {
        return GetRow();
    }
}
