using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>
///     One door in a wall: two panels that slide apart from the middle of the opening to open it, and slide back to shut it,
///     like a sliding door on a starship.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Port only: the arcade's playfield has no doors.</item>
///         <item>
///             It slides open, stays open, and slides shut (<see cref="WallDoorTuning" />), counting
///             <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.
///         </item>
///         <item>All the sums are whole numbers.</item>
///     </list>
/// </remarks>
public sealed class WallDoor
{
    private const int TotalTicks = WallDoorTuning.OpenTicks + WallDoorTuning.HoldTicks + WallDoorTuning.CloseTicks;

    private int _ticksSinceStart;

    /// <summary>Makes a door that has just started to open.</summary>
    /// <param name="side">Which wall the door is in.</param>
    /// <param name="centre">
    ///     Where the middle of the opening is along the wall, in port pixels: across from the left edge of the screen for
    ///     the top and bottom walls, down from the top for the walls at the sides.
    /// </param>
    /// <param name="gapLength">How long the opening is along the wall, in port pixels.</param>
    public WallDoor(WallDoorSide side, int centre, int gapLength)
    {
        Side = side;
        Centre = centre;
        GapLength = gapLength;
    }

    /// <summary>Which wall the door is in.</summary>
    public WallDoorSide Side { get; }

    /// <summary>Where the middle of the opening is along the wall, in port pixels.</summary>
    public int Centre { get; }

    /// <summary>How long the opening is along the wall, in port pixels, when the door is shut.</summary>
    public int GapLength { get; }

    /// <summary>Where the opening starts along the wall, in port pixels.</summary>
    public int GetStart()
    {
        return Centre - GapLength / 2;
    }

    /// <summary>Where the opening ends along the wall, in port pixels.</summary>
    public int GetEnd()
    {
        return GetStart() + GapLength;
    }

    /// <summary>Gets whether the door is wide open: the panels are all the way apart.</summary>
    public bool IsWideOpen()
    {
        return _ticksSinceStart >= WallDoorTuning.OpenTicks &&
               _ticksSinceStart < WallDoorTuning.OpenTicks + WallDoorTuning.HoldTicks;
    }

    /// <summary>Gets whether the door has slid shut again, and is no longer needed.</summary>
    public bool IsFinished()
    {
        return _ticksSinceStart >= TotalTicks;
    }

    /// <summary>Gets how long the opening is now, in port pixels: 0 when the door is shut, the whole gap when it is wide open.</summary>
    public int GetOpenLength()
    {
        if (_ticksSinceStart < WallDoorTuning.OpenTicks) return GapLength * _ticksSinceStart / WallDoorTuning.OpenTicks;

        if (_ticksSinceStart < WallDoorTuning.OpenTicks + WallDoorTuning.HoldTicks) return GapLength;

        var ticksLeft = Math.Max(0, TotalTicks - _ticksSinceStart);
        return GapLength * ticksLeft / WallDoorTuning.CloseTicks;
    }

    /// <summary>Moves the door on by one tick.</summary>
    public void Tick()
    {
        _ticksSinceStart++;
    }
}
