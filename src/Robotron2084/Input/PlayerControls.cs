using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;

namespace Robotron2084.Input;

/// <summary>
///     One player's eight bindings (notes §101) and the rules that turn them into a
///     <see cref="PlayerInputState" />.
///     The two-stick model is the arcade's exactly: the four MOVE actions are the left
///     stick's direction vector and the four SHOOT actions the right stick's, so a
///     half-pressed pair (say MOVE UP with SHOOT RIGHT) is a diagonal, and FIRING is
///     "any shoot action is held" — which is what the port's IJKL keyboard has always
///     done (holding an aim key fires).
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
    ///     The port's factory controls, which keep every previously-documented scheme
    ///     working. Player 1: WASD to move, IJKL to shoot (the port's keyboard since
    ///     2026-09-12). Player 2: the cursor keys to move and the numeric keypad to shoot,
    ///     which is the only scheme that shares one keyboard cleanly. The pad defaults are
    ///     the arcade's own two sticks: pad 1 for player 1, pad 2 for player 2.
    /// </summary>
    public static PlayerControls CreateDefaults(int playerIndex)
    {
        var playerControls = new PlayerControls();
        var isSecondPlayer = playerIndex == 1;
        var pad = 0;
        if (isSecondPlayer) pad = 1;

        (int dx, int dy)[] directions =
        [
            (0, -1), // MoveUp
            (0, 1), // MoveDown
            (-1, 0), // MoveLeft
            (1, 0), // MoveRight
            (0, -1), // ShootUp
            (0, 1), // ShootDown
            (-1, 0), // ShootLeft
            (1, 0) // ShootRight
        ];

        Keys[] moveKeys = [Keys.W, Keys.S, Keys.A, Keys.D];
        Keys[] shootKeys = [Keys.I, Keys.K, Keys.J, Keys.L];
        if (isSecondPlayer)
        {
            moveKeys = [Keys.Up, Keys.Down, Keys.Left, Keys.Right];
            shootKeys = [Keys.NumPad8, Keys.NumPad5, Keys.NumPad4, Keys.NumPad6];
        }

        for (var i = 0; i < InputActions.All.Length; i++)
        {
            var action = InputActions.All[i];
            var shooting = !action.IsMove();
            var index = i % 4;
            var (dx, dy) = directions[i];

            var key = moveKeys[index];
            if (shooting) key = shootKeys[index];

            playerControls[action] = new ActionBinding(
                InputBinding.CreateKey(key),
                InputBinding.CreateStick(pad, shooting, dx, dy));
        }

        return playerControls;
    }

    /// <summary>The move stick's direction (-1/0/1 per axis), from the bound MOVE actions.</summary>
    public IntVector2 GetMoveDirection(KeyboardState keyboardState, GamePadState playerOneGamePadState, GamePadState playerTwoGamePadState)
    {
        return new IntVector2(
            GetAxis(InputAction.MoveRight, InputAction.MoveLeft, keyboardState, playerOneGamePadState, playerTwoGamePadState),
            GetAxis(InputAction.MoveDown, InputAction.MoveUp, keyboardState, playerOneGamePadState, playerTwoGamePadState));
    }

    /// <summary>The fire stick's direction, from the bound SHOOT actions.</summary>
    public IntVector2 GetShootDirection(KeyboardState keyboardState, GamePadState playerOneGamePadState, GamePadState playerTwoGamePadState)
    {
        return new IntVector2(
            GetAxis(InputAction.ShootRight, InputAction.ShootLeft, keyboardState, playerOneGamePadState, playerTwoGamePadState),
            GetAxis(InputAction.ShootDown, InputAction.ShootUp, keyboardState, playerOneGamePadState, playerTwoGamePadState));
    }

    /// <summary>
    ///     True while ANY shoot action is held (holding an aim key fires).
    /// </summary>
    public bool IsFiring(KeyboardState keyboardState, GamePadState playerOneGamePadState, GamePadState playerTwoGamePadState)
    {
        return GetShootDirection(keyboardState, playerOneGamePadState, playerTwoGamePadState) != IntVector2.Zero;
    }

    private int GetAxis(
        InputAction positive,
        InputAction negative,
        KeyboardState keyboardState,
        GamePadState playerOneGamePadState,
        GamePadState playerTwoGamePadState)
    {
        var value = 0;
        if (this[positive].IsHeld(keyboardState, playerOneGamePadState, playerTwoGamePadState)) value++;
        if (this[negative].IsHeld(keyboardState, playerOneGamePadState, playerTwoGamePadState)) value--;

        return Math.Clamp(value, -1, 1);
    }
}
