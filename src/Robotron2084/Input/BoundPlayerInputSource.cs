namespace Robotron2084.Input;

/// <summary>Reads one player's input.</summary>
public sealed class BoundPlayerInputSource : IPlayerInputSource
{
    /// <summary>The controls to read.</summary>
    private readonly ControlSettings _settings;

    /// <summary>0 for player 1, 1 for player 2.</summary>
    private readonly int _playerIndex;

    /// <summary>Creates a source that reads one player's input.</summary>
    /// <param name="settings">The controls to read.</param>
    /// <param name="playerIndex">0 for player 1, 1 for player 2.</param>
    public BoundPlayerInputSource(ControlSettings settings, int playerIndex)
    {
        _settings = settings;
        _playerIndex = playerIndex;
    }

    /// <summary>Reads the player's input for this tick.</summary>
    public PlayerInputState Poll() => Poll(InputSnapshot.Read());

    /// <summary>Reads the player's input from a given snapshot — the seam the tests use.</summary>
    /// <param name="snapshot">The snapshot to read from.</param>
    public PlayerInputState Poll(InputSnapshot snapshot) =>
        _settings.ReadPlayer(_playerIndex, snapshot.Keys, snapshot.PadOne, snapshot.PadTwo);
}
