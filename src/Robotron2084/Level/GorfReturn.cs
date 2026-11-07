namespace Robotron2084.Level;

/// <summary>A Gorf that is to come back after one was shot with grunts still to drop. Port only (notes §152).</summary>
public sealed class GorfReturn
{
    private int _ticksLeft;

    /// <summary>Makes a return that is waiting to happen.</summary>
    /// <param name="gruntsOwed">How many grunts the new Gorf drops.</param>
    /// <param name="dropIntervalSteps">How many steps there are between its drops.</param>
    /// <param name="delayTicks">How many <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> to wait before it comes on.</param>
    public GorfReturn(int gruntsOwed, int dropIntervalSteps, int delayTicks)
    {
        GruntsOwed = gruntsOwed;
        DropIntervalSteps = dropIntervalSteps;
        _ticksLeft = delayTicks;
    }

    /// <summary>How many grunts the new Gorf drops.</summary>
    public int GruntsOwed { get; }

    /// <summary>How many steps there are between its drops.</summary>
    public int DropIntervalSteps { get; }

    /// <summary>Gets whether the wait is over and the new Gorf is to come on.</summary>
    public bool IsDue()
    {
        return _ticksLeft <= 0;
    }

    /// <summary>Moves the wait on by one tick.</summary>
    public void Tick()
    {
        _ticksLeft--;
    }
}
