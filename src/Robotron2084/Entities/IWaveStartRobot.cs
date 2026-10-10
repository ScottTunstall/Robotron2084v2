using Robotron2084.Level;

namespace Robotron2084.Entities;

/// <summary>
///     A robot that is on the field when a wave starts. It stands still until the game goes live, and then makes its
///     first move a set time later.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: the head of each robot's routine, which sleeps and looks again until
///             <c>STATUS</c> is clear: <c>RRP8.ASM</c> <c>ROBOT</c>, <c>RRH11.ASM</c> <c>HULK</c>,
///             <c>RRB10.ASM</c> <c>BRAIN</c> and <c>RRTK4.ASM</c> <c>TANK</c>
///         </item>
///         <item>Disassembly: the status byte at <c>$59</c></item>
///     </list>
///     Each kind of robot waits its own length of time between looks, so each kind's first move comes a
///     different time after the game goes live (notes §143).
/// </remarks>
public interface IWaveStartRobot
{
    /// <summary>
    ///     Sets the time of the robot's first move. The playfield calls it once, on the tick the game goes live, before
    ///     it moves the robots on.
    /// </summary>
    /// <param name="field">The playfield, which works out how long the robot waits from how often it looks.</param>
    void BeginPlay(PlayField field);
}
