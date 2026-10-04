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
    /// <summary>How many letters the entry asks for — the ROM's <c>LDD #$300</c>, and the width of a table entry. It is the size of <see cref="_letters"/>.</summary>
    public const int LetterCount = HighScoreTable.InitialsLength;

    /// <summary>The ROM's rub code (<c>SLASH</c>/<c>LASCAR</c>, <c>$5E</c>): the marker the player cycles to in order to delete a letter. It is not an ASCII character, so it is carried as the ROM's own byte.</summary>
    public const char RubLetter = '\u005E';

    /// <summary>The blank a cell starts on — the ROM's own space code, stored as each cell becomes current.</summary>
    private const char Blank = ' ';

    /// <summary>LUP/LDOWN's <c>DELAY1</c> loop — 8192 turns of a six-cycle loop, about 49 ms, i.e. two and a half ROM frames.</summary>
    private const int CycleWaitClockUnits = ArcadeClock.UnitsPerRomFrame * 5 / 2;

    /// <summary>A repeat after those ten costs <c>DELAY1</c> plus LUP's <c>NAP 1</c>. It is the value <see cref="_repeatClockUnits"/> is set to once a letter is cycling steadily.</summary>
    private const int CycleIntervalClockUnits = CycleWaitClockUnits + ArcadeClock.UnitsPerRomFrame;

    /// <summary>How often a held direction is looked at — LUP/LDOWN poll their own switch inside the delay loop. It is the value <see cref="_periodClockUnits"/> is set to while a letter is cycling.</summary>
    private const int CyclePollClockUnits = ArcadeClock.UnitsPerRomFrame;

    /// <summary>The at-rest move stick's down (<c>RORA / LBCS LDOWN</c>): cycles the letter back.</summary>
    private const int DownDirection = 1;

    /// <summary>LUP's <c>LDA #10</c>: ten <c>DELAY1</c> turns pass before the second cycle.</summary>
    private const int FastRepeatCount = 10;

    /// <summary>GETLZZ's <c>NAP 4</c>: the fire switch is looked at once every four frames until it is up. It is the starting value of <see cref="_periodClockUnits"/>, and the value it is set to while the fire button is being checked for release.</summary>
    private const int FireReleaseCheckClockUnits = 4 * ArcadeClock.UnitsPerRomFrame;

    /// <summary>The first repeat's period: the ten <c>DELAY1</c> turns of the <c>DECA / BNE LUP1</c> loop. It is the value <see cref="_repeatClockUnits"/> is set to when a letter first starts to cycle.</summary>
    private const int FirstCycleIntervalClockUnits = FastRepeatCount * CycleWaitClockUnits;

    /// <summary>GETRET's typematic count for the first auto-repeat (<c>ANDA #$80 / ADDA #$20</c>).</summary>
    private const int FirstTypematicCounts = 0x20;

    /// <summary>GETLT5's count for every later one (<c>ADDA #4</c>).</summary>
    private const int LaterTypematicCounts = 4;

    /// <summary>TIMPRC's deadline for one letter: <c>NAP $FF</c> + <c>NAP $FF</c> + <c>NAP $82</c> = 640 ROM frames (12.8 s). <see cref="_timeoutClockUnits"/> is compared with this to tell whether the player has run out of time to choose a letter.</summary>
    private const int LetterTimeoutClockUnits = (0xFF + 0xFF + 0x82) * ArcadeClock.UnitsPerRomFrame;

    /// <summary>GETLT1's <c>NAP 2</c>: the main loop reads the switches every two frames. It is the value <see cref="_periodClockUnits"/> is set to in the main waiting loop.</summary>
    private const int MainLoopClockUnits = 2 * ArcadeClock.UnitsPerRomFrame;

    /// <summary>GETLT3's <c>NAP 2</c> between two typematic counts. It is the value <see cref="_periodClockUnits"/> is set to while a direction is held and the letter repeats.</summary>
    private const int TypematicStepClockUnits = 2 * ArcadeClock.UnitsPerRomFrame;

    /// <summary>The at-rest move stick's up (GETLT2's <c>RORA / LBCS LUP</c>): cycles the letter forward.</summary>
    private const int UpDirection = -1;

    private readonly char[] _letters = new string(Blank, LetterCount).ToCharArray();

    private int _clockUnits;

    private int _cycleDirection;

    private int _lettersLeft = LetterCount;

    private int _periodClockUnits = FireReleaseCheckClockUnits;

    private Phase _phase = Phase.AwaitingFireRelease;

    private int _letterIndex;

    private int _repeatClockUnits;

    private bool _isRubAllowed;

    private int _timeoutClockUnits;

    private int _typematicCounts;

    private enum Phase
    {
        AwaitingFireRelease,
        AwaitingInput,
        Cycling,
        Typematic,
    }

    /// <summary>The letters entered so far — three characters, a space for every cell left blank.</summary>
    public string GetInitials() => new(_letters);

    /// <summary>True once every letter has been committed or timed out (the ROM's G2LET).</summary>
    public bool IsComplete { get; private set; }

    /// <summary>The cell the cursor is on (0-based); <see cref="LetterCount"/> once the entry is over.</summary>
    public int LetterIndex => _letterIndex;

    /// <summary>The letter shown in the cursor's cell — the ROM's preview, which cycling rewrites in place.</summary>
    public char Preview => _letterIndex < LetterCount ? _letters[_letterIndex] : Blank;

    /// <summary>True while the preview is the rub marker, which the page draws with the ROM's own sprite.</summary>
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

    private static bool IsHeld(PlayerInputState input, int direction) =>
            direction == UpDirection ? input.MoveDirection.Y < 0 : input.MoveDirection.Y > 0;

    /// <summary>
    /// TIMPRC: every letter still to come gets its own 640-frame deadline, running whether or not the
    /// player is typing. The last one ends the entry, and a name must not end on the rub marker.
    /// </summary>
    private void AdvanceTimeout()
    {
        _timeoutClockUnits += ArcadeClock.UnitsPerPortTick;
        if (_timeoutClockUnits < LetterTimeoutClockUnits)
        {
            return;
        }

        _timeoutClockUnits -= LetterTimeoutClockUnits;
        if (--_lettersLeft > 0)
        {
            return;
        }

        if (_letterIndex < LetterCount && PreviewIsRub)
        {
            _letters[_letterIndex] = Blank;
        }

        IsComplete = true;
    }

    /// <summary>LUP/LDOWN: the first cycle happens the moment the switch is seen, and the repeats follow the ROM's delays.</summary>
    private void BeginCycling(int direction)
    {
        _cycleDirection = direction;
        Cycle(direction);
        _repeatClockUnits = FirstCycleIntervalClockUnits;
        _phase = Phase.Cycling;
        _periodClockUnits = CyclePollClockUnits;
        _clockUnits = 0;
    }

    /// <summary>GETLZZ: the screen comes up under a held fire, so the entry first waits for the release.</summary>
    private void CheckFireReleased(PlayerInputState input)
    {
        if (input.FireHeld)
        {
            return;
        }

        EnterMainLoop();
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

        _isRubAllowed = true;
        _letterIndex++;
        if (--_lettersLeft <= 0)
        {
            IsComplete = true;
            return;
        }

        // G0SUB seeds the cell the cursor has just reached so a letter left alone is a valid blank.
        SeedCell();
        _typematicCounts = typematicCounts;
        _phase = Phase.Typematic;
        _periodClockUnits = TypematicStepClockUnits;
        _clockUnits = 0;
    }

    /// <summary>The alpha-only ring (LUPP1/LDN1): space, A to Z, and the rub marker once a letter has been committed.</summary>
    private void Cycle(int direction) =>
        _letters[_letterIndex] = direction == UpDirection ? NextLetter(Preview) : GetPreviousLetter(Preview);

    /// <summary>GETLT1: back to reading the switches every two frames.</summary>
    private void EnterMainLoop()
    {
        _phase = Phase.AwaitingInput;
        _periodClockUnits = MainLoopClockUnits;
        _clockUnits = 0;
    }

    private char NextLetter(char current) => current switch
    {
        Blank => 'A',
        'Z' when _isRubAllowed => RubLetter,
        'Z' => Blank,
        RubLetter => Blank,
        _ => (char)(current + 1),
    };

    private char GetPreviousLetter(char current) => current switch
    {
        Blank => _isRubAllowed ? RubLetter : 'Z',
        'A' => Blank,
        RubLetter => 'Z',
        _ => (char)(current - 1),
    };

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
        else if (input.FireHeld)
        {
            Commit(FirstTypematicCounts);
        }
    }

    /// <summary>
    /// LUP/LDOWN's loop: the switch is polled a frame at a time, the preview repeats on the ROM's
    /// delays, and the loop is left the instant the switch is released — committing then if fire is
    /// held by then (GETRET).
    /// </summary>
    private void RepeatCycle(PlayerInputState input)
    {
        if (!IsHeld(input, _cycleDirection))
        {
            EnterMainLoop();
            if (input.FireHeld)
            {
                Commit(FirstTypematicCounts);
            }

            return;
        }

        _repeatClockUnits -= CyclePollClockUnits;
        if (_repeatClockUnits > 0)
        {
            return;
        }

        Cycle(_cycleDirection);
        _repeatClockUnits = CycleIntervalClockUnits;
    }

    /// <summary>
    /// GETLT3/GETLT4: with fire still held after a commit the ROM types the rest of the name itself —
    /// the first repeat after 32 counts, then one every four counts (~160 ms). The last letter is
    /// never typed automatically.
    /// </summary>
    private void RepeatTypematic(PlayerInputState input)
    {
        if (!input.FireHeld)
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

    /// <summary>GETRUB: the rub marker clears its own cell, steps back one and asks for that letter again.</summary>
    private void RubOut()
    {
        _letters[_letterIndex] = Blank;
        _letterIndex--;
        _lettersLeft++;

        // GETRUB ends by re-entering GETLLL → G0SUB, which seeds the cell the cursor has gone back
        // to: that is why the letter the player rejected is gone and can be typed afresh.
        SeedCell();
        _isRubAllowed = false;
        _phase = Phase.AwaitingFireRelease;
        _periodClockUnits = FireReleaseCheckClockUnits;
        _clockUnits = 0;
    }

    /// <summary>G0SUB: puts a blank in the cell the cursor is on, so it is always a valid letter position.</summary>
    private void SeedCell() => _letters[_letterIndex] = Blank;

    /// <summary>Runs the current phase once its own period has elapsed.</summary>
    private void Step(PlayerInputState input)
    {
        _clockUnits += ArcadeClock.UnitsPerPortTick;
        if (_clockUnits < _periodClockUnits)
        {
            return;
        }

        _clockUnits -= _periodClockUnits;
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
}
