using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;

namespace Robotron2084.Input;

/// <summary>
/// One player's eight bindings (notes §101) and the rules that turn them into a
/// <see cref="PlayerInputState"/>.
///
/// The two-stick model is the arcade's exactly: the four MOVE actions are the left
/// stick's direction vector and the four SHOOT actions the right stick's, so a
/// half-pressed pair (say MOVE UP with SHOOT RIGHT) is a diagonal, and FIRING is
/// "any shoot action is held" — which is what the port's IJKL keyboard has always
/// done (holding an aim key fires).
/// </summary>
public sealed class PlayerControls
{
    private readonly ActionBinding[] _bindings = new ActionBinding[InputActions.All.Length];

    /// <summary>An all-unbound set.</summary>
    public PlayerControls()
    {
    }

    /// <summary>Reads or writes one action's line.</summary>
    public ActionBinding this[InputAction action]
    {
        get => _bindings[(int)action];
        set => _bindings[(int)action] = value;
    }

    /// <summary>
    /// The port's factory controls, which keep every previously-documented scheme
    /// working. Player 1: WASD to move, IJKL to shoot (the port's keyboard since
    /// 2026-09-12). Player 2: the cursor keys to move and the numeric keypad to shoot,
    /// which is the only scheme that shares one keyboard cleanly. The pad defaults are
    /// the arcade's own two sticks: pad 1 for player 1, pad 2 for player 2.
    /// </summary>
    public static PlayerControls Defaults(int playerIndex)
    {
        var controls = new PlayerControls();
        bool second = playerIndex == 1;
        int pad = second ? 1 : 0;

        (int dx, int dy)[] directions =
        [
            (0, -1), // MoveUp
            (0, 1),  // MoveDown
            (-1, 0), // MoveLeft
            (1, 0),  // MoveRight
            (0, -1), // ShootUp
            (0, 1),  // ShootDown
            (-1, 0), // ShootLeft
            (1, 0),  // ShootRight
        ];

        Keys[] moveKeys = second
            ? [Keys.Up, Keys.Down, Keys.Left, Keys.Right]
            : [Keys.W, Keys.S, Keys.A, Keys.D];
        Keys[] shootKeys = second
            ? [Keys.NumPad8, Keys.NumPad5, Keys.NumPad4, Keys.NumPad6]
            : [Keys.I, Keys.K, Keys.J, Keys.L];

        for (int i = 0; i < InputActions.All.Length; i++)
        {
            InputAction action = InputActions.All[i];
            bool shooting = !action.IsMove();
            int index = i % 4;
            (int dx, int dy) = directions[i];

            controls[action] = new ActionBinding(
                InputBinding.Key(shooting ? shootKeys[index] : moveKeys[index]),
                InputBinding.Stick(pad, rightStick: shooting, dx, dy));
        }

        return controls;
    }

    /// <summary>The move stick's direction (-1/0/1 per axis), from the bound MOVE actions.</summary>
    public IntVector2 MoveDirection(KeyboardState keys, GamePadState padOne, GamePadState padTwo) =>
        new(
            Axis(InputAction.MoveRight, InputAction.MoveLeft, keys, padOne, padTwo),
            Axis(InputAction.MoveDown, InputAction.MoveUp, keys, padOne, padTwo));

    /// <summary>The fire stick's direction, from the bound SHOOT actions.</summary>
    public IntVector2 ShootDirection(KeyboardState keys, GamePadState padOne, GamePadState padTwo) =>
        new(
            Axis(InputAction.ShootRight, InputAction.ShootLeft, keys, padOne, padTwo),
            Axis(InputAction.ShootDown, InputAction.ShootUp, keys, padOne, padTwo));

    /// <summary>
    /// True while ANY shoot action is held — the port's rule since round 7, when the
    /// author asked for shooting without a separate fire button.
    /// </summary>
    public bool Firing(KeyboardState keys, GamePadState padOne, GamePadState padTwo) =>
        ShootDirection(keys, padOne, padTwo) != IntVector2.Zero;

    private int Axis(
        InputAction positive,
        InputAction negative,
        KeyboardState keys,
        GamePadState padOne,
        GamePadState padTwo)
    {
        int value = (this[positive].IsHeld(keys, padOne, padTwo) ? 1 : 0)
            - (this[negative].IsHeld(keys, padOne, padTwo) ? 1 : 0);
        return Math.Clamp(value, -1, 1);
    }
}
