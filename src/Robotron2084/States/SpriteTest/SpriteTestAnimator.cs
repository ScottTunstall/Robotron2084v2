using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;

namespace Robotron2084.States.SpriteTest;

/// <summary>Plays one <see cref="SpriteTestEntry" />'s animation, one tick at a time.</summary>
/// <remarks>
///     When the way held gets a different cycle, the new cycle starts from its first frame, as a walk does in the game
///     when the walker turns.
/// </remarks>
public sealed class SpriteTestAnimator
{
    private readonly SpriteTestEntry _entry;
    private readonly Random _random;
    private SpriteTestCycle _cycle;
    private int _clockUnitsLeft;

    /// <summary>Starts an entry on the first frame of the cycle it shows when no way is held, or of its down walk.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="random">Picks the hold of a frame whose cycle has a random hold.</param>
    public SpriteTestAnimator(SpriteTestEntry entry, Random random)
    {
        _entry = entry;
        _random = random;
        _cycle = entry.GetCycle(null) ?? entry.GetCycle(Direction8.Down) ??
            throw new ArgumentException("The entry has no cycle for no way held or for down.", nameof(entry));
        StartCycle(_cycle);
    }

    /// <summary>The place of the frame showing in its cycle, counting from 0.</summary>
    public int FrameIndex { get; private set; }

    /// <summary>How many frames the cycle showing has.</summary>
    public int FrameCount => _cycle.Frames.Count;

    /// <summary>How many times the frame has moved on to the next in its cycle. Starting a new cycle does not count.</summary>
    public int FrameChangeCount { get; private set; }

    /// <summary>Gets the animation frame showing.</summary>
    public Texture2D GetCurrentAnimationFrameTexture()
    {
        return _cycle.Frames[FrameIndex].Texture;
    }

    /// <summary>Moves the animation on by one tick.</summary>
    /// <param name="heldDirection">The way held, or null when none is.</param>
    public void Tick(Direction8? heldDirection)
    {
        var cycle = _entry.GetCycle(heldDirection);
        if (cycle is null) return;

        if (!ReferenceEquals(cycle, _cycle))
        {
            StartCycle(cycle);
            return;
        }

        _clockUnitsLeft -= ArcadeClock.UnitsPerPortTick;
        if (_clockUnitsLeft > 0) return;

        FrameIndex = (FrameIndex + 1) % _cycle.Frames.Count;
        FrameChangeCount++;
        _clockUnitsLeft += PickHold(FrameIndex);
    }

    /// <summary>Picks how long a frame of the cycle showing is shown this time.</summary>
    /// <param name="frameIndex">The frame's place in the cycle.</param>
    /// <returns>The hold, in <see cref="ArcadeClock.ClockUnitsPerSecond">clock units</see>.</returns>
    private int PickHold(int frameIndex)
    {
        return _cycle.Frames[frameIndex].HoldClockUnits * (1 + _random.Next(_cycle.RandomHoldMax));
    }

    /// <summary>Shows the first frame of a cycle.</summary>
    /// <param name="cycle">The cycle.</param>
    private void StartCycle(SpriteTestCycle cycle)
    {
        _cycle = cycle;
        FrameIndex = 0;
        _clockUnitsLeft = PickHold(0);
    }
}
