using Microsoft.Xna.Framework.Input;

namespace Robotron2084.Input;

/// <summary>
/// One player's input, read through their DEFINED controls (notes §101) instead of a
/// hard-coded scheme. Replaces the old keyboard/gamepad/composite trio: with bindings
/// there is nothing to arbitrate between, because each line says which device slot it
/// came from and both are always live.
/// </summary>
public sealed class BoundPlayerInputSource : IPlayerInputSource
{
    private readonly ControlSettings _settings;
    private readonly int _playerIndex;

    /// <param name="playerIndex">0 for player 1, 1 for player 2.</param>
    public BoundPlayerInputSource(ControlSettings settings, int playerIndex)
    {
        _settings = settings;
        _playerIndex = playerIndex;
    }

    public PlayerInputState Poll() => Poll(InputSnapshot.Read());

    /// <summary>Polls from a given snapshot — the seam the tests use.</summary>
    public PlayerInputState Poll(InputSnapshot snapshot) =>
        _settings.ReadPlayer(_playerIndex, snapshot.Keys, snapshot.PadOne, snapshot.PadTwo);
}
