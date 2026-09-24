using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Robotron2084.Input;

/// <summary>One tick's raw hardware, as the definitions page and the game read it.</summary>
public readonly record struct InputSnapshot(KeyboardState Keys, GamePadState PadOne, GamePadState PadTwo)
{
    /// <summary>Reads the live keyboard and both gamepads.</summary>
    public static InputSnapshot Read() => new(
        Keyboard.GetState(),
        GamePad.GetState(PlayerIndex.One),
        GamePad.GetState(PlayerIndex.Two));
}
