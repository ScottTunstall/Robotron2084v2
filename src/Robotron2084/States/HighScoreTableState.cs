using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Palette;
using Robotron2084.Persistence;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
/// THE ARCADE'S HIGH SCORE TABLE (<c>TABLE</c>, notes §98, §123) — RRTABLE's <c>TABLE</c>, drawn at
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
public sealed class HighScoreTableState : IGameState, IAttractState
{
    private readonly HighScorePalette _colour = new();
    private readonly HighScoreFrameAnimation _frame = new();
    private readonly HighScorePageHold _hold = new();
    private readonly IPlayerInputSource _input;
    private readonly int[] _postedScores;
    private readonly HighScorePrintSequence _print = new();
    private readonly GameSettings _settings;
    private readonly SpriteSet _sprites;
    private readonly HighScoreStore _store;
    private readonly HighScoreTable _table;
    private bool _rampsStarted;

    /// <summary>Creates the table screen for the scores this session just offered.</summary>
    /// <param name="services">The attract screens' shared dependencies.</param>
    /// <param name="postedScores">
    /// The scores this session just offered to the table (highest first) — the
    /// ROM's <c>ZP1SCR</c>/<c>ZP2SCR</c> at <c>CLSET</c> time: their rows are
    /// highlighted. Empty when the table is reached from the attract cycle.
    /// </param>
    /// <param name="table">
    /// The table to draw. The score ceremony hands over the very table it has just written the
    /// session's scores into, because TODAY's list is deliberately NOT persisted (the ROM reloads it
    /// from <c>TODTAB</c> at power-up) and a reload would lose the scores just posted. Null loads it
    /// from the store, which is what the attract cycle and the Insert dev key want.
    /// </param>
    public HighScoreTableState(GameServices services, IReadOnlyList<int>? postedScores = null, HighScoreTable? table = null)
    {
        _input = services.Input;
        _sprites = services.Sprites;
        _store = services.HighScores;
        _settings = services.Settings;
        _postedScores = [.. postedScores ?? []];
        _table = table ?? _store.Load();

        // The ROM's own score processing (EGSUB) has already happened by the time
        // the table is drawn; a save here is the CMOS write.
        _store.Save(_table);

        // RRTABLE's TABLE starts the page's colour processes before it draws anything
        // (MAKP LOOPP comes first, the other four after the lists are printed).
        if (_sprites.Blitter.Palette is { } palette)
        {
            _colour.Start(palette);
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

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        // The switches are only READ after the 600-frame hold (TAB888 has no check),
        // but polling every tick keeps the input source's own edge state moving.
        PlayerInputState input = _input.Poll();

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
                if (_sprites.Blitter.Palette is { } livePalette)
                {
                    _colour.StartRamps(livePalette);
                }
            }

            // TAB888's 200 x NAP 3 with NO switch check at all, then TAB777/TAB999 —
            // which leave the moment the switches are CLEAR and are delayed, not
            // shortened, by a switch that is held (see HighScorePageHold).
            if (_hold.Tick(AnySwitchHeld(input)))
            {
                Leave(manager);
                return;
            }
        }

        if (_sprites.Blitter.Palette is { } live)
        {
            _colour.Update(live);
        }
    }

    /// <summary>
    /// The ROM's PIA read at TAB999: PIA-B's two bits <c>OR</c> the whole of PIA-A, so
    /// any switch at all counts — either stick, either fire button, START 1 or START 2.
    /// A SET bit is a PRESSED switch (the $3031 movement table), and the port's own
    /// test keys (P) are not arcade switches, so they do not hold the page up.
    /// </summary>
    private static bool AnySwitchHeld(PlayerInputState input) =>
        input.FireHeld
        || input.StartOnePlayerHeld
        || input.StartTwoPlayersHeld
        || input.MoveDirection != IntVector2.Zero
        || input.ShootDirection != IntVector2.Zero;

    private static int GetColumnX(int column) => HudLayout.ToPortColumnX(column);

    private static int GetRowY(int row) => HudLayout.ToPortY(row);

    private void DrawAllTimeList(SpriteBatch spriteBatch, int printedRows)
    {
        IReadOnlyList<HighScoreEntry> entries = _table.AllTime;

        for (int rank = 2; rank <= HighScoreTableLayout.AllTimeRows + 1 && rank - 2 < entries.Count && rank - 1 <= printedRows; rank++)
        {
            (int column, int row) = HighScoreTableLayout.AllTimePosition(rank);
            int x = GetColumnX(column);
            int y = GetRowY(row);
            int slot = GetSlot(entries[rank - 2], ScreenTuning.HighScoreAllTimeSlot, ScreenTuning.HighScoreAllTimeHighlightSlot);

            int afterRank = DrawRank(spriteBatch, rank, x, y, slot, large: false);
            _sprites.Text.DrawSmallFontText(spriteBatch, entries[rank - 2].Initials, afterRank, y, slot);

            if (entries[rank - 2].Score != 0)
            {
                _sprites.Text.DrawSmallTableNumber(
                    spriteBatch,
                    entries[rank - 2].Score,
                    afterRank + ScreenSize.ToPortPixels(HighScoreTableLayout.AllTimeScoreOffsetColumns * 2),
                    y,
                    slot);
            }
        }
    }

    /// <summary>
    /// The ROM's frame (`FRAMER` → `MARQ`, notes §98.5/§98.7): the hatched band its two passes
    /// leave behind, drawn STROKE BY STROKE because every stroke has its OWN palette slot —
    /// MARQ's flavour walks down by `$11` a stroke (see
    /// <see cref="HighScoreTableLayout.FrameStrokeSlot"/>), so the eight visible strokes are
    /// slots 8…1, so the band carries eight cycling colours at once.
    /// LOOPP keeps rewriting slots 1-8, so all eight stripes cycle together, three frames apart.
    ///
    /// The erase pass paints those same pixels black, so only the strokes above
    /// <see cref="HighScoreFrameAnimation.ErasedStroke"/> are drawn.
    /// </summary>
    private void DrawFrame(SpriteBatch spriteBatch)
    {
        for (int stroke = _frame.ErasedStroke + 1; stroke <= _frame.DrawnStroke; stroke++)
        {
            DrawStroke(spriteBatch, stroke);
        }
    }

    /// <summary>One pixel column of the same hatch — the strokes' vertical edges.</summary>
    private void DrawHatchedColumn(SpriteBatch spriteBatch, Color colour, int column, int top, int bottom)
    {
        int px = HudLayout.ToPortX(column);
        int width = HudLayout.ToPortX(column + 1) - px;

        for (int y = top; y <= bottom; y++)
        {
            if (!HighScoreTableLayout.FramePixelIsLit(column, y))
            {
                continue;
            }

            int py = HudLayout.ToPortY(y);
            _sprites.Blitter.DrawSolidRectangle(
                spriteBatch,
                new Rectangle(px, py, width, HudLayout.ToPortY(y + 1) - py),
                colour);
        }
    }

    /// <summary>One raster row of MARQ's hatch: every other arcade pixel of the run.</summary>
    private void DrawHatchedRow(SpriteBatch spriteBatch, Color colour, int left, int right, int row)
    {
        int top = HudLayout.ToPortY(row);
        int height = HudLayout.ToPortY(row + 1) - top;

        for (int x = left; x <= right; x++)
        {
            if (!HighScoreTableLayout.FramePixelIsLit(x, row))
            {
                continue;
            }

            int px = HudLayout.ToPortX(x);
            _sprites.Blitter.DrawSolidRectangle(
                spriteBatch,
                new Rectangle(px, top, HudLayout.ToPortX(x + 1) - px, height),
                colour);
        }
    }

    private void DrawHeader(SpriteBatch spriteBatch, string text, int row)
    {
        _sprites.Text.DrawLargeFontText(
            spriteBatch,
            text,
            GetColumnX(HighScoreTableLayout.HeaderColumn),
            GetRowY(row),
            ScreenTuning.HighScoreHeaderSlot);
    }

    /// <summary>
    /// The ROM's message 111 (`INDMEP`): the rank, ')' and a space. The arcade's
    /// rows are NOT padded (its 10) sits a glyph further right than its 9)), which
    /// is what the cabinet shows.
    /// </summary>
    private int DrawRank(SpriteBatch spriteBatch, int rank, int x, int y, int slot, bool large)
    {
        string text = $"{rank}) ";
        return large
            ? _sprites.Text.DrawLargeFontText(spriteBatch, text, x, y, slot)
            : _sprites.Text.DrawSmallFontText(spriteBatch, text, x, y, slot);
    }

    /// <summary>
    /// One MARQ stroke: four hatched edges in that stroke's slot, each two pixels thick. The
    /// horizontal edges are the two rows of its top and bottom; the vertical ones are the two
    /// pixel columns of its left edge and of its right one — which MARQ puts at `RIGHT-1` and
    /// `RIGHT-2`, one pixel inside the rectangle's own right column: `VHIGH` runs at `RIGHT`
    /// and lights the HIGH nibble (that byte's left pixel), and `VLOW` at the `DECA`-shifted
    /// `RIGHT-1` lights the low one (the R5 disassembly's GFLIP case).
    /// </summary>
    private void DrawStroke(SpriteBatch spriteBatch, int stroke)
    {
        Color colour = _sprites.Blitter.GetSlotColour(HighScoreTableLayout.FrameStrokeSlot(stroke));
        (int left, int top, int right, int bottom) = HighScoreTableLayout.FrameStroke(stroke);

        DrawHatchedRow(spriteBatch, colour, left, right, top);
        DrawHatchedRow(spriteBatch, colour, left, right, top + 1);
        DrawHatchedRow(spriteBatch, colour, left, right, bottom - 1);
        DrawHatchedRow(spriteBatch, colour, left, right, bottom);
        DrawHatchedColumn(spriteBatch, colour, left, top, bottom);
        DrawHatchedColumn(spriteBatch, colour, left + 1, top, bottom);
        DrawHatchedColumn(spriteBatch, colour, right - 2, top, bottom);
        DrawHatchedColumn(spriteBatch, colour, right - 1, top, bottom);
    }

    private void DrawTodayList(SpriteBatch spriteBatch, int printedRows)
    {
        IReadOnlyList<HighScoreEntry> entries = _table.Today;

        for (int rank = 1; rank <= HighScoreTableLayout.TodayRows && rank <= entries.Count && rank <= printedRows; rank++)
        {
            (int column, int row) = HighScoreTableLayout.TodayPosition(rank);
            int x = GetColumnX(column);
            int y = GetRowY(row);
            int slot = GetSlot(entries[rank - 1], ScreenTuning.HighScoreTodaySlot, ScreenTuning.HighScoreTodayHighlightSlot);

            int afterRank = DrawRank(spriteBatch, rank, x, y, slot, large: true);
            _sprites.Text.DrawLargeFontText(spriteBatch, entries[rank - 1].Initials, afterRank, y, slot);

            // The ROM's fixed offset from the POST-RANK cursor (RRTABLE's
            // `PSHS X` right after the rank message, then `LEAX D,X`): 11 columns
            // in the large font. The initials therefore run INTO that gap, which is
            // exactly what the arcade's rows look like — 3 large glyphs = 21 px of
            // the 22 px the offset leaves.
            if (entries[rank - 1].Score != 0)
            {
                _sprites.Text.DrawLargeTableNumber(
                    spriteBatch,
                    entries[rank - 1].Score,
                    afterRank + ScreenSize.ToPortPixels(HighScoreTableLayout.TodayScoreOffsetColumns * 2),
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
        int y = GetRowY(HighScoreTableLayout.TopRow);
        int slot = GetSlot(_table.Top.Score, ScreenTuning.HighScoreAllTimeSlot, ScreenTuning.HighScoreAllTimeHighlightSlot);

        int x = GetColumnX(HighScoreTableLayout.TopColumn);
        x = _sprites.Text.DrawLargeFontText(spriteBatch, "(", x, y, slot);
        x = _sprites.Text.DrawLargeFontText(spriteBatch, _table.Top.Name, x, y, slot);
        x = _sprites.Text.DrawLargeFontText(spriteBatch, ")", x, y, slot);
        _sprites.Text.DrawLargeTableNumber(spriteBatch, _table.Top.Score, x + ScreenSize.ToPortPixels(HudLayout.HudSmallFontBlankAdvancePixels), y, slot);
    }

    private void Leave(GameStateManager manager)
    {
        if (_sprites.Blitter.Palette is { } palette)
        {
            _colour.Stop(palette);
        }

        manager.TransitionTo(new TitleScreenState(new GameServices(_sprites, _store, ControlSettings.CreateDefaults(), _input, _settings)));
    }

    /// <summary>
    /// ROM `CLSET`: a row holding a score the current players posted is drawn in
    /// the second colour — but only when the NEXT table entry is not the same
    /// score, so a repeated score highlights once.
    /// </summary>
    private int GetSlot(HighScoreEntry entry, int normalSlot, int highlightSlot) =>
        GetSlot(entry.Score, normalSlot, highlightSlot);

    private int GetSlot(int score, int normalSlot, int highlightSlot) =>
        score != 0 && _postedScores.Contains(score) ? highlightSlot : normalSlot;
}
