using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;

namespace Robotron2084.Input;

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
    public static InputBinding CreateKey(Keys key) => new(InputBindingKind.Key, (int)key, 0);

    /// <summary>A gamepad button on <paramref name="gamePadIndex"/> (0 = pad 1, 1 = pad 2).</summary>
    public static InputBinding CreateButton(int gamePadIndex, Buttons button) =>
        new(InputBindingKind.GamePadButton, (int)button, gamePadIndex);

    /// <summary>
    /// A stick direction on <paramref name="gamePadIndex"/>. <paramref name="dx"/> and
    /// <paramref name="dy"/> are screen-space (-1, 0 or 1) and must not both be zero.
    /// </summary>
    public static InputBinding CreateStick(int gamePadIndex, bool isRightStick, int dx, int dy)
    {
        dx = Math.Clamp(dx, -1, 1);
        dy = Math.Clamp(dy, -1, 1);
        if (dx == 0 && dy == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dx), "a stick binding needs a direction");
        }

        return new InputBinding(
            isRightStick ? InputBindingKind.GamePadRightStick : InputBindingKind.GamePadLeftStick,
            GetDirectionCode(dx, dy),
            gamePadIndex);
    }

    public int GetDirectionX() => Kind is InputBindingKind.GamePadLeftStick or InputBindingKind.GamePadRightStick
        ? (Code / 3) - 1
        : 0;

    public int GetDirectionY() => Kind is InputBindingKind.GamePadLeftStick or InputBindingKind.GamePadRightStick
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
            InputBindingKind.GamePadLeftStick => GamePadSticks.Read(pad, isRightStick: false) == new IntVector2(GetDirectionX(), GetDirectionY()),
            InputBindingKind.GamePadRightStick => GamePadSticks.Read(pad, isRightStick: true) == new IntVector2(GetDirectionX(), GetDirectionY()),
            _ => false,
        };
    }

    /// <summary>
    /// The line's value as the page and the INI file both write it: "W", "INSERT",
    /// "P1 A", "P2 RIGHT STICK UP". Uppercase and SPACE-separated on purpose — the
    /// arcade's small font carries digits, A-Z and the two brackets and nothing else, so
    /// a lowercase name or a "P1-LS-UP" would reach the screen as "P1LSUP" (notes §101).
    /// The stick and direction words are spelled out rather than abbreviated for that reason.
    /// </summary>
    public string GetDisplayName() => Kind switch
    {
        InputBindingKind.Key => ((Keys)Code).ToString().ToUpperInvariant(),
        InputBindingKind.GamePadButton => $"P{GamePadIndex + 1} {((Buttons)Code).ToString().ToUpperInvariant()}",
        InputBindingKind.GamePadLeftStick or InputBindingKind.GamePadRightStick =>
            $"P{GamePadIndex + 1} {(Kind == InputBindingKind.GamePadRightStick ? "RIGHT STICK" : "LEFT STICK")} {GetDirectionName(Code)}",
        _ => "NONE",
    };

    /// <summary>
    /// The inverse of <see cref="GetDisplayName"/> — the controls INI file stores each
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
            return TryParseGamePad(parts, out binding);
        }

        if (!Enum.TryParse(text, ignoreCase: true, out Keys key))
        {
            return false;
        }

        binding = CreateKey(key);
        return true;
    }

    /// <summary>Parses a "P1 ..."/"P2 ..." value: a stick direction or a gamepad button.</summary>
    /// <param name="parts">The value's words; the first is the pad ("P1" or "P2").</param>
    /// <param name="binding">The parsed binding, or <see cref="None"/> when it fails.</param>
    /// <returns>True when the value parsed.</returns>
    private static bool TryParseGamePad(string[] parts, out InputBinding binding)
    {
        binding = None;
        int padIndex = parts[0][1] - '1';
        if (TryStick(parts, out bool isRightStick, out int directionFrom))
        {
            if (!TryDirection(string.Join(' ', parts[directionFrom..]), out int dx, out int dy))
            {
                return false;
            }

            binding = CreateStick(padIndex, isRightStick, dx, dy);
            return true;
        }

        if (!Enum.TryParse(parts[1], ignoreCase: true, out Buttons button))
        {
            return false;
        }

        binding = CreateButton(padIndex, button);
        return true;
    }

    /// <summary>
    /// Recognises the stick part of a value, in either the current spelling
    /// ("P1 LEFT STICK UP") or the older abbreviations ("P1 LS UP", "P1-LS-UP"), and
    /// reports where the direction word starts.
    /// </summary>
    private static bool TryStick(string[] parts, out bool isRightStick, out int directionFrom)
    {
        isRightStick = false;
        directionFrom = 0;
        if (parts.Length < 2)
        {
            return false;
        }

        if (parts[1].Equals("LS", StringComparison.OrdinalIgnoreCase)
            || parts[1].Equals("RS", StringComparison.OrdinalIgnoreCase))
        {
            isRightStick = parts[1].Equals("RS", StringComparison.OrdinalIgnoreCase);
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

        isRightStick = parts[1].Equals("RIGHT", StringComparison.OrdinalIgnoreCase);
        directionFrom = 3;
        return true;
    }

    private static int GetDirectionCode(int dx, int dy) => ((dx + 1) * 3) + (dy + 1);

    private static string GetDirectionName(int code) => Directions.First(d => d.Code == code).Name;

    private static bool TryDirection(string name, out int dx, out int dy)
    {
        string wanted = ExpandDirection(name);
        foreach ((string direction, int x, int y, _) in Directions)
        {
            if (string.Equals(direction, wanted, StringComparison.OrdinalIgnoreCase))
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
    /// Spells an older two-letter direction out in full ("DN" -&gt; "DOWN", "UP LT" -&gt;
    /// "UP LEFT") so a controls file hand-written with the abbreviations
    /// still loads. Anything already full, or unknown, is left alone.
    /// </summary>
    private static string ExpandDirection(string name)
    {
        var expanded = new List<string>();
        foreach (string part in name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string upper = part.ToUpperInvariant();
            expanded.Add(upper switch
            {
                "DN" => "DOWN",
                "LT" => "LEFT",
                "RT" => "RIGHT",
                _ => upper,
            });
        }

        return string.Join(' ', expanded);
    }

    /// <summary>
    /// The eight stick directions, their screen-space deltas and their names. The directions
    /// are spelled out rather than abbreviated, in the same
    /// breath as the sticks themselves: the value column shows "P2 RIGHT STICK DOWN
    /// RIGHT", not "P2 RS DN RT".
    /// </summary>
    private static readonly (string Name, int Dx, int Dy, int Code)[] Directions =
    [
        ("UP", 0, -1, GetDirectionCode(0, -1)),
        ("DOWN", 0, 1, GetDirectionCode(0, 1)),
        ("LEFT", -1, 0, GetDirectionCode(-1, 0)),
        ("RIGHT", 1, 0, GetDirectionCode(1, 0)),
        ("UP LEFT", -1, -1, GetDirectionCode(-1, -1)),
        ("UP RIGHT", 1, -1, GetDirectionCode(1, -1)),
        ("DOWN LEFT", -1, 1, GetDirectionCode(-1, 1)),
        ("DOWN RIGHT", 1, 1, GetDirectionCode(1, 1)),
    ];
}
