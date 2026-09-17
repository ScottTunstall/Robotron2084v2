using Robotron2084.Core;
using Robotron2084.Input;

namespace Robotron2084.Tests;

/// <summary>
/// Shared test double for <see cref="IPlayerInputSource"/>: returns a fixed
/// <see cref="PlayerInputState"/> (default = no movement, no fire) and counts
/// polls.
/// </summary>
public sealed class FakeInputSource : IPlayerInputSource
{
    private readonly PlayerInputState _state;

    public FakeInputSource()
        : this(default)
    {
    }

    public FakeInputSource(PlayerInputState state)
    {
        _state = state;
    }

    public int PollCount { get; private set; }

    public PlayerInputState Poll()
    {
        PollCount++;
        return _state;
    }
}
