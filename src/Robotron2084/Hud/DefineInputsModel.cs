using Robotron2084.Input;

namespace Robotron2084.Hud;

/// <summary>
/// The DEFINE INPUTS page's logic, with no MonoGame in it (notes §101) — the same
/// split the high score page uses (<see cref="HighScorePrintSequence"/>): the state
/// draws and reads hardware, this decides what a keypress MEANS.
///
/// The page is ONE column of lines: player 1's eight stick lines, then player 2's
/// eight, then the machine's single PAUSE line — seventeen in all, of which only
/// <see cref="VisibleLines"/> are on screen at once. The author's ask was exactly that:
/// two side-by-side columns made the screen feel full, so player 2's section sits
/// BENEATH player 1's and the cursor SCROLLS between them.
///
/// Setting an input is deliberately TWO steps (the author's choice): <c>Enter</c> arms
/// the highlighted line, then the next thing pressed becomes the binding. Without it
/// the cursor keys could never be bound, since they are how the page is scrolled.
/// Scrolling is therefore only live while nothing is armed.
/// </summary>
public sealed class DefineInputsModel
{
    /// <summary>Lines per player: the four MOVE and four SHOOT directions of one stick pair.</summary>
    public const int LinesPerPlayer = 8;

    /// <summary>The PAUSE line's index — the last line, after both players.</summary>
    public const int PauseLine = 2 * LinesPerPlayer;

    /// <summary>Every line on the page.</summary>
    public const int LineCount = PauseLine + 1;

    /// <summary>How many lines fit on screen — one player's section, heading and all.</summary>
    public const int VisibleLines = LinesPerPlayer;

    /// <summary>True once Enter has armed the highlighted line for capture.</summary>
    public bool IsArmed { get; private set; }

    /// <summary>The highlighted line, from player 1's first MOVE to the PAUSE line.</summary>
    public int Line { get; private set; }

    /// <summary>The first line on screen, so the highlighted line is always in the window.</summary>
    public int FirstVisibleLine { get; private set; }

    /// <summary>True while the highlight is on the shared PAUSE line.</summary>
    public bool IsPauseLine => Line == PauseLine;

    /// <summary>Which player owns a line: 0 for player 1, 1 for player 2 (the PAUSE line has none).</summary>
    public static int PlayerOf(int line) => line < LinesPerPlayer ? 0 : 1;

    /// <summary>The action a line sets, or null on the PAUSE line.</summary>
    public static InputAction? ActionOf(int line) =>
        line < PauseLine ? InputActions.All[line % LinesPerPlayer] : null;

    /// <summary>The highlighted action, or null on the PAUSE line.</summary>
    public InputAction? HighlightedAction => ActionOf(Line);

    /// <summary>True when the highlight is on <paramref name="line"/> (and not mid-capture).</summary>
    public bool IsCursorOn(int line) => !IsArmed && Line == line;

    /// <summary>Arms the highlighted line: the next input captured is assigned to it.</summary>
    public void Arm() => IsArmed = true;

    /// <summary>Disarms without assigning (Back while armed).</summary>
    public void CancelArm() => IsArmed = false;

    /// <summary>
    /// Scrolls the highlight up one line, wrapping round the whole page (player 1's
    /// MOVE UP is directly below the PAUSE line), and brings it into the window.
    /// </summary>
    public void MoveUp()
    {
        if (!IsArmed)
        {
            MoveTo(Line == 0 ? PauseLine : Line - 1);
        }
    }

    /// <summary>Scrolls the highlight down one line, wrapping, and brings it into the window.</summary>
    public void MoveDown()
    {
        if (!IsArmed)
        {
            MoveTo(Line == PauseLine ? 0 : Line + 1);
        }
    }

    /// <summary>
    /// Gives the highlighted line its new binding and, as the author's two-step flow
    /// expects, moves on to the next line and disarms. The PAUSE line has nowhere to go,
    /// so the highlight stays there. Returns false when the page was not armed.
    /// </summary>
    public bool Assign(ControlSettings settings, InputBinding binding)
    {
        if (!IsArmed || binding.Kind == InputBindingKind.None)
        {
            return false;
        }

        SetLine(settings, Line, binding);
        if (Line < PauseLine)
        {
            MoveTo(Line + 1);
        }

        IsArmed = false;
        return true;
    }

    /// <summary>Clears the highlighted line (Del) — both device slots go unbound.</summary>
    public void ClearHighlighted(ControlSettings settings)
    {
        IsArmed = false;
        SetLine(settings, Line, InputBinding.None);
    }

    /// <summary>The page's <c>R</c>: every line of both players, and PAUSE, back to the factory scheme.</summary>
    public void ResetAll(ControlSettings settings) => settings.ResetToDefaults();

    /// <summary>Writes one line — the device slot the new binding belongs to, or both for a clear.</summary>
    private static void SetLine(ControlSettings settings, int line, InputBinding binding)
    {
        bool clearing = binding.Kind == InputBindingKind.None;
        if (line == PauseLine)
        {
            settings.Pause = clearing ? InputBinding.None : binding;
            return;
        }

        PlayerControls controls = settings[PlayerOf(line)];
        InputAction action = ActionOf(line)!.Value;
        controls[action] = clearing ? ActionBinding.None : controls[action].With(binding);
    }

    /// <summary>Moves the highlight and scrolls the window just enough to keep it visible.</summary>
    private void MoveTo(int line)
    {
        Line = line;
        if (Line < FirstVisibleLine)
        {
            FirstVisibleLine = Line;
        }
        else if (Line >= FirstVisibleLine + VisibleLines)
        {
            FirstVisibleLine = Line - VisibleLines + 1;
        }
    }
}
