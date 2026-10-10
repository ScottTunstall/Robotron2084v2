using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>Forms the player out of strips at the start of a wave. The strips come in from above and below, from the left and the right, and on both slants, and close in on where the player stands.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c> <c>PAPPR</c> ("SUPER PLAYER APPEAR"),
/// called at <c>PLS1</c>, and <c>PDAPPR</c> ("PLAYER DIAGONAL APPEAR"), called at <c>PLS1A</c></item>
/// <item>Disassembly: <c>$29F5</c> and <c>$29D2</c>, called from <c>$2874</c> and <c>$2882</c></item>
/// </list> The arcade makes many appear effects of the player's sprite at once, each closing in on a
/// different row or column of it, so together they fill a cross and then two slants. The player's own
/// sprite is not drawn until the game goes live. The effects are still running then, and they stay
/// with the player as the player walks (<see cref="StripEffect.CreateFollowingAppear"/>). Each effect
/// needs a record from its strip routine, and a routine with none left makes no effect, so only as
/// many column fans are made as the horizontal routine has records (notes §143).</item>
/// </list>
/// </remarks>
public sealed class PlayerAppear
{
    /// <summary>How many rows apart the centre rows of the leaning strips are. The arcade starts at the row below the sprite's last and goes up by this many each time.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>DPAPP1</c>, <c>SUBA #3</c>.</item>
    /// <item>Disassembly: <c>$29D2</c> onwards.</item>
    /// </list>
    /// </remarks>
    private const int LeaningStripRowStep = 3;

    /// <summary>The two ways the leaning strips lean, in the order the arcade makes them.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>DPAPP1</c>, <c>CLRA / JSR DAPST / COMA / JSR DAPST</c>.</item>
    /// <item>Disassembly: <c>$29D2</c> onwards.</item>
    /// </list>
    /// </remarks>
    private static readonly int[] LeaningStripSlopes = [1, -1];

    /// <summary>True once the strips that lean have been started.</summary>
    private bool _hasLeaningPartStarted;

    /// <summary>True once the strips that come straight in have been started.</summary>
    private bool _hasStraightPartStarted;

    /// <summary>Starts each part of the appear on the tick its time comes.</summary>
    /// <param name="waveStart">The timekeeper for the start of the wave, which says when each part starts.</param>
    /// <param name="player">The player.</param>
    /// <param name="entities">Everything on the field. The appear effects join its strip effects.</param>
    /// <param name="clip">The edges the strips are cut off at.</param>
    public void Update(WaveStartSequence waveStart, Player player, FieldEntities entities, StripClip clip)
    {
        if (!_hasStraightPartStarted && waveStart.HasPlayerAppeared())
        {
            _hasStraightPartStarted = true;
            StartStraightStrips(player, entities, clip, waveStart.GetClockUnitsSincePlayerAppeared() - ArcadeClock.UnitsPerPortTick);
        }

        if (!_hasLeaningPartStarted && waveStart.HasPlayerDiagonalAppearStarted())
        {
            _hasLeaningPartStarted = true;
            StartLeaningStrips(player, entities, clip, waveStart.GetClockUnitsSincePlayerDiagonalAppearStarted() - ArcadeClock.UnitsPerPortTick);
        }
    }

    /// <summary>Leaves the appear out, for a test of something that happens in play and not at the start of a wave (test hook).</summary>
    internal void Skip()
    {
        _hasStraightPartStarted = true;
        _hasLeaningPartStarted = true;
    }

    /// <summary>Starts the strips that come straight in: a row fan for every row of the player's sprite, and then a column fan for every one of the arcade's columns of it.</summary>
    /// <param name="player">The player.</param>
    /// <param name="entities">Everything on the field.</param>
    /// <param name="clip">The edges the strips are cut off at.</param>
    /// <param name="startClockUnits">How far into a ROM frame the effects start, in clock units.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>PAPPR1</c> (<c>JSR APST</c>, "VERTS") and <c>PAPPR2</c>
    /// (<c>JSR HAPST</c>), each counting down from the far edge of the sprite.</item>
    /// <item>Disassembly: <c>$29F5</c> onwards. The arcade's column is two pixels wide, so the column fans
    /// close in on every second pixel column.</item>
    /// </list>
    /// </remarks>
    private static void StartStraightStrips(Player player, FieldEntities entities, StripClip clip, int startClockUnits)
    {
        for (int row = GetSpriteRows(player) - 1; row >= 0; row--)
        {
            entities.Add(StripEffect.CreateFollowingAppear(player, StripFanAxis.Rows, slope: 0, clip, row, startClockUnits));
        }

        for (int column = GetSpriteColumns(player) - 1; column >= 0; column--)
        {
            if (entities.HasRoomForStripEffect(StripEngine.Horizontal))
            {
                entities.Add(StripEffect.CreateFollowingAppear(player, StripFanAxis.Columns, slope: 0, clip, column * ScreenSize.ArcadePixelsPerByte, startClockUnits));
            }
        }
    }

    /// <summary>Starts the strips that lean: a pair of row fans, one leaning each way, for every third row of the player's sprite.</summary>
    /// <param name="player">The player.</param>
    /// <param name="entities">Everything on the field.</param>
    /// <param name="clip">The edges the strips are cut off at.</param>
    /// <param name="startClockUnits">How far into a ROM frame the effects start, in clock units.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>PDAPPR</c>.</item>
    /// <item>Disassembly: <c>$29D2</c> onwards. The arcade's count runs down to nothing as well, which asks
    /// for the row above the sprite's top. The diagonal routine will not take a row outside the
    /// sprite, and uses half the sprite's width in columns instead (<c>RRDX2.ASM</c> <c>NWCEAP</c>,
    /// <c>LDB ,X / LSRB</c>).</item>
    /// </list>
    /// </remarks>
    private static void StartLeaningStrips(Player player, FieldEntities entities, StripClip clip, int startClockUnits)
    {
        for (int rowsLeft = GetSpriteRows(player); rowsLeft >= 0; rowsLeft -= LeaningStripRowStep)
        {
            int row = rowsLeft > 0 ? rowsLeft - 1 : GetSpriteColumns(player) / 2;
            foreach (int slope in LeaningStripSlopes)
            {
                if (entities.HasRoomForStripEffect(StripEngine.Diagonal))
                {
                    entities.Add(StripEffect.CreateFollowingAppear(player, StripFanAxis.Rows, slope, clip, row, startClockUnits));
                }
            }
        }
    }

    /// <summary>Gets how many of the arcade's columns wide the player's sprite is. A column is two pixels.</summary>
    /// <param name="player">The player.</param>
    private static int GetSpriteColumns(Player player) => player.GetBounds().Width / ScreenSize.ToPortPixelsFromColumns(1);

    /// <summary>Gets how many rows high the player's sprite is.</summary>
    /// <param name="player">The player.</param>
    private static int GetSpriteRows(Player player) => player.GetBounds().Height / ScreenSize.ToPortPixels(1);
}
