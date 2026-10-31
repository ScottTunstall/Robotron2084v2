namespace Robotron2084.Entities;

/// <summary>Which way a sprite is cut into strips: into rows or into columns.</summary>
/// <remarks>
///     Each way of cutting has its own routine in the arcade: rows are done by the vertical and diagonal routines, and
///     columns by the horizontal one.
///     <list type="bullet">
///         <item>Original source: <c>RRX7.ASM</c> and <c>RRDX2.ASM</c> for rows, <c>RRHX4.ASM</c> for columns</item>
///         <item>
///             Disassembly: the entry tables at <c>$5B40</c> (vertical), <c>$4680</c> (diagonal) and <c>$F000</c>
///             (horizontal); see <see cref="StripEngine" />
///         </item>
///     </list>
/// </remarks>
public enum StripFanAxis
{
    /// <summary>Cut into rows, which move up and down.</summary>
    Rows,

    /// <summary>Cut into columns, which move left and right.</summary>
    Columns
}
