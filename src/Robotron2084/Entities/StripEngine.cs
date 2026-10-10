namespace Robotron2084.Entities;

/// <summary>
///     Which of the arcade's three strip routines a <see cref="StripEffect" /> is run by. Each routine has its own
///     speed for an appear and its own store of records, so a full store stops one kind of effect and not the others.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRX7.ASM</c> (<see cref="Vertical" />), <c>RRHX4.ASM</c> (<see cref="Horizontal" />)
///             and <c>RRDX2.ASM</c> (<see cref="Diagonal" />)
///         </item>
///         <item>
///             Disassembly: <c>$5B40</c> (<see cref="Vertical" />), <c>$F000</c> (<see cref="Horizontal" />) and
///             <c>$4680</c> (<see cref="Diagonal" />)
///         </item>
///     </list>
///     The arcade names each
///     routine for the way its pieces move, so the vertical one cuts a sprite into rows and the horizontal one
///     cuts it into columns.
/// </remarks>
public enum StripEngine
{
    /// <summary>
    ///     Rows that move straight up and down. Its records are taken from the arcade's list of free objects, which this
    ///     game does not keep, so it never runs out here.
    /// </summary>
    Vertical,

    /// <summary>Columns that move left and right.</summary>
    Horizontal,

    /// <summary>Rows that move up and down and lean to one side.</summary>
    Diagonal
}
