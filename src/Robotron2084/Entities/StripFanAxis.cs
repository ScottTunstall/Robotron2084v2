namespace Robotron2084.Entities;

/// <summary>Which way a sprite is cut into strips: into rows or into columns.</summary>
/// <remarks>
///     ROM: each has its own code. <see cref="Rows" /> are done by the vertical code
///     (RRX7.ASM/RRDX2.ASM) and <see cref="Columns" /> by the horizontal code (RRHX4.ASM).
/// </remarks>
public enum StripFanAxis
{
    /// <summary>Cut into rows, which move up and down.</summary>
    Rows,

    /// <summary>Cut into columns, which move left and right.</summary>
    Columns
}
