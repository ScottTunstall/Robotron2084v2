using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Robotron2084.Input;

/// <summary>One tick's raw hardware, as the definitions page and the game read it.</summary>
public readonly record struct InputSnapshot(KeyboardState KeyboardState, GamePadState PadOne, GamePadState PadTwo)
{
    /// <summary>Gets the state of one gamepad.</summary>
    /// <param name="padIndex">0 for the first gamepad, 1 for the second.</param>
    /// <returns>The state of that gamepad.</returns>
    public GamePadState GetPad(int padIndex)
    {
        if (padIndex == 1) return PadTwo;

        return PadOne;
    }

    /// <summary>Reads the live keyboard and both gamepads.</summary>
    public static InputSnapshot Read()
    {
        return new InputSnapshot(
            Keyboard.GetState(),
            GamePad.GetState(PlayerIndex.One),
            GamePad.GetState(PlayerIndex.Two));
    }
}
