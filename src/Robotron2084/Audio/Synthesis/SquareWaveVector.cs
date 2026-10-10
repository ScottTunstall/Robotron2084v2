namespace Robotron2084.Audio.Synthesis;

/// <summary>The settings for one square wave sound, as the sound board's program stores them: nine bytes.</summary>
/// <remarks>Original source: <c>VSNDRM3.SRC</c>, the <c>VARIWAVE PARAMETERS</c> block and the rows of <c>VVECT</c>.</remarks>
/// <param name="LowWait">How long the wave stays low each time, in counts (<c>LOPER</c>).</param>
/// <param name="HighWait">How long the wave stays high each time, in counts (<c>HIPER</c>).</param>
/// <param name="LowWaitStep">How much the low wait grows at each sweep (<c>LODT</c>).</param>
/// <param name="HighWaitStep">How much the high wait grows at each sweep (<c>HIDT</c>).</param>
/// <param name="HighWaitEnd">The high wait at which the sweeps stop (<c>HIEN</c>).</param>
/// <param name="SweepLength">How many counts pass between sweeps (<c>SWPDT</c>, two bytes).</param>
/// <param name="LowWaitChange">
///     How much the starting low wait changes when the sweeps stop, or 0 to end the sound (
///     <c>LOMOD</c>).
/// </param>
/// <param name="Level">The level the sound starts by writing (<c>VAMP</c>).</param>
internal readonly record struct SquareWaveVector(
    byte LowWait,
    byte HighWait,
    byte LowWaitStep,
    byte HighWaitStep,
    byte HighWaitEnd,
    ushort SweepLength,
    byte LowWaitChange,
    byte Level);
