using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;

namespace Robotron2084.States.SpriteTest;

/// <summary>
///     A run of animation frames that a sprite shows over and over, each for as long as it is shown in the game.
/// </summary>
public sealed class SpriteTestCycle
{
    /// <summary>Makes a cycle.</summary>
    /// <param name="frames">The animation frames, in the order they are shown.</param>
    /// <param name="randomHoldMax">
    ///     The most times longer a frame can be shown than its own hold. Each time a frame comes up, its hold is multiplied
    ///     by a random number from 1 to this. It is 1 for a sprite that always changes frame at the same speed.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="frames" /> is empty.</exception>
    public SpriteTestCycle(IReadOnlyList<SpriteTestFrame> frames, int randomHoldMax = 1)
    {
        if (frames.Count == 0) throw new ArgumentException("A cycle needs at least one frame.", nameof(frames));

        Frames = frames;
        RandomHoldMax = randomHoldMax;
    }

    /// <summary>The animation frames, in the order they are shown.</summary>
    public IReadOnlyList<SpriteTestFrame> Frames { get; }

    /// <summary>
    ///     The most times longer a frame can be shown than its own hold. It is 1 for a sprite that always changes frame at
    ///     the same speed.
    /// </summary>
    public int RandomHoldMax { get; }

    /// <summary>Makes a cycle whose frames are all shown for the same number of arcade frames.</summary>
    /// <param name="textures">The animation frames, in the order they are shown.</param>
    /// <param name="holdArcadeFrames">How long each is shown, in arcade frames.</param>
    /// <param name="randomHoldMax">The most times longer a frame can be shown than <paramref name="holdArcadeFrames" />.</param>
    public static SpriteTestCycle CreateFromArcadeFrames(IEnumerable<Texture2D> textures, int holdArcadeFrames,
        int randomHoldMax = 1)
    {
        var holdClockUnits = ArcadeClock.ToClockUnits(holdArcadeFrames);
        return new SpriteTestCycle([.. textures.Select(texture => new SpriteTestFrame(texture, holdClockUnits))], randomHoldMax);
    }

    /// <summary>Makes a cycle whose frames are all shown for the same number of ticks.</summary>
    /// <param name="textures">The animation frames, in the order they are shown.</param>
    /// <param name="holdTicks">
    ///     How long each is shown, in <see cref="ArcadeClock.PortTicksPerSecond">ticks</see>.
    /// </param>
    public static SpriteTestCycle CreateFromTicks(IEnumerable<Texture2D> textures, int holdTicks)
    {
        var holdClockUnits = holdTicks * ArcadeClock.UnitsPerPortTick;
        return new SpriteTestCycle([.. textures.Select(texture => new SpriteTestFrame(texture, holdClockUnits))]);
    }

    /// <summary>Makes a cycle of one frame that never changes.</summary>
    /// <param name="texture">The frame.</param>
    public static SpriteTestCycle CreateStill(Texture2D texture)
    {
        return new SpriteTestCycle([new SpriteTestFrame(texture, ArcadeClock.ClockUnitsPerSecond)]);
    }
}
