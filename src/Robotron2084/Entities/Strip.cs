namespace Robotron2084.Entities;

/// <summary>One thin slice of a sprite: a single row of its pixels, or a single column, and the place on the screen it goes.</summary>
/// <remarks>
///     A sprite is cut into strips so they can be pulled apart or pushed back together (see <see cref="StripEffect" />). Whether the
///     strips are rows or columns depends on how the sprite is cut (see <see cref="StripFanAxis" />). A strip does not hold any pixels
///     of its own. It only says which slice of the sprite it is and where that slice is drawn.
/// </remarks>
/// <param name="SourceIndex">
///     Which slice of the sprite this is, counting from 0. When the sprite is cut into rows it is the row number, counting down from
///     the top. When the sprite is cut into columns it is the column number, counting across from the left.
/// </param>
/// <param name="X">How far across the screen the left edge of the strip goes, in arcade pixels from the left side of the screen.</param>
/// <param name="Y">How far down the screen the top edge of the strip goes, in arcade pixels from the top of the screen.</param>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>AWRITE</c> in <c>RRX7.ASM</c>, <c>RRHX4.ASM</c> and <c>RRDX2.ASM</c>, which draw each strip
///             of a sprite as it comes apart or comes together
///         </item>
///         <item>Disassembly: the three strip routines at <c>$5B40</c>, <c>$F000</c> and <c>$4680</c></item>
///     </list>
/// </remarks>
public readonly record struct Strip(int SourceIndex, int X, int Y);
