using Robotron2084.Core;
using Robotron2084.Input;
using Robotron2084.Persistence;

namespace Robotron2084.Hud;

/// <summary>
/// ROM GETLET — the initials entry (notes §116). The move stick's up and down cycle the letter shown
/// at the current cell, fire commits it and moves the cursor on, and a fire that is still held types
/// the rest of the name by itself. Cycling to the rub marker and pressing fire deletes the letter
/// before the cursor, and each letter still to come carries its own deadline: when it passes, the
/// letter stays blank and the entry moves on.
/// </summary>
/// <remarks>
/// RRTESTB.ASM's GETLET, called by <c>EGSUB</c> with three letters and alpha-only
/// (<c>LDD #$300</c>) into the echo region <c>$4680</c>. GETLZ waits for fire to be released
/// (<c>NAP 4</c> a check); GETLT2 reads the switches (<c>NAP 2</c>, up before down before fire);
/// LUP/LDOWN cycle the letter with the <c>DELAY1</c> loop — ten of them before the first repeat
/// (~0.5 s), then one every <c>DELAY1</c> plus <c>NAP 1</c>; G1LET commits; GETLT3/GETLT4 go on
/// committing after 32 counts of two frames, then 4 (~160 ms) while fire is held; GETRUB deletes;
/// TIMPRC ends the entry after 640 frames per remaining letter. Alphanumeric-only mode is the only
/// one the port uses, so the ROM's <c>$80</c> "all characters" paths are not modelled.
/// </remarks>
public sealed class InitialsEntryModel
{
    /// <summary>The ROM's rub code (<c>SLASH</c>/<c>LASCAR</c>, <c>$5E</c>): the marker the player cycles to in order to delete a letter. It is not an ASCII character, so it is carried as the ROM's own byte.</summary>
    public const char RubLetter = '\u005E';

    /// <summary>How many letters the entry asks for — the ROM's <c>LDD #$300</c>, and the width of a table entry.</summary>
    public const int LetterCount = HighScoreTable.InitialsLength;

    /// <summary>The blank a cell starts on — the ROM's own space code, stored as each cell becomes current.</summary>
    private const char Blank = ' ';

    private enum Phase
    {
        AwaitingFireRelease,
        AwaitingInput,
        Cycling,
        Typematic,
    }

    /// <summary>GETLZZ's <c>NAP 4</c>: the fire switch is looked at once every four frames until it is up.</summary>
    private const int FireReleaseCheckSixths = 4 * ArcadeClock.UnitsPerRomFrame;

    /// <summary>GETLT1's <c>NAP 2</c>: the main loop reads the switches every two frames.</summary>
    private const int MainLoopSixths = 2 * ArcadeClock.UnitsPerRomFrame;

    /// <summary>GETLT3's <c>NAP 2</c> between two typematic counts.</summary>
    private const int TypematicStepSixths = 2 * ArcadeClock.UnitsPerRomFrame;

    /// <summary>How often a held direction is looked at — LUP/LDOWN poll their own switch inside the delay loop.</summary>
    private const int CyclePollSixths = ArcadeClock.UnitsPerRomFrame;

    /// <summary>LUP/LDOWN's <c>DELAY1</c> loop — 8192 turns of a six-cycle loop, about 49 ms, i.e. two and a half ROM frames.</summary>
    private const int CycleDelaySixths = ArcadeClock.UnitsPerRomFrame * 5 / 2;

    /// <summary>LUP's <c>LDA #10</c>: ten <c>DELAY1</c> turns pass before the second cycle.</summary>
    private const int FastRepeatCount = 10;

    /// <summary>A repeat after those ten costs <c>DELAY1</c> plus LUP's <c>NAP 1</c>.</summary>
    private const int CyclePeriodSixths = CycleDelaySixths + ArcadeClock.UnitsPerRomFrame;

    /// <summary>The first repeat's period: the ten <c>DELAY1</c> turns of the <c>DECA / BNE LUP1</c> loop.</summary>
    private const int FirstCyclePeriodSixths = FastRepeatCount * CycleDelaySixths;

    /// <summary>TIMPRC's deadline for one letter: <c>NAP $FF</c> + <c>NAP $FF</c> + <c>NAP $82</c> = 640 ROM frames (12.8 s).</summary>
    private const int LetterTimeoutSixths = (0xFF + 0xFF + 0x82) * ArcadeClock.UnitsPerRomFrame;

    /// <summary>GETRET's typematic count for the first auto-repeat (<c>ANDA #$80 / ADDA #$20</c>).</summary>
    private const int FirstTypematicCounts = 0x20;

    /// <summary>GETLT5's count for every later one (<c>ADDA #4</c>).</summary>
    private const int LaterTypematicCounts = 4;

    /// <summary>The at-rest move stick's up (GETLT2's <c>RORA / LBCS LUP</c>): cycles the letter forward.</summary>
    private const int UpDirection = -1;

    /// <summary>The at-rest move stick's down (<c>RORA / LBCS LDOWN</c>): cycles the letter back.</summary>
    private const int DownDirection = 1;

    private readonly char[] _letters = new string(Blank, LetterCount).ToCharArray();
    private Phase _phase = Phase.AwaitingFireRelease;
    private int _position;
    private int _lettersLeft = LetterCount;
    private int _sixths;
    private int _periodSixths = FireReleaseCheckSixths;
    private int _repeatSixths;
    private int _timeoutSixths;
    private int _typematicCounts;
    private int _cycleDirection;
    private bool _rubAllowed;

    /// <summary>True once every letter has been committed or timed out (the ROM's G2LET).</summary>
    public bool IsComplete { get; private set; }

    /// <summary>The letters entered so far — three characters, a space for every cell left blank.</summary>
    public string Initials => new(_letters);

    /// <summary>The cell the cursor is on (0-based); <see cref="LetterCount"/> once the entry is over.</summary>
    public int Position => _position;

    /// <summary>The letter shown in the cursor's cell — the ROM's preview, which cycling rewrites in place.</summary>
    public char Preview => _position < LetterCount ? _letters[_position] : Blank;

    /// <summary>True while the preview is the rub marker, which the page draws with the ROM's own art.</summary>
    public bool PreviewIsRub => Preview == RubLetter;

    /// <summary>
    /// Advances the entry one port tick and returns true once it is over.
    /// </summary>
    /// <param name="input">Player 1's input — the bound move stick cycles, the bound fire commits.</param>
    public bool Tick(PlayerInputState input)
    {
        if (IsComplete)
        {
            return true;
        }

        AdvanceTimeout();
        if (!IsComplete)
        {
            Step(input);
        }

        return IsComplete;
    }

    /// <summary>
    /// TIMPRC: every letter still to come gets its own 640-frame deadline, running whether or not the
    /// player is typing. The last one ends the entry, and a name must not end on the rub marker.
    /// </summary>
    private void AdvanceTimeout()
    {
        _timeoutSixths += ArcadeClock.UnitsPerPortTick;
        if (_timeoutSixths < LetterTimeoutSixths)
        {
            return;
        }

        _timeoutSixths -= LetterTimeoutSixths;
        if (--_lettersLeft > 0)
        {
            return;
        }

        if (_position < LetterCount && PreviewIsRub)
        {
            _letters[_position] = Blank;
        }

        IsComplete = true;
    }

    /// <summary>Runs the current phase once its own period has elapsed.</summary>
    private void Step(PlayerInputState input)
    {
        _sixths += ArcadeClock.UnitsPerPortTick;
        if (_sixths < _periodSixths)
        {
            return;
        }

        _sixths -= _periodSixths;
        switch (_phase)
        {
            case Phase.AwaitingFireRelease:
                CheckFireReleased(input);
                break;
            case Phase.AwaitingInput:
                ReadSwitches(input);
                break;
            case Phase.Cycling:
                RepeatCycle(input);
                break;
            default:
                RepeatTypematic(input);
                break;
        }
    }

    /// <summary>GETLZZ: the screen comes up under a held fire, so the entry first waits for the release.</summary>
    private void CheckFireReleased(PlayerInputState input)
    {
        if (input.FirePressed)
        {
            return;
        }

        EnterMainLoop();
    }

    /// <summary>GETLT2: up and down cycle the preview, fire commits it — the ROM reads the switches in that order.</summary>
    private void ReadSwitches(PlayerInputState input)
    {
        if (input.MoveDirection.Y < 0)
        {
            BeginCycling(UpDirection);
        }
        else if (input.MoveDirection.Y > 0)
        {
            BeginCycling(DownDirection);
        }
        else if (input.FirePressed)
        {
            Commit(FirstTypematicCounts);
        }
    }

    /// <summary>LUP/LDOWN: the first cycle happens the moment the switch is seen, and the repeats follow the ROM's delays.</summary>
    private void BeginCycling(int direction)
    {
        _cycleDirection = direction;
        Cycle(direction);
        _repeatSixths = FirstCyclePeriodSixths;
        _phase = Phase.Cycling;
        _periodSixths = CyclePollSixths;
        _sixths = 0;
    }

    /// <summary>
    /// LUP/LDOWN's loop: the switch is polled a frame at a time, the preview repeats on the ROM's
    /// delays, and the loop is left the instant the switch is released — committing then if fire is
    /// held by then (GETRET).
    /// </summary>
    private void RepeatCycle(PlayerInputState input)
    {
        if (!Held(input, _cycleDirection))
        {
            EnterMainLoop();
            if (input.FirePressed)
            {
                Commit(FirstTypematicCounts);
            }

            return;
        }

        _repeatSixths -= CyclePollSixths;
        if (_repeatSixths > 0)
        {
            return;
        }

        Cycle(_cycleDirection);
        _repeatSixths = CyclePeriodSixths;
    }

    /// <summary>
    /// GETLT3/GETLT4: with fire still held after a commit the ROM types the rest of the name itself —
    /// the first repeat after 32 counts, then one every four counts (~160 ms). The last letter is
    /// never typed automatically.
    /// </summary>
    private void RepeatTypematic(PlayerInputState input)
    {
        if (!input.FirePressed)
        {
            EnterMainLoop();
            return;
        }

        if (_lettersLeft <= 1 || --_typematicCounts > 0)
        {
            return;
        }

        Commit(LaterTypematicCounts);
    }

    /// <summary>G1LET: the preview is committed to its cell and the cursor moves on, or the rub marker deletes.</summary>
    /// <param name="typematicCounts">The count the ROM's typematic waits before typing the next letter by itself.</param>
    private void Commit(int typematicCounts)
    {
        if (PreviewIsRub)
        {
            RubOut();
            return;
        }

        _rubAllowed = true;
        _position++;
        if (--_lettersLeft <= 0)
        {
            IsComplete = true;
            return;
        }

        // G0SUB seeds the cell the cursor has just reached so a letter left alone is a valid blank.
        SeedCell();
        _typematicCounts = typematicCounts;
        _phase = Phase.Typematic;
        _periodSixths = TypematicStepSixths;
        _sixths = 0;
    }

    /// <summary>GETRUB: the rub marker clears its own cell, steps back one and asks for that letter again.</summary>
    private void RubOut()
    {
        _letters[_position] = Blank;
        _position--;
        _lettersLeft++;

        // GETRUB ends by re-entering GETLLL → G0SUB, which seeds the cell the cursor has gone back
        // to: that is why the letter the player rejected is gone and can be typed afresh.
        SeedCell();
        _rubAllowed = false;
        _phase = Phase.AwaitingFireRelease;
        _periodSixths = FireReleaseCheckSixths;
        _sixths = 0;
    }

    /// <summary>G0SUB: puts a blank in the cell the cursor is on, so it is always a valid letter position.</summary>
    private void SeedCell() => _letters[_position] = Blank;

    /// <summary>GETLT1: back to reading the switches every two frames.</summary>
    private void EnterMainLoop()
    {
        _phase = Phase.AwaitingInput;
        _periodSixths = MainLoopSixths;
        _sixths = 0;
    }

    private static bool Held(PlayerInputState input, int direction) =>
        direction == UpDirection ? input.MoveDirection.Y < 0 : input.MoveDirection.Y > 0;

    /// <summary>The alpha-only ring (LUPP1/LDN1): space, A to Z, and the rub marker once a letter has been committed.</summary>
    private void Cycle(int direction) =>
        _letters[_position] = direction == UpDirection ? NextLetter(Preview) : PreviousLetter(Preview);

    private char NextLetter(char current) => current switch
    {
        Blank => 'A',
        'Z' when _rubAllowed => RubLetter,
        'Z' => Blank,
        RubLetter => Blank,
        _ => (char)(current + 1),
    };

    private char PreviousLetter(char current) => current switch
    {
        Blank => _rubAllowed ? RubLetter : 'Z',
        'A' => Blank,
        RubLetter => 'Z',
        _ => (char)(current - 1),
    };
}
