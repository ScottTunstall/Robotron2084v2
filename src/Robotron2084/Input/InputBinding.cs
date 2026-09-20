using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;

namespace Robotron2084.Input;

/// <summary>What a binding is attached to.</summary>
public enum InputBindingKind
{
    /// <summary>Nothing — the action is unbound.</summary>
    None,

    /// <summary>A keyboard key (<see cref="InputBinding.Code"/> is a <see cref="Keys"/>).</summary>
    Key,

    /// <summary>A gamepad button (<see cref="InputBinding.Code"/> is a <see cref="Buttons"/>).</summary>
    GamePadButton,

    /// <summary>A left-stick direction (<see cref="InputBinding.Code"/> is a direction code).</summary>
    GamePadLeftStick,

    /// <summary>A right-stick direction.</summary>
    GamePadRightStick,
}

/// <summary>
/// One thing the player can press: a keyboard key, a gamepad button, or one of a
/// stick's eight directions. Port-only (notes §101) — the arcade cabinet's wiring
/// is fixed, so there is nothing ROM-side to be faithful to here.
///
/// A stick direction is stored SCREEN-SPACE (Y down-positive, like every other
/// coordinate in the port) even though XNA's <c>ThumbSticks</c> is up-positive; the
/// sign flip happens once, in <see cref="GamePadSticks"/>.
/// </summary>
public readonly record struct InputBinding(InputBindingKind Kind, int Code, int GamePadIndex)
{
    /// <summary>The unbound binding. <c>Del</c> stores this.</summary>
    public static readonly InputBinding None = new(InputBindingKind.None, 0, 0);

    /// <summary>A keyboard key.</summary>
    public static InputBinding Key(Keys key) => new(InputBindingKind.Key, (int)key, 0);

    /// <summary>A gamepad button on <paramref name="gamePadIndex"/> (0 = pad 1, 1 = pad 2).</summary>
    public static InputBinding Button(int gamePadIndex, Buttons button) =>
        new(InputBindingKind.GamePadButton, (int)button, gamePadIndex);

    /// <summary>
    /// A stick direction on <paramref name="gamePadIndex"/>. <paramref name="dx"/> and
    /// <paramref name="dy"/> are screen-space (-1, 0 or 1) and must not both be zero.
    /// </summary>
    public static InputBinding Stick(int gamePadIndex, bool rightStick, int dx, int dy)
    {
        dx = Math.Clamp(dx, -1, 1);
        dy = Math.Clamp(dy, -1, 1);
        if (dx == 0 && dy == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dx), "a stick binding needs a direction");
        }

        return new InputBinding(
            rightStick ? InputBindingKind.GamePadRightStick : InputBindingKind.GamePadLeftStick,
            DirectionCode(dx, dy),
            gamePadIndex);
    }

    /// <summary>A button or a stick — i.e. anything a gamepad can produce.</summary>
    public bool IsGamePad => Kind is InputBindingKind.GamePadButton
        or InputBindingKind.GamePadLeftStick
        or InputBindingKind.GamePadRightStick;

    public int DirectionX => Kind is InputBindingKind.GamePadLeftStick or InputBindingKind.GamePadRightStick
        ? (Code / 3) - 1
        : 0;

    public int DirectionY => Kind is InputBindingKind.GamePadLeftStick or InputBindingKind.GamePadRightStick
        ? (Code % 3) - 1
        : 0;

    /// <summary>True while this binding is held down, per the ROM's active-HIGH switch convention.</summary>
    public bool IsHeld(KeyboardState keys, GamePadState padOne, GamePadState padTwo)
    {
        GamePadState pad = GamePadIndex == 1 ? padTwo : padOne;

        return Kind switch
        {
            InputBindingKind.Key => keys.IsKeyDown((Keys)Code),
            InputBindingKind.GamePadButton => pad.IsButtonDown((Buttons)Code),
            InputBindingKind.GamePadLeftStick => GamePadSticks.Read(pad, rightStick: false) == new IntVector2(DirectionX, DirectionY),
            InputBindingKind.GamePadRightStick => GamePadSticks.Read(pad, rightStick: true) == new IntVector2(DirectionX, DirectionY),
            _ => false,
        };
    }

    /// <summary>
    /// The line's value as the page and the INI file both write it: "W", "INSERT",
    /// "P1 A", "P2 RIGHT STICK UP". Uppercase and SPACE-separated on purpose — the
    /// arcade's small font carries digits, A-Z and the two brackets and nothing else, so
    /// a lowercase name or a "P1-LS-UP" would reach the screen as "P1LSUP" (notes §101).
    /// The author asked for the sticks to be spelled out rather than abbreviated.
    /// </summary>
    public string DisplayName => Kind switch
    {
        InputBindingKind.Key => ((Keys)Code).ToString().ToUpperInvariant(),
        InputBindingKind.GamePadButton => $"P{GamePadIndex + 1} {((Buttons)Code).ToString().ToUpperInvariant()}",
        InputBindingKind.GamePadLeftStick or InputBindingKind.GamePadRightStick =>
            $"P{GamePadIndex + 1} {(Kind == InputBindingKind.GamePadRightStick ? "RIGHT STICK" : "LEFT STICK")} {DirectionName(Code)}",
        _ => "NONE",
    };

    /// <summary>
    /// The inverse of <see cref="DisplayName"/> — the controls INI file stores each
    /// line in exactly the vocabulary the page shows (notes §101), so what is in the
    /// file is what the page says and either can be read by a human. Accepts "NONE", an
    /// empty value or "-" as the unbound binding, and ":" or "-" in place of the spaces
    /// so a hand-written file in the older dashed style still loads.
    /// </summary>
    public static bool TryParse(string? text, out InputBinding binding)
    {
        binding = None;
        text = text?.Trim();
        if (string.IsNullOrEmpty(text) || text == "-" || string.Equals(text, "NONE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string[] parts = text.Replace(':', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[0].Length == 2 && parts[0][0] is 'P' or 'p' && parts[0][1] is '1' or '2')
        {
            int pad = parts[0][1] - '1';
            if (TryStick(parts, out bool rightStick, out int directionFrom))
            {
                if (!TryDirection(string.Join(' ', parts[directionFrom..]), out int dx, out int dy))
                {
                    return false;
                }

                binding = Stick(pad, rightStick, dx, dy);
                return true;
            }

            if (!Enum.TryParse(parts[1], ignoreCase: true, out Buttons button))
            {
                return false;
            }

            binding = Button(pad, button);
            return true;
        }

        if (!Enum.TryParse(text, ignoreCase: true, out Keys key))
        {
            return false;
        }

        binding = Key(key);
        return true;
    }

    /// <summary>
    /// Recognises the stick part of a value, in either the current spelling
    /// ("P1 LEFT STICK UP") or the older abbreviations ("P1 LS UP", "P1-LS-UP"), and
    /// reports where the direction word starts.
    /// </summary>
    private static bool TryStick(string[] parts, out bool rightStick, out int directionFrom)
    {
        rightStick = false;
        directionFrom = 0;
        if (parts.Length < 2)
        {
            return false;
        }

        if (parts[1].Equals("LS", StringComparison.OrdinalIgnoreCase)
            || parts[1].Equals("RS", StringComparison.OrdinalIgnoreCase))
        {
            rightStick = parts[1].Equals("RS", StringComparison.OrdinalIgnoreCase);
            directionFrom = 2;
            return true;
        }

        if (parts.Length < 3
            || !parts[2].Equals("STICK", StringComparison.OrdinalIgnoreCase)
            || (!parts[1].Equals("LEFT", StringComparison.OrdinalIgnoreCase)
                && !parts[1].Equals("RIGHT", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        rightStick = parts[1].Equals("RIGHT", StringComparison.OrdinalIgnoreCase);
        directionFrom = 3;
        return true;
    }

    private static int DirectionCode(int dx, int dy) => ((dx + 1) * 3) + (dy + 1);

    private static string DirectionName(int code) => Directions.First(d => d.Code == code).Name;

    private static bool TryDirection(string name, out int dx, out int dy)
    {
        foreach ((string direction, int x, int y, _) in Directions)
        {
            if (string.Equals(direction, name, StringComparison.OrdinalIgnoreCase))
            {
                dx = x;
                dy = y;
                return true;
            }
        }

        dx = 0;
        dy = 0;
        return false;
    }

    /// <summary>
    /// The eight stick directions, their screen-space deltas and their short names — all
    /// two letters, because the page's value column shows them beside a key.
    /// </summary>
    private static readonly (string Name, int Dx, int Dy, int Code)[] Directions =
    [
        ("UP", 0, -1, DirectionCode(0, -1)),
        ("DN", 0, 1, DirectionCode(0, 1)),
        ("LT", -1, 0, DirectionCode(-1, 0)),
        ("RT", 1, 0, DirectionCode(1, 0)),
        ("UP LT", -1, -1, DirectionCode(-1, -1)),
        ("UP RT", 1, -1, DirectionCode(1, -1)),
        ("DN LT", -1, 1, DirectionCode(-1, 1)),
        ("DN RT", 1, 1, DirectionCode(1, 1)),
    ];
}

/// <summary>
/// Reads a gamepad stick as one of the eight directions (or zero), dead-zoned and
/// quantized. The single place floating point touches port-only input code:
/// <see cref="GamePadState.ThumbSticks"/> is <see cref="Vector2"/> at the
/// hardware boundary and is quantized before it leaves.
/// </summary>
public static class GamePadSticks
{
    private const float DeadZoneLengthSquared = 0.0625f; // 0.25^2 — still-centered

    /// <summary>The stick's direction in SCREEN space (Y down-positive), or zero when centred.</summary>
    public static IntVector2 Read(GamePadState pad, bool rightStick)
    {
        Vector2 stick = rightStick ? pad.ThumbSticks.Right : pad.ThumbSticks.Left;
        if (stick.LengthSquared() < DeadZoneLengthSquared)
        {
            return IntVector2.Zero;
        }

        return new IntVector2(Math.Sign(stick.X), Math.Sign(-stick.Y));
    }
}
