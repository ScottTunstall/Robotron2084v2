using Microsoft.Xna.Framework;
using Robotron2084.Core;

namespace Robotron2084.Entities;

/// <summary>The step a grunt takes towards the player, which the BerzerkRobot takes too.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRP8.ASM</c> <c>ROB1B</c> to <c>ROB4A</c></item>
/// <item>Disassembly: <c>MOVE_GRUNT</c> (<c>$39E6</c>) from <c>$39EF</c> to <c>$3A29</c></item>
/// </list>
///
/// The two directions are decided separately. Sideways, a
/// robot always steps: towards the player, or to the right when the columns match. Up and down, one level
/// with the player steps down, and only a gap of exactly one row is ignored. A step that would leave the
/// playfield is not taken.
/// </remarks>
internal static class GruntChaseStep
{
    /// <summary>How far a step goes sideways towards the player, in columns.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRP8.ASM</c> <c>ROB3</c> to <c>ROB4A</c>, a step of 2 columns.</item>
    /// <item>Disassembly: <c>MOVE_GRUNT</c> (<c>$39E6</c>) at <c>$3A15</c> and <c>$3A1D</c>.</item>
    /// </list>
    /// </remarks>
    private const int StepColumns = 2;

    /// <summary>How far a step goes up or down towards the player, in rows.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRP8.ASM</c> <c>ROB1B</c> to <c>ROB2A</c>, a step of 4 rows.</item>
    /// <item>Disassembly: <c>MOVE_GRUNT</c> (<c>$39E6</c>) at <c>$39F9</c> and <c>$3A01</c>.</item>
    /// </list>
    /// </remarks>
    private const int StepRows = 4;

    /// <summary>The up-and-down gap, in rows, at which a robot level with the player stays put.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRP8.ASM</c> <c>ROB2</c> and the <c>CMPB #$FE</c> before <c>ROB2A</c>: a
    /// robot only 1 row above or below the player does not move up or down.</item>
    /// <item>Disassembly: <c>MOVE_GRUNT</c> (<c>$39E6</c>) at <c>$39F5</c> and <c>$39FD</c>.</item>
    /// </list>
    /// </remarks>
    private const int StayPutGapRows = 1;

    /// <summary>One column, in port pixels.</summary>
    private static readonly int ColumnPixels = ScreenSize.ToPortPixelsFromColumns(1);

    /// <summary>One row, in port pixels.</summary>
    private static readonly int RowPixels = ScreenSize.ToPortPixels(1);

    /// <summary>How far one step moves a robot sideways, in port pixels.</summary>
    private static readonly int StepXPixels = ScreenSize.ToPortPixelsFromColumns(StepColumns);

    /// <summary>How far one step moves a robot up or down, in port pixels.</summary>
    private static readonly int StepYPixels = ScreenSize.ToPortPixels(StepRows);

    /// <summary>Works out where a robot is after one step towards the player.</summary>
    /// <param name="position">The robot's top-left corner now, in port pixels.</param>
    /// <param name="player">The player's top-left corner.</param>
    /// <param name="playfield">The inside of the wall.</param>
    /// <param name="size">The robot's size, in port pixels.</param>
    /// <returns>Where the robot's top-left corner is after the step.</returns>
    public static IntVector2 GetNextPosition(IntVector2 position, IntVector2 player, Rectangle playfield, (int Width, int Height) size)
    {
        int columnsRightOfPlayer = DivideRoundingDown(position.X - player.X, ColumnPixels);
        int rowsBelowPlayer = DivideRoundingDown(position.Y - player.Y, RowPixels);

        int dx = columnsRightOfPlayer > 0 ? -StepXPixels : StepXPixels;
        int dy = 0;
        if (rowsBelowPlayer > 0)
        {
            dy = rowsBelowPlayer > StayPutGapRows ? -StepYPixels : 0;
        }
        else if (rowsBelowPlayer != -StayPutGapRows)
        {
            dy = StepYPixels;
        }

        int y = position.Y + dy;
        if (dy != 0 && y >= playfield.Y && y + size.Height <= playfield.Bottom)
        {
            position = position with { Y = y };
        }

        int x = position.X + dx;
        if (x >= playfield.X && x + size.Width <= playfield.Right)
        {
            position = position with { X = x };
        }

        return position;
    }

    /// <summary>Divides, rounding towards the lower number, so a gap in pixels becomes a whole number of columns or rows.</summary>
    /// <param name="pixels">The gap in port pixels, which may be negative.</param>
    /// <param name="unitPixels">How many port pixels make one column or one row.</param>
    private static int DivideRoundingDown(int pixels, int unitPixels) =>
        (int)Math.Floor(pixels / (double)unitPixels);
}
