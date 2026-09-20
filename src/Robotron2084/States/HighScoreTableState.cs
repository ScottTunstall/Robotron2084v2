using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Persistence;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
/// THE ARCADE'S HIGH SCORE TABLE (notes §98) — RRTABLE's <c>TABLE</c>, drawn at
/// its own cursors, in its own fonts and colours:
/// <list type="bullet">
/// <item>"ROBOTRON HEROES" (slot 7) over TODAY'S list at the ROM's (53, 37);</item>
/// <item>TODAY'S ten rows in the LARGE font, 5 per column × 2 columns, from
/// (26, 53), 9 rows / 52 columns apart — each row " N) XXX  1234567";</item>
/// <item>"ALL TIME HEROES" (slot 7) at (53, 110);</item>
/// <item>the operator's top entry at (21, 122) in $AA (or $DD when it is the
/// score you just posted) as "( NAME ) 1234567";</item>
/// <item>the ALL-TIME thirty-six rows in the SMALL font, 12 per column ×
/// 3 columns, from (20, 136), 7 rows / 40 columns apart, ranks 2-37;</item>
/// <item>a 50%-dithered frame in slot 8 between the ROM's corners — see the
/// open items in §98.4: the frame's grow/erase animation and the LOOPP/COLA/
/// COLC/COLD palette cycling are NOT implemented yet.</item>
/// </list>
///
/// A row whose score is one of the scores this session just posted is drawn in
/// the highlight colour — the ROM's <c>CLSET</c> test against <c>ZP1SCR</c>/
/// <c>ZP2SCR</c>, which is how the player finds their own entry.
///
/// It holds for 600 ROM frames (12 s) and then, exactly as the ROM does, waits
/// for any switch/start press — up to another 255 × 4 frames — before handing
/// back to the title page (<c>FAMPAG</c>).
/// </summary>
public sealed class HighScoreTableState : IGameState
{
    private readonly IPlayerInputSource _input;
    private readonly SpriteSet _sprites;
    private readonly HighScoreStore _store;
    private readonly HighScoreTable _table;
    private readonly int[] _postedScores;
    private readonly int _holdTicks = GameplayConstants.PortTicks(GameplayConstants.HighScoreHoldRomFrames);
    private readonly int _leaveTicks = GameplayConstants.PortTicks(GameplayConstants.HighScoreLeaveTimeoutRomFrames);
    private int _elapsedTicks;
    private bool _previousFire;
    private bool _previousStartOne;
    private bool _previousStartTwo;

    /// <param name="postedScores">
    /// The scores this session just offered to the table (highest first) — the
    /// ROM's <c>ZP1SCR</c>/<c>ZP2SCR</c> at <c>CLSET</c> time: their rows are
    /// highlighted. Empty when the table is reached from the attract cycle.
    /// </param>
    public HighScoreTableState(
        IPlayerInputSource input,
        SpriteSet sprites,
        HighScoreStore store,
        IReadOnlyList<int>? postedScores = null)
    {
        _input = input;
        _sprites = sprites;
        _store = store;
        _postedScores = [.. postedScores ?? []];
        _table = store.Load();

        // The ROM's own score processing (EGSUB) has already happened by the time
        // the table is drawn; a save here is the CMOS write.
        store.Save(_table);
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        _elapsedTicks++;

        PlayerInputState input = _input.Poll();
        bool pressed = (input.FirePressed && !_previousFire)
            || (input.StartOnePlayerPressed && !_previousStartOne)
            || (input.StartTwoPlayersPressed && !_previousStartTwo);

        _previousFire = input.FirePressed;
        _previousStartOne = input.StartOnePlayerPressed;
        _previousStartTwo = input.StartTwoPlayersPressed;

        // The ROM: hold 600 frames, then wait for ANY switch (with a 255 × 4 frame
        // timeout), then back to the family page.
        if (pressed || _elapsedTicks >= _holdTicks + _leaveTicks)
        {
            manager.TransitionTo(new TitleScreenState(_input, _sprites, _store));
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        DrawFrame(spriteBatch);

        DrawHeader(spriteBatch, "ROBOTRON HEROES", HighScoreTableLayout.TodayHeaderRow);
        DrawTodayList(spriteBatch);

        DrawHeader(spriteBatch, "ALL TIME HEROES", HighScoreTableLayout.AllTimeHeaderRow);
        DrawTopEntry(spriteBatch);
        DrawAllTimeList(spriteBatch);
    }

    private static int ColumnX(int column) => GameplayConstants.ArcadeX(column * 2);

    private static int RowY(int row) => GameplayConstants.ArcadeY(row);

    private void DrawHeader(SpriteBatch spriteBatch, string text, int row)
    {
        _sprites.DrawLargeFontText(
            spriteBatch,
            text,
            ColumnX(HighScoreTableLayout.HeaderColumn),
            RowY(row),
            GameplayConstants.HighScoreHeaderSlot);
    }

    private void DrawTodayList(SpriteBatch spriteBatch)
    {
        IReadOnlyList<HighScoreEntry> entries = _table.Today;

        for (int rank = 1; rank <= HighScoreTableLayout.TodayRows && rank <= entries.Count; rank++)
        {
            (int column, int row) = HighScoreTableLayout.TodayPosition(rank);
            int x = ColumnX(column);
            int y = RowY(row);
            int slot = SlotFor(entries[rank - 1], GameplayConstants.HighScoreListSlot, GameplayConstants.HighScoreListHighlightSlot);

            int afterRank = DrawRank(spriteBatch, rank, x, y, slot, large: true);
            _sprites.DrawLargeFontText(spriteBatch, entries[rank - 1].Initials, afterRank, y, slot);

            // The ROM's fixed offset from the POST-RANK cursor (RRTABLE's
            // `PSHS X` right after the rank message, then `LEAX D,X`): 11 columns
            // in the large font. The initials therefore run INTO that gap, which is
            // exactly what the arcade's rows look like — 3 large glyphs = 21 px of
            // the 22 px the offset leaves.
            if (entries[rank - 1].Score != 0)
            {
                _sprites.DrawLargeScore(
                    spriteBatch,
                    entries[rank - 1].Score,
                    afterRank + ScreenSize.Scaled(HighScoreTableLayout.TodayScoreOffsetColumns * 2),
                    y,
                    slot);
            }
        }
    }

    private void DrawAllTimeList(SpriteBatch spriteBatch)
    {
        IReadOnlyList<HighScoreEntry> entries = _table.AllTime;

        for (int rank = 2; rank <= HighScoreTableLayout.AllTimeRows + 1 && rank - 2 < entries.Count; rank++)
        {
            (int column, int row) = HighScoreTableLayout.AllTimePosition(rank);
            int x = ColumnX(column);
            int y = RowY(row);
            int slot = SlotFor(entries[rank - 2], GameplayConstants.HighScoreListSlot, GameplayConstants.HighScoreListHighlightSlot);

            int afterRank = DrawRank(spriteBatch, rank, x, y, slot, large: false);
            _sprites.DrawSmallFontText(spriteBatch, entries[rank - 2].Initials, afterRank, y, slot);

            if (entries[rank - 2].Score != 0)
            {
                _sprites.DrawSmallScore(
                    spriteBatch,
                    entries[rank - 2].Score,
                    afterRank + ScreenSize.Scaled(HighScoreTableLayout.AllTimeScoreOffsetColumns * 2),
                    y,
                    slot);
            }
        }
    }

    private void DrawTopEntry(SpriteBatch spriteBatch)
    {
        // TABLE: only the initials part is drawn when the CMOS says the player may
        // not see the name (GA2); the port always has a name, so it always prints
        // "( NAME )" then the score.
        int y = RowY(HighScoreTableLayout.TopRow);
        int slot = SlotFor(_table.Top.Score, GameplayConstants.HighScoreTopSlot, GameplayConstants.HighScoreTopHighlightSlot);

        int x = ColumnX(HighScoreTableLayout.TopColumn);
        x = _sprites.DrawLargeFontText(spriteBatch, "(", x, y, slot);
        x = _sprites.DrawLargeFontText(spriteBatch, _table.Top.Name, x, y, slot);
        x = _sprites.DrawLargeFontText(spriteBatch, ")", x, y, slot);
        _sprites.DrawLargeScore(spriteBatch, _table.Top.Score, x + ScreenSize.Scaled(GameplayConstants.HudSmallFontBlankAdvancePixels), y, slot);
    }

    /// <summary>
    /// The ROM's message 111 (`INDMEP`): a space, the rank in its digit count, ')' and
    /// a space. The score that follows is placed from the ROW's column (the ROM's fixed
    /// offset), so the rank only has to LOOK right.
    /// </summary>
    private int DrawRank(SpriteBatch spriteBatch, int rank, int x, int y, int slot, bool large)
    {
        string text = $" {rank,2}) ";
        return large
            ? _sprites.DrawLargeFontText(spriteBatch, text, x, y, slot)
            : _sprites.DrawSmallFontText(spriteBatch, text, x, y, slot);
    }

    /// <summary>
    /// ROM `CLSET`: a row holding a score the current players posted is drawn in
    /// the second colour — but only when the NEXT table entry is not the same
    /// score, so a repeated score highlights once.
    /// </summary>
    private int SlotFor(HighScoreEntry entry, int normalSlot, int highlightSlot) =>
        SlotFor(entry.Score, normalSlot, highlightSlot);

    private int SlotFor(int score, int normalSlot, int highlightSlot) =>
        score != 0 && _postedScores.Contains(score) ? highlightSlot : normalSlot;

    /// <summary>
    /// The ROM's frame (`FRAMER` → `MARQ`): the rectangle between (col 6, row 13)
    /// and (col 145, row 239), two raster rows thick on the horizontal edges and
    /// dithered 2-px blocks on the vertical ones, in slot 8. The ROM animates it
    /// (grow, then an erase pass) and cycles its slot — notes §98.4.
    /// </summary>
    private void DrawFrame(SpriteBatch spriteBatch)
    {
        Color colour = _sprites.SlotColor(GameplayConstants.HighScoreFrameSlot);
        int thickness = ScreenSize.Scaled(GameplayConstants.HighScoreFrameThicknessPixels);
        int left = ColumnX(GameplayConstants.HighScoreFrameLeftColumn);
        int top = RowY(GameplayConstants.HighScoreFrameTopRow);
        int right = ColumnX(GameplayConstants.HighScoreFrameRightColumn);
        int bottom = RowY(GameplayConstants.HighScoreFrameBottomRow);

        _sprites.DrawSolidRectangle(spriteBatch, new Rectangle(left, top, right - left + thickness, thickness), colour);
        _sprites.DrawSolidRectangle(spriteBatch, new Rectangle(left, bottom, right - left + thickness, thickness), colour);

        // The vertical edges are the "linky" dither: 2 px of colour, 2 px of gap.
        for (int y = top; y <= bottom; y += 2 * thickness)
        {
            _sprites.DrawSolidRectangle(spriteBatch, new Rectangle(left, y, thickness, thickness), colour);
            _sprites.DrawSolidRectangle(spriteBatch, new Rectangle(right, y, thickness, thickness), colour);
        }
    }
}
