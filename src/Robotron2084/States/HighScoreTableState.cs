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
/// <item>the operator's top entry at (21, 122) in $AA/$DD (the ROM's SECOND colour
/// pair, which the all-time list below it shares) as "( NAME ) 1234567";</item>
/// <item>the ALL-TIME thirty-six rows in the SMALL font, 12 per column ×
/// 3 columns, from (20, 136), 7 rows / 40 columns apart, ranks 2-37;</item>
/// <item>a hatched frame in slot 8 between the ROM's corners, drawn the way the ROM
/// draws it — see <see cref="HighScoreFrameAnimation"/> and <see cref="DrawFrame"/>
/// (<c>FRAMER</c> grows it, then an erase pass in black leaves the band);</item>
/// <item>and the page's own colour processes, which are what make the arcade's page
/// move: <see cref="HighScorePalette"/> cycles the wall's slot through COLTAB, ramps
/// the two lists and their highlights, and drags the headers along a slot behind.</item>
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
    private readonly HighScorePalette _colour = new();
    private readonly HighScoreFrameAnimation _frame = new();
    private readonly HighScorePrintSequence _print = new();
    private readonly int _holdTicks = GameplayConstants.PortTicks(GameplayConstants.HighScoreHoldRomFrames);
    private readonly int _leaveTicks = GameplayConstants.PortTicks(GameplayConstants.HighScoreLeaveTimeoutRomFrames);
    private int _elapsedTicks;
    private bool _rampsStarted;
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

        // RRTABLE's TABLE starts the page's colour processes before it draws anything
        // (MAKP LOOPP comes first, the other four after the lists are printed).
        if (_sprites.Palette is { } palette)
        {
            _colour.Start(palette);
        }
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        _frame.Tick();

        // RRTABLE's order: FRAMER draws the wall on an EMPTY page (SCRCLR ran before
        // TABORG), and only when it returns does PRJNK print the lists four rows a ROM
        // frame, then the top entry, then the headers — and only THEN do the four ramp
        // processes start and the 600-frame hold begin (notes §98.6).
        _print.Tick(_frame.IsFinished, _table.Today.Count, _table.AllTime.Count);

        if (_print.IsDone)
        {
            if (!_rampsStarted)
            {
                _rampsStarted = true;
                if (_sprites.Palette is { } livePalette)
                {
                    _colour.StartRamps(livePalette);
                }
            }

            // The ROM's hold counts from the MAKPs, i.e. from the finished page.
            _elapsedTicks++;
        }

        if (_sprites.Palette is { } live)
        {
            _colour.Update(live);
        }

        PlayerInputState input = _input.Poll();
        bool pressed = (input.FirePressed && !_previousFire)
            || (input.StartOnePlayerPressed && !_previousStartOne)
            || (input.StartTwoPlayersPressed && !_previousStartTwo);

        _previousFire = input.FirePressed;
        _previousStartOne = input.StartOnePlayerPressed;
        _previousStartTwo = input.StartTwoPlayersPressed;

        // The ROM: hold 600 frames, then wait for ANY switch (with a 255 × 4 frame
        // timeout), then back to the family page. A press during the printing is
        // ignored — PRJNK has no switch check (the page finishes building first).
        if ((pressed && _print.IsDone) || _elapsedTicks >= _holdTicks + _leaveTicks)
        {
            if (_sprites.Palette is { } palette)
            {
                _colour.Stop(palette);
            }

            manager.TransitionTo(new TitleScreenState(_input, _sprites, _store));
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        DrawFrame(spriteBatch);

        // Nothing but the wall until the frame's passes have finished, then the page
        // prints itself in the ROM's own order: today's list, the top entry, the
        // all-time list, the headers (PRJNK + TABLE + SCRMES).
        DrawTodayList(spriteBatch, _print.TodayRows);

        if (_print.TopPrinted)
        {
            DrawTopEntry(spriteBatch);
        }

        DrawAllTimeList(spriteBatch, _print.AllTimeRows);

        if (_print.HeadersPrinted)
        {
            DrawHeader(spriteBatch, "ROBOTRON HEROES", HighScoreTableLayout.TodayHeaderRow);
            DrawHeader(spriteBatch, "ALL TIME HEROES", HighScoreTableLayout.AllTimeHeaderRow);
        }
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

    private void DrawTodayList(SpriteBatch spriteBatch, int printedRows)
    {
        IReadOnlyList<HighScoreEntry> entries = _table.Today;

        for (int rank = 1; rank <= HighScoreTableLayout.TodayRows && rank <= entries.Count && rank <= printedRows; rank++)
        {
            (int column, int row) = HighScoreTableLayout.TodayPosition(rank);
            int x = ColumnX(column);
            int y = RowY(row);
            int slot = SlotFor(entries[rank - 1], GameplayConstants.HighScoreTodaySlot, GameplayConstants.HighScoreTodayHighlightSlot);

            int afterRank = DrawRank(spriteBatch, rank, x, y, slot, large: true);
            _sprites.DrawLargeFontText(spriteBatch, entries[rank - 1].Initials, afterRank, y, slot);

            // The ROM's fixed offset from the POST-RANK cursor (RRTABLE's
            // `PSHS X` right after the rank message, then `LEAX D,X`): 11 columns
            // in the large font. The initials therefore run INTO that gap, which is
            // exactly what the arcade's rows look like — 3 large glyphs = 21 px of
            // the 22 px the offset leaves.
            if (entries[rank - 1].Score != 0)
            {
                _sprites.DrawLargeTableNumber(
                    spriteBatch,
                    entries[rank - 1].Score,
                    afterRank + ScreenSize.Scaled(HighScoreTableLayout.TodayScoreOffsetColumns * 2),
                    y,
                    slot);
            }
        }
    }

    private void DrawAllTimeList(SpriteBatch spriteBatch, int printedRows)
    {
        IReadOnlyList<HighScoreEntry> entries = _table.AllTime;

        for (int rank = 2; rank <= HighScoreTableLayout.AllTimeRows + 1 && rank - 2 < entries.Count && rank - 1 <= printedRows; rank++)
        {
            (int column, int row) = HighScoreTableLayout.AllTimePosition(rank);
            int x = ColumnX(column);
            int y = RowY(row);
            int slot = SlotFor(entries[rank - 2], GameplayConstants.HighScoreAllTimeSlot, GameplayConstants.HighScoreAllTimeHighlightSlot);

            int afterRank = DrawRank(spriteBatch, rank, x, y, slot, large: false);
            _sprites.DrawSmallFontText(spriteBatch, entries[rank - 2].Initials, afterRank, y, slot);

            if (entries[rank - 2].Score != 0)
            {
                _sprites.DrawSmallTableNumber(
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
        int slot = SlotFor(_table.Top.Score, GameplayConstants.HighScoreAllTimeSlot, GameplayConstants.HighScoreAllTimeHighlightSlot);

        int x = ColumnX(HighScoreTableLayout.TopColumn);
        x = _sprites.DrawLargeFontText(spriteBatch, "(", x, y, slot);
        x = _sprites.DrawLargeFontText(spriteBatch, _table.Top.Name, x, y, slot);
        x = _sprites.DrawLargeFontText(spriteBatch, ")", x, y, slot);
        _sprites.DrawLargeTableNumber(spriteBatch, _table.Top.Score, x + ScreenSize.Scaled(GameplayConstants.HudSmallFontBlankAdvancePixels), y, slot);
    }

    /// <summary>
    /// The ROM's message 111 (`INDMEP`): the rank, ')' and a space. The arcade's
    /// rows are NOT padded (its 10) sits a glyph further right than its 9)), which
    /// is what the author's photo of the cabinet shows.
    /// </summary>
    private int DrawRank(SpriteBatch spriteBatch, int rank, int x, int y, int slot, bool large)
    {
        string text = $"{rank}) ";
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
    /// The ROM's frame (`FRAMER` → `MARQ`, notes §98.5): the hatched band its two
    /// passes leave behind, drawn in slot 8's LIVE colour — which is what makes the wall
    /// cycle, because every wall pixel is palette index 8 and the LOOPP process keeps
    /// rewriting that slot.
    ///
    /// MARQ hands the blitter packed pairs and lights exactly ONE pixel of each (see
    /// <see cref="HighScoreTableLayout.FramePixelIsLit"/>), so the band is a fine
    /// checkerboard rather than a solid colour. The erase pass paints those same pixels
    /// black up to the rectangle <see cref="HighScoreFrameAnimation.InnerRect"/> reports,
    /// so only the part of the band outside it is drawn.
    /// </summary>
    private void DrawFrame(SpriteBatch spriteBatch)
    {
        (int outerLeft, int outerTop, int outerRight, int outerBottom) = _frame.OuterRect;
        (int innerLeft, int innerTop, int innerRight, int innerBottom) = _frame.InnerRect;
        Color colour = _sprites.SlotColor(GameplayConstants.HighScoreFrameSlot);

        for (int y = outerTop; y <= outerBottom; y++)
        {
            int top = GameplayConstants.ArcadeY(y);
            int height = GameplayConstants.ArcadeY(y + 1) - top;

            if (y >= innerTop && y <= innerBottom)
            {
                DrawHatchedRow(spriteBatch, colour, outerLeft, Math.Min(innerLeft - 1, outerRight), y, top, height);
                DrawHatchedRow(spriteBatch, colour, Math.Max(innerRight + 1, outerLeft), outerRight, y, top, height);
            }
            else
            {
                DrawHatchedRow(spriteBatch, colour, outerLeft, outerRight, y, top, height);
            }
        }
    }

    /// <summary>One raster row of MARQ's hatch: every other arcade pixel of the run.</summary>
    private void DrawHatchedRow(SpriteBatch spriteBatch, Color colour, int left, int right, int row, int top, int height)
    {
        for (int x = left; x <= right; x++)
        {
            if (!HighScoreTableLayout.FramePixelIsLit(x, row))
            {
                continue;
            }

            int px = GameplayConstants.ArcadeX(x);
            _sprites.DrawSolidRectangle(
                spriteBatch,
                new Rectangle(px, top, GameplayConstants.ArcadeX(x + 1) - px, height),
                colour);
        }
    }
}
