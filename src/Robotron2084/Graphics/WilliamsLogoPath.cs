namespace Robotron2084.Graphics;

/// <summary>
/// Where the next Williams "W" of the attract page's border goes and which palette slot it is drawn in:
/// the walk round the screen edge the ROM keeps in <c>$E6</c>/<c>$E8</c>/<c>$EA</c>/<c>$E9</c> and advances at
/// <c>$896C</c> (R5).
/// </summary>
/// <remarks>
/// A lap takes the four sides in turn (right, down, left, up) in 32-pixel steps, and every time it comes back to
/// the top it starts the next lap a little further in (<c>$EA</c>), for eight laps, then starts over.
/// Positions are the ROM's: <see cref="Column"/> is a screen BYTE, which is two arcade pixels, and
/// <see cref="Row"/> is an arcade pixel row.
/// </remarks>
public sealed class WilliamsLogoPath
{
    /// <summary>ROM <c>$894B</c>: the first W's column, in bytes (10 arcade pixels).</summary>
    private const int StartColumn = 0x05;

    /// <summary>ROM <c>$894B</c>: the first W's row, and the row of the top edge.</summary>
    private const int StartRow = 0x0F;

    /// <summary>ROM <c>$8957</c>: the first W's colour operand, both nibbles the palette slot.</summary>
    private const int StartColorOperand = 0x77;

    /// <summary>ROM <c>$8970</c>: the side counter is taken modulo this.</summary>
    private const int SideCount = 4;

    /// <summary>ROM <c>$89C5</c>: a step along the top or bottom edge is 16 bytes, 32 arcade pixels.</summary>
    private const int HorizontalStepColumns = 0x10;

    /// <summary>ROM <c>$897D</c>: a step along the left or right edge is 32 rows.</summary>
    private const int VerticalStepRows = 0x20;

    /// <summary>ROM <c>$89C7</c>: the right edge's column; a step that reaches it turns the corner.</summary>
    private const int RightColumn = 0x85;

    /// <summary>ROM <c>$89A5</c>: the left edge's column.</summary>
    private const int LeftColumn = 0x05;

    /// <summary>ROM <c>$89B7</c>: the bottom edge's row.</summary>
    private const int BottomRow = 0xCF;

    /// <summary>ROM <c>$8987</c>: each lap starts this many bytes further in...</summary>
    private const int LapInsetColumns = 0x02;

    /// <summary>ROM <c>$8989</c>: ...for laps until the inset would reach this, when the walk starts over.</summary>
    private const int LapInsetLimit = 0x10;

    /// <summary>ROM <c>$898F</c>: the column the walk starts over from (the ROM's own value, which is not the left edge).</summary>
    private const int RestartColumn = 0x15;

    /// <summary>ROM <c>$89DA</c>: each W's colour operand steps down by one in both nibbles.</summary>
    private const int ColorOperandStep = 0x11;

    private int _side;
    private int _inset;

    /// <summary>Starts at the first W's place in the first colour.</summary>
    public WilliamsLogoPath() => Reset();

    /// <summary>The W's column in screen bytes (two arcade pixels each).</summary>
    public int Column { get; private set; }

    /// <summary>The W's top row in arcade pixels.</summary>
    public int Row { get; private set; }

    /// <summary>The palette slot the W is drawn in: the colour operand's nibble, 7 down to 1 and round.</summary>
    public int Slot => ColorOperand & 0x0F;

    private int ColorOperand { get; set; }

    /// <summary>Puts the walk back at the first W (ROM <c>$894B</c>).</summary>
    public void Reset()
    {
        Column = StartColumn;
        Row = StartRow;
        _side = 0;
        _inset = 0;
        ColorOperand = StartColorOperand;
    }

    /// <summary>Moves on to the next W's place and colour (ROM <c>$896C</c>).</summary>
    public void Step()
    {
        switch (_side % SideCount)
        {
            case 0:
                StepRight();
                break;
            case 1:
                StepDown();
                break;
            case 2:
                StepLeft();
                break;
            default:
                StepUp();
                break;
        }

        StepColor();
    }

    private void StepRight()
    {
        int column = Column + HorizontalStepColumns;
        if (column < RightColumn)
        {
            Column = column;
            return;
        }

        Column = RightColumn;
        Row = ((_inset * 2) + StartRow) & 0xFF;
        _side++;
    }

    private void StepDown()
    {
        int row = Row + VerticalStepRows;
        if (row < BottomRow)
        {
            Row = row;
            return;
        }

        Row = BottomRow;
        Column = (RightColumn - _inset) & 0xFF;
        _side++;
    }

    private void StepLeft()
    {
        int column = Column - HorizontalStepColumns;
        if (column >= 0 && column > LeftColumn)
        {
            Column = column;
            return;
        }

        Column = LeftColumn;
        Row = (BottomRow - (_inset * 2)) & 0xFF;
        _side++;
    }

    private void StepUp()
    {
        int row = Row - VerticalStepRows;
        if (row >= 0 && row > StartRow)
        {
            Row = row;
            return;
        }

        int inset = _inset + LapInsetColumns;
        if (inset < LapInsetLimit)
        {
            _inset = inset;
            Column = LeftColumn + _inset;
        }
        else
        {
            _inset = 0;
            Column = RestartColumn;
        }

        Row = StartRow;
        _side++;
    }

    private void StepColor()
    {
        int next = ColorOperand - ColorOperandStep;
        ColorOperand = next <= 0 ? StartColorOperand : next;
    }
}
