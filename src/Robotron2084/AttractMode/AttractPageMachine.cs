using Robotron2084.Core;
using Robotron2084.Level.Attract;

namespace Robotron2084.AttractMode;

/// <summary>
///     Reads the attract movie's page script, which says what to print and when to start each little program, and
///     keeps the text that is on the screen.
/// </summary>
/// <remarks>
///     It reads a stream of bytes that is part text, part actions and part sleeps:
///     <list type="bullet">
///         <item>
///             a byte below 10 is an action — CURSAB, CLEARM, NEWLIN, SCRPT, SNOOZE,
///             MESS, DONE, COLOR, GRUNTS, DONE2;
///         </item>
///         <item>
///             10 is the port's own action: wait until a part of the movie is done, however long it takes
///             (<see cref="PageScriptOpcodes.WaitUntilDone" />);
///         </item>
///         <item>a byte of $5F or more is a sleep that many fiftieths of a second;</item>
///         <item>
///             anything else is a character to blit in the LARGE font — one every
///             three frames, exactly the ROM's `NAP 3`.
///         </item>
///     </list>
///     Besides reading the bytes:
///     <list type="bullet">
///         <item>
///             It starts the object machine's programs (SCRPT, FORK, GRUNTS) and builds the layer of text that is
///             drawn.
///         </item>
///         <item>It runs one <see cref="StepFrame" /> for each 50th of a second.</item>
///         <item>Original source: the page-script loop <c>SPWAKE</c> (notes §95.2)</item>
///         <item>Disassembly: not separately labelled</item>
///     </list>
/// </remarks>
public sealed class AttractPageMachine
{
    /// <summary>The row where clearing the text starts: row 48.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>TOPLEF</c> plus $10</item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    public const int ClearTopRow = 48;

    /// <summary>How wide the cleared block of text is, in arcade pixels.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: the clear width of $74 columns</item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    public const int ClearWidth = 232;

    /// <summary>How tall the score row's message is, in rows.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: the message row of six rows</item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    public const int MessageHeight = 6;

    /// <summary>The row of the score row, where the name popups are shown: row 216.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>MESHIT</c>, row $D8</item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    public const int MessageRow = 216;

    /// <summary>How far in from the left edge the text starts, in arcade pixels.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>LEFT</c>, $14 columns</item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    public const int TextLeft = 40;

    /// <summary>How many bytes <see cref="PageScriptOpcodes.WaitUntilDone" /> takes with its operand.</summary>
    private const int WaitUntilDoneLength = 2;

    /// <summary>The start of each of the four programs a grunt can follow.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>GSTRTS</c></item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    private static readonly int[] GruntScripts = [0x84F5, 0x8517, 0x8540, 0x8567];

    private readonly AttractObjectMachine _attractObjectMachine;
    private readonly Random _random;
    private readonly byte[] _scriptBytes;
    private readonly List<MovieTextCell> _textCells = [];
    private int _cursorX = TextLeft;
    private int _cursorY;
    private int _gruntsLeft;
    private int _gruntTimer;
    private int _scriptIndex;
    private int _waitArcadeFrames;

    public AttractPageMachine(byte[] scriptBytes, AttractObjectMachine attractObjectMachine, Random random)
    {
        _scriptBytes = scriptBytes;
        _attractObjectMachine = attractObjectMachine;
        _random = random;
    }

    /// <summary>The script reached DONE / DONE2.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>The name popup currently in the score row, if any.</summary>
    public MovieMessage? Message { get; private set; }

    /// <summary>Every character currently on the story screen.</summary>
    public IReadOnlyList<MovieTextCell> TextCells => _textCells;

    /// <summary>The palette index the text is printed in now.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>TEXCOL</c></item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    public int TextPaletteIndex { get; private set; } = 10;

    /// <summary>Runs one fiftieth of a second of the page script.</summary>
    public void StepFrame()
    {
        SpawnQueuedGrunts();

        if (_waitArcadeFrames > 0 && --_waitArcadeFrames > 0) return;

        while (!IsFinished)
        {
            if (_scriptIndex >= _scriptBytes.Length)
            {
                IsFinished = true;
                return;
            }

            var opcode = _scriptBytes[_scriptIndex++];
            if (opcode <= PageScriptOpcodes.WaitUntilDone)
            {
                RunAction(opcode);
                if (_waitArcadeFrames > 0 || IsFinished) return;

                continue;
            }

            if (opcode >= 0x5F)
            {
                _waitArcadeFrames = opcode;
                return;
            }

            PrintCharacter(opcode);
            _waitArcadeFrames = 3;
            return;
        }
    }

    /// <summary>Turns one of the arcade font's codes into the character it draws.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>The codes are in the order of the arcade's font table.</item>
    ///         <item>Original source: the large font table</item>
    ///         <item>Disassembly: <c>BLIT_LARGE_CHARACTER</c> ($6023)</item>
    ///     </list>
    /// </remarks>
    private static char? DecodeCharacter(byte code)
    {
        return code switch
        {
            0x3A => ' ',
            0x3B => '!',
            0x3C => ',',
            0x3D => '.',
            0x3F => ':',
            0x40 => '-',
            0x5B => '(',
            0x5C => ')',
            >= 0x30 and <= 0x39 => (char)code,
            >= 0x41 and <= 0x5A => (char)code,
            _ => null
        };
    }

    private static string GetMessageText(int number)
    {
        var index = number - AttractMovieData.FirstMessageNumber;
        var isKnownMessage = index >= 0 && index < AttractMovieData.Messages.Length;
        if (!isKnownMessage) return string.Empty;

        return AttractMovieData.Messages[index];
    }

    /// <summary>Takes away every character inside a block that is cleared.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>BLKCLR</c></item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    private void ClearTextCells(int x, int y, int width, int height)
    {
        _textCells.RemoveAll(cell =>
            cell.X >= x && cell.X < x + width &&
            cell.Y >= y && cell.Y < y + height);

        if (Message is { } message &&
            message.X >= x && message.X < x + width &&
            message.Y >= y && message.Y < y + height)
            Message = null;
    }

    private byte NextByte()
    {
        return _scriptBytes[_scriptIndex++];
    }

    private int NextWord()
    {
        return (NextByte() << 8) | NextByte();
    }

    /// <summary>Prints one character at the pen, and moves the pen along.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>The pen moves along by the character's width plus 1 pixel.</item>
    ///         <item>A code outside $30 to $5E draws nothing and does not move the pen.</item>
    ///         <item>A space arrives as $20, and is swapped for the table's blank character, $3A, first.</item>
    ///         <item>Original source: <c>BLIT_LARGE_CHARACTER</c></item>
    ///         <item>Disassembly: <c>BLIT_LARGE_CHARACTER</c> ($6023)</item>
    ///     </list>
    /// </remarks>
    private void PrintCharacter(byte code)
    {
        if (code == 0x20) code = 0x3A;

        if (code < 0x30 || code > 0x5E) return;

        var character = DecodeCharacter(code);
        if (character is not null)
        {
            if (_textCells.Count > 0 && _textCells[^1].X == _cursorX && _textCells[^1].Y == _cursorY)
                _textCells.RemoveAt(_textCells.Count - 1);

            _textCells.Add(new MovieTextCell(_cursorX, _cursorY, character.Value, TextPaletteIndex));
        }

        _cursorX += AttractMovieData.FontWidths[code - 0x30] + 1;
    }

    /// <summary>Runs a page-script action: the flow opcodes here, the text opcodes in <see cref="RunTextAction" />.</summary>
    private void RunAction(byte opcode)
    {
        switch (opcode)
        {
            case PageScriptOpcodes.StartScript:
                _attractObjectMachine.StartScript(NextWord());
                return;

            case PageScriptOpcodes.Pause:
                _waitArcadeFrames = NextByte();
                return;

            case PageScriptOpcodes.Done:
            case PageScriptOpcodes.Done2:
                IsFinished = true;
                return;

            case PageScriptOpcodes.Grunts: // 14 of them, one every 16 frames (the ROM's GRPROC).
                _gruntsLeft = 14;
                _gruntTimer = 16;
                return;

            case PageScriptOpcodes.WaitUntilDone:
                WaitUntilDone();
                return;

            default:
                RunTextAction(opcode);
                return;
        }
    }

    /// <summary>Runs a page-script text action: the cursor, the clear, the name popup and the colour.</summary>
    private void RunTextAction(byte opcode)
    {
        switch (opcode)
        {
            case PageScriptOpcodes.Cursor:
                _cursorX = NextByte() * ScreenSize.ArcadePixelsPerByte;
                _cursorY = NextByte();
                return;

            case PageScriptOpcodes.Clear:
                {
                    int row = NextByte();
                    int range = NextByte();
                    _cursorX = TextLeft;
                    _cursorY = row;
                    ClearTextCells(TextLeft, ClearTopRow, ClearWidth, range - 0x10);
                    return;
                }

            case PageScriptOpcodes.NewLine:
                _cursorX = TextLeft;
                _cursorY += 11;
                return;

            case PageScriptOpcodes.Message: // a name popup
                {
                    var x = NextByte() * ScreenSize.ArcadePixelsPerByte;
                    int number = NextByte();
                    ClearTextCells(TextLeft, MessageRow, ClearWidth, MessageHeight);
                    Message = new MovieMessage(
                        x,
                        MessageRow,
                        GetMessageText(number),
                        TextPaletteIndex);
                    return;
                }

            case PageScriptOpcodes.Colour:
                TextPaletteIndex = NextByte() >> 4;
                return;
        }
    }

    /// <summary>Holds the script back until the part of the movie named by the action's operand is done.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Until then the action is read again in the next 50th of a second.</item>
    ///         <item>Port only: the arcade's page script has no such action (notes §147).</item>
    ///     </list>
    /// </remarks>
    private void WaitUntilDone()
    {
        if (AttractSequenceDoneRules.IsDone((AttractSequence)NextByte(), _attractObjectMachine)) return;

        _scriptIndex -= WaitUntilDoneLength;
        _waitArcadeFrames = 1;
    }

    private void SpawnQueuedGrunts()
    {
        if (_gruntsLeft <= 0) return;

        if (--_gruntTimer > 0) return;

        _gruntTimer = 16;
        _gruntsLeft--;
        _attractObjectMachine.StartScript(GruntScripts[_random.Next(GruntScripts.Length)]);
    }
}
