namespace Robotron2084.Entities;

/// <summary>Which way the dying sprite is cut up: <see cref="Rows"/> fans up/down, <see cref="Columns"/> left/right.</summary>
/// <remarks>ROM: separate code per axis — <see cref="Rows"/> by the vertical-fan code
/// (RRX7.ASM/RRDX2.ASM), <see cref="Columns"/> by the horizontal-fan code (RRHX4.ASM).</remarks>
public enum StripFanAxis
{
    Rows,
    Columns,
}
