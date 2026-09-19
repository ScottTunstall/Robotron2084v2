namespace Robotron2084.Level.Attract;

/// <summary>
/// One character the movie has printed: where (arcade pixels / rows) and in which
/// palette slot. The ROM blits characters straight onto the screen; the port
/// keeps them as a layer so the renderer can draw them with the arcade font.
/// </summary>
public readonly record struct MovieTextCell(int X, int Y, char Character, int Slot);

/// <summary>A MESS popup — one of the ROM's message strings in the score row.</summary>
public readonly record struct MovieMessage(int X, int Y, string Text, int Slot);

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
    /// <summary>The ROM's `LEFT` = $14 columns = 40 arcade pixels — the text margin.</summary>
    public const int TextLeft = 40;

    /// <summary>The ROM's `TOPLEF+$10` = row 48: where CLEARM starts clearing.</summary>
    public const int ClearTopRow = 48;

    /// <summary>The ROM's clear width: $74 columns = 232 arcade pixels.</summary>
    public const int ClearWidth = 232;

    /// <summary>The ROM's `MESHIT` = row $D8: the score row the name popups use.</summary>
    public const int MessageRow = 216;

    /// <summary>The ROM's message row is six rows tall.</summary>
    public const int MessageHeight = 6;

    private readonly byte[] _script;
    private readonly AttractObjectMachine _objects;
    private readonly List<MovieTextCell> _text = [];
    private readonly Random _random;
    private int _pc;
    private int _wait;
    private int _cursorX = TextLeft;
    private int _cursorY = 0;
    private int _gruntsLeft;
    private int _gruntTimer;

    public AttractPageMachine(byte[] script, AttractObjectMachine objects, Random random)
    {
        _script = script;
        _objects = objects;
        _random = random;
    }

    /// <summary>Every character currently on the story screen.</summary>
    public IReadOnlyList<MovieTextCell> Text => _text;

    /// <summary>The name popup currently in the score row, if any.</summary>
    public MovieMessage? Message { get; private set; }

    /// <summary>The current text colour slot (the ROM's `TEXCOL`).</summary>
    public int TextSlot { get; private set; } = 10;

    /// <summary>The script reached DONE / DONE2.</summary>
    public bool Finished { get; private set; }

    /// <summary>The GRUNTS action's remaining spawns (the ROM's GRPROC countdown).</summary>
    public int GruntsRemaining => _gruntsLeft;

    /// <summary>Runs one ROM frame of the page script.</summary>
    public void StepFrame()
    {
        SpawnQueuedGrunts();

        if (_wait > 0 && --_wait > 0)
        {
            return;
        }

        while (!Finished)
        {
            if (_pc >= _script.Length)
            {
                Finished = true;
                return;
            }

            byte op = _script[_pc++];
            if (op <= 9)
            {
                RunAction(op);
                if (_wait > 0 || Finished)
                {
                    return;
                }

                continue;
            }

            if (op >= 0x5F)
            {
                _wait = op;
                return;
            }

            PrintCharacter(op);
            _wait = 3;
            return;
        }
    }

    private void RunAction(byte op)
    {
        switch (op)
        {
            case 0: // CURSAB — set the text cursor (column, row).
                _cursorX = NextByte() * 2;
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

            case 3: // SCRPT — start an object script.
                _objects.StartScript(NextWord());
                return;

            case 4: // SNOOZE
                _wait = NextByte();
                return;

            case 5: // MESS — a name popup in the score row.
            {
                int x = NextByte() * 2;
                int number = NextByte();
                ClearText(TextLeft, MessageRow, ClearWidth, MessageHeight);
                Message = new MovieMessage(
                    x,
                    MessageRow,
                    MessageText(number),
                    TextSlot);
                return;
            }

            case 6: // DONE
                Finished = true;
                return;

            case 7: // COLOR — the text's palette slot (a doubled nibble like $AA).
                TextSlot = NextByte() >> 4;
                return;

            case 8: // GRUNTS — 14 of them, one every 16 frames (the ROM's GRPROC).
                _gruntsLeft = 14;
                _gruntTimer = 16;
                return;

            case 9: // DONE2
                Finished = true;
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
        _objects.StartScript(GruntScripts[_random.Next(GruntScripts.Length)]);
    }

    /// <summary>The ROM's `GSTRTS`: the four grunt personalities.</summary>
    private static readonly int[] GruntScripts = [0x84F5, 0x8517, 0x8540, 0x8567];

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
            if (_text.Count > 0 && _text[^1].X == _cursorX && _text[^1].Y == _cursorY)
            {
                _text.RemoveAt(_text.Count - 1);
            }

            _text.Add(new MovieTextCell(_cursorX, _cursorY, character.Value, TextSlot));
        }

        _cursorX += AttractMovieData.FontWidths[code - 0x30] + 1;
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

    private static string MessageText(int number)
    {
        int index = number - AttractMovieData.FirstMessageNumber;
        return index >= 0 && index < AttractMovieData.Messages.Length
            ? AttractMovieData.Messages[index]
            : string.Empty;
    }

    /// <summary>Drops every character inside a cleared block (the ROM's `BLKCLR`).</summary>
    private void ClearText(int x, int y, int width, int height)
    {
        _text.RemoveAll(cell =>
            cell.X >= x && cell.X < x + width &&
            cell.Y >= y && cell.Y < y + height);

        if (Message is { } message &&
            message.X >= x && message.X < x + width &&
            message.Y >= y && message.Y < y + height)
        {
            Message = null;
        }
    }

    private byte NextByte() => _script[_pc++];

    private int NextWord() => (NextByte() << 8) | NextByte();
}
