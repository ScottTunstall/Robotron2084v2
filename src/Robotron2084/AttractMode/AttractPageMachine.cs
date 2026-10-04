using Robotron2084.Core;
using Robotron2084.Level.Attract;

namespace Robotron2084.AttractMode;

/// <summary>
/// The attract movie's PAGE-script interpreter (notes §95.2): the ROM's SPWAKE
/// loop, which walks a byte stream that is one third text, one third actions and
/// one third sleeps:
/// <list type="bullet">
/// <item>a byte below 10 is an action — CURSAB, CLEARM, NEWLIN, SCRPT, SNOOZE,
/// MESS, DONE, COLOR, GRUNTS, DONE2;</item>
/// <item>a byte of $5F or more is a sleep that many ROM frames;</item>
/// <item>anything else is a character to blit in the LARGE font — one every
/// three frames, exactly the ROM's `NAP 3`.</item>
/// </list>
/// It drives the object machine (SCRPT/FORK/GRUNTS) and builds the text layer the
/// renderer draws. Timing is the ROM's frame clock: one <see cref="StepFrame"/>
/// per ROM frame.
/// </summary>
public sealed class AttractPageMachine
{
    /// <summary>The ROM's `TOPLEF+$10` = row 48: where CLEARM starts clearing.</summary>
    public const int ClearTopRow = 48;

    /// <summary>The ROM's clear width: $74 columns = 232 arcade pixels.</summary>
    public const int ClearWidth = 232;

    /// <summary>The ROM's message row is six rows tall.</summary>
    public const int MessageHeight = 6;

    /// <summary>The ROM's `MESHIT` = row $D8: the score row the name popups use.</summary>
    public const int MessageRow = 216;

    /// <summary>The ROM's `LEFT` = $14 columns = 40 arcade pixels — the text margin. It is the starting value of <see cref="_cursorX"/>.</summary>
    public const int TextLeft = 40;

    /// <summary>The ROM's `GSTRTS`: the four grunt personalities.</summary>
    private static readonly int[] GruntScripts = [0x84F5, 0x8517, 0x8540, 0x8567];

    private readonly AttractObjectMachine _objectMachine;
    private readonly Random _random;
    private readonly byte[] _scriptBytes;
    private readonly List<MovieTextCell> _textCells = [];
    private int _cursorX = TextLeft;
    private int _cursorY = 0;
    private int _gruntsLeft;
    private int _gruntTimer;
    private int _scriptIndex;
    private int _waitRomFrames;

    public AttractPageMachine(byte[] scriptBytes, AttractObjectMachine objectMachine, Random random)
    {
        _scriptBytes = scriptBytes;
        _objectMachine = objectMachine;
        _random = random;
    }

    /// <summary>The script reached DONE / DONE2.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>The name popup currently in the score row, if any.</summary>
    public MovieMessage? Message { get; private set; }

    /// <summary>Every character currently on the story screen.</summary>
    public IReadOnlyList<MovieTextCell> TextCells => _textCells;

    /// <summary>The current text colour slot (the ROM's `TEXCOL`).</summary>
    public int TextSlot { get; private set; } = 10;

    /// <summary>Runs one ROM frame of the page script.</summary>
    public void StepFrame()
    {
        SpawnQueuedGrunts();

        if (_waitRomFrames > 0 && --_waitRomFrames > 0)
        {
            return;
        }

        while (!IsFinished)
        {
            if (_scriptIndex >= _scriptBytes.Length)
            {
                IsFinished = true;
                return;
            }

            byte opcode = _scriptBytes[_scriptIndex++];
            if (opcode <= 9)
            {
                RunAction(opcode);
                if (_waitRomFrames > 0 || IsFinished)
                {
                    return;
                }

                continue;
            }

            if (opcode >= 0x5F)
            {
                _waitRomFrames = opcode;
                return;
            }

            PrintCharacter(opcode);
            _waitRomFrames = 3;
            return;
        }
    }

    /// <summary>The ROM's font codes to the port's characters (the font table's order).</summary>
    private static char? DecodeCharacter(byte code) => code switch
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
        _ => null,
    };

    private static string GetMessageText(int number)
    {
        int index = number - AttractMovieData.FirstMessageNumber;
        return index >= 0 && index < AttractMovieData.Messages.Length
            ? AttractMovieData.Messages[index]
            : string.Empty;
    }

    /// <summary>Drops every character inside a cleared block (the ROM's `BLKCLR`).</summary>
    private void ClearText(int x, int y, int width, int height)
    {
        _textCells.RemoveAll(cell =>
            cell.X >= x && cell.X < x + width &&
            cell.Y >= y && cell.Y < y + height);

        if (Message is { } message &&
            message.X >= x && message.X < x + width &&
            message.Y >= y && message.Y < y + height)
        {
            Message = null;
        }
    }

    private byte NextByte() => _scriptBytes[_scriptIndex++];

    private int NextWord() => (NextByte() << 8) | NextByte();

    /// <summary>
    /// ROM `BLIT_LARGE_CHARACTER`: the pen advances (width + 1) pixels, and a code
    /// outside $30..$5E draws nothing at all (the routine returns before its
    /// `LEAX`). A space arrives as $20 and is substituted with the table's blank
    /// glyph at $3A first.
    /// </summary>
    private void PrintCharacter(byte code)
    {
        if (code == 0x20)
        {
            code = 0x3A;
        }

        if (code < 0x30 || code > 0x5E)
        {
            return;
        }

        char? character = DecodeCharacter(code);
        if (character is not null)
        {
            if (_textCells.Count > 0 && _textCells[^1].X == _cursorX && _textCells[^1].Y == _cursorY)
            {
                _textCells.RemoveAt(_textCells.Count - 1);
            }

            _textCells.Add(new MovieTextCell(_cursorX, _cursorY, character.Value, TextSlot));
        }

        _cursorX += AttractMovieData.FontWidths[code - 0x30] + 1;
    }

    /// <summary>Runs a page-script action: the flow opcodes here, the text opcodes in <see cref="RunTextAction"/>.</summary>
    private void RunAction(byte opcode)
    {
        switch (opcode)
        {
            case 3: // SCRPT — start an object script.
                _objectMachine.StartScript(NextWord());
                return;

            case 4: // SNOOZE
                _waitRomFrames = NextByte();
                return;

            case 6: // DONE
            case 9: // DONE2
                IsFinished = true;
                return;

            case 8: // GRUNTS — 14 of them, one every 16 frames (the ROM's GRPROC).
                _gruntsLeft = 14;
                _gruntTimer = 16;
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
            case 0: // CURSAB — set the text cursor (column, row).
                _cursorX = NextByte() * ScreenSize.ArcadePixelsPerColumn;
                _cursorY = NextByte();
                return;

            case 1: // CLEARM — cursor to the left margin on the given row, clear the block.
                {
                    int row = NextByte();
                    int range = NextByte();
                    _cursorX = TextLeft;
                    _cursorY = row;
                    ClearText(TextLeft, ClearTopRow, ClearWidth, range - 0x10);
                    return;
                }

            case 2: // NEWLIN
                _cursorX = TextLeft;
                _cursorY += 11;
                return;

            case 5: // MESS — a name popup in the score row.
                {
                    int x = NextByte() * ScreenSize.ArcadePixelsPerColumn;
                    int number = NextByte();
                    ClearText(TextLeft, MessageRow, ClearWidth, MessageHeight);
                    Message = new MovieMessage(
                        x,
                        MessageRow,
                        GetMessageText(number),
                        TextSlot);
                    return;
                }

            case 7: // COLOR — the text's palette slot (a doubled nibble like $AA).
                TextSlot = NextByte() >> 4;
                return;
        }
    }

    private void SpawnQueuedGrunts()
    {
        if (_gruntsLeft <= 0)
        {
            return;
        }

        if (--_gruntTimer > 0)
        {
            return;
        }

        _gruntTimer = 16;
        _gruntsLeft--;
        _objectMachine.StartScript(GruntScripts[_random.Next(GruntScripts.Length)]);
    }
}
