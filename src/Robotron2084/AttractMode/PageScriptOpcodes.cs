namespace Robotron2084.AttractMode;

/// <summary>
///     The actions of the attract movie's page script: the byte that starts each one. 0 to 9 are the arcade's own
///     (notes §95.2); 10 is the port's (notes §147).
/// </summary>
public static class PageScriptOpcodes
{
    /// <summary>CURSAB: move the text cursor to a column and row (two operands).</summary>
    public const byte Cursor = 0;

    /// <summary>CLEARM: move the cursor to the left margin of a row, and clear the text below the title (two operands).</summary>
    public const byte Clear = 1;

    /// <summary>NEWLIN: move the cursor to the start of the next line.</summary>
    public const byte NewLine = 2;

    /// <summary>SCRPT: start an object script, at the address in the two operand bytes.</summary>
    public const byte StartScript = 3;

    /// <summary>SNOOZE: pause for the operand's number of 50ths of a second.</summary>
    public const byte Pause = 4;

    /// <summary>MESS: show a message in the score row (two operands: the column, and the message's number).</summary>
    public const byte Message = 5;

    /// <summary>DONE: the movie is over, and the machine's demo game starts.</summary>
    public const byte Done = 6;

    /// <summary>COLOR: the colour of the text after it (one operand: the palette index, as a doubled nibble).</summary>
    public const byte Colour = 7;

    /// <summary>GRUNTS: start the 14 grunts of the grunts page.</summary>
    public const byte Grunts = 8;

    /// <summary>DONE2: the title's short script is over.</summary>
    public const byte Done2 = 9;

    /// <summary>
    ///     Wait until a part of the movie says it is done, checking once every 50th of a second, so the script after it
    ///     never starts early however long that part takes. Its one operand is the part, an
    ///     <see cref="AttractSequence" />. Port only: the arcade's page script waits by time alone.
    /// </summary>
    public const byte WaitUntilDone = 10;
}
