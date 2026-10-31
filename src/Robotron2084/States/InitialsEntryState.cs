using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Graphics;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Palette;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
///     The CONG screen — the arcade's "YOU ARE A ROBOTRON HERO / ENTER YOUR INITIALS:" page (notes §116).
///     It collects one player's initials through <see cref="InitialsEntryModel" />, offers the score to the
///     table under them, and hands the ceremony on to its next screen.
/// </summary>
/// <remarks>
///     ROM RRTESTC <c>EGSUB</c> printing RRET message 95 (<c>CONGP</c>): <c>COLOR $44</c>, "PLAYER n" at
///     (64, 16), "YOU ARE A ROBOTRON HERO" at (41, 48), "ENTER YOUR INITIALS:" at (45, 88), then
///     <c>TELSUB</c>'s small-font white instructions under the letters' echo region. Cursors, palette indices and
///     cell geometry are <see cref="InitialsEntryLayout" />'s.
/// </remarks>
public sealed class InitialsEntryState : IGameState
{
    private readonly ScoreEntryCeremony _scoreEntryCeremony;
    private readonly InitialsEntryModel _initialsEntryModel = new();
    private readonly IPlayerInputSource _playerInputSource;
    private readonly FinalScore _score;
    private readonly GameServices _gameServices;
    private readonly SpriteSet _spriteSet;

    /// <summary>Builds the screen for one qualifying score.</summary>
    /// <param name="gameServices">The attract screens' bundle: sprites, the store, the controls and player 1's input.</param>
    /// <param name="scoreEntryCeremony">The ceremony this screen belongs to, which the entered score is written back into.</param>
    /// <param name="score">The score being entered, whose player number the page prints.</param>
    public InitialsEntryState(GameServices gameServices, ScoreEntryCeremony scoreEntryCeremony, FinalScore score)
    {
        _gameServices = gameServices;
        _scoreEntryCeremony = scoreEntryCeremony;
        _score = score;
        _spriteSet = gameServices.SpriteSet;
        _playerInputSource = gameServices.PlayerInputSource;
        RestorePalette();
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        DrawPage(spriteBatch);
        DrawCells(spriteBatch);
    }

    public void Update(GameTime gameTime, GameStateManager gameStateManager)
    {
        var playerInputState = _playerInputSource.Poll();
        if (!_initialsEntryModel.Tick(playerInputState)) return;

        var result = _scoreEntryCeremony.Submit(_score, _initialsEntryModel.GetInitials());
        if (result.ReachedEntriesMaximum)
        {
            gameStateManager.TransitionTo(new EntriesMaximumState(_gameServices, _scoreEntryCeremony));
            return;
        }

        gameStateManager.TransitionTo(_scoreEntryCeremony.NextScreen());
    }

    private void DrawCell(SpriteBatch spriteBatch, int cell, int x)
    {
        if (cell == _initialsEntryModel.LetterIndex && _initialsEntryModel.IsPreviewRub())
        {
            _spriteSet.ArcadeText.DrawRubMarker(spriteBatch, x, InitialsEntryLayout.GetEchoY(),
                InitialsEntryLayout.InkPaletteIndex);
            return;
        }

        _spriteSet.ArcadeText.DrawLargeFontText(spriteBatch, _initialsEntryModel.GetInitials()[cell].ToString(), x,
            InitialsEntryLayout.GetEchoY(), InitialsEntryLayout.InkPaletteIndex);
    }

    /// <summary>
    ///     The three cells of the echo region: each letter, the cursor's cell showing its preview — the ROM's
    ///     own rub marker when that is what the cell holds — and G0SUB's white dash under every cell.
    /// </summary>
    private void DrawCells(SpriteBatch spriteBatch)
    {
        for (var cell = 0; cell < InitialsEntryModel.LetterCount; cell++)
        {
            var x = InitialsEntryLayout.GetCellX(cell);
            DrawCell(spriteBatch, cell, x);
            DrawMarker(spriteBatch, x);
        }
    }

    private void DrawLarge(SpriteBatch spriteBatch, string text, int column, int row)
    {
        _spriteSet.ArcadeText.DrawLargeFontText(spriteBatch, text, HudLayout.ToPortColumnX(column),
            HudLayout.ToPortY(row), InitialsEntryLayout.InkPaletteIndex);
    }

    /// <summary>G0SUB's "frob" marker: a two-pixel dash one row of the arcade below its cell.</summary>
    private void DrawMarker(SpriteBatch spriteBatch, int x)
    {
        _spriteSet.BlitterDraw.DrawSolidRectangle(
            spriteBatch,
            new Rectangle(x, InitialsEntryLayout.GetMarkerY(), InitialsEntryLayout.GetMarkerWidthPixels(),
                InitialsEntryLayout.GetMarkerHeightPixels()),
            _spriteSet.BlitterDraw.GetPaletteIndexColour(InitialsEntryLayout.InstructionPaletteIndex));
    }

    /// <summary>CONGP's three large-font lines and TELSUB's two small-font instructions.</summary>
    private void DrawPage(SpriteBatch spriteBatch)
    {
        DrawLarge(spriteBatch, $"PLAYER {_score.PlayerNumber}", InitialsEntryLayout.PlayerColumn,
            InitialsEntryLayout.PlayerRow);
        DrawLarge(spriteBatch, "YOU ARE A ROBOTRON HERO", InitialsEntryLayout.HeroColumn, InitialsEntryLayout.HeroRow);
        DrawLarge(spriteBatch, "ENTER YOUR INITIALS:", InitialsEntryLayout.PromptColumn, InitialsEntryLayout.PromptRow);
        DrawSmall(spriteBatch, "USE -MOVE- TO SELECT LETTER", InitialsEntryLayout.SelectColumn,
            InitialsEntryLayout.SelectRow);
        DrawSmall(spriteBatch, "-FIRE UP- TO ENTER LETTER", InitialsEntryLayout.FireColumn,
            InitialsEntryLayout.FireRow);
    }

    private void DrawSmall(SpriteBatch spriteBatch, string text, int column, int row)
    {
        _spriteSet.ArcadeText.DrawSmallFontText(spriteBatch, text, HudLayout.ToPortColumnX(column),
            HudLayout.ToPortY(row), InitialsEntryLayout.InstructionPaletteIndex);
    }

    /// <summary>
    ///     The two entries the page draws with, put back on their CRTAB values: the screen it follows may
    ///     have left them holding anything (the in-play animator and the wave colours own palette indices 0-15), and
    ///     the page's own colours are CONGP's palette index 4 and TELSUB's palette index 9.
    /// </summary>
    private void RestorePalette()
    {
        if (_spriteSet.BlitterDraw.GamePalette is not { } palette) return;

        palette.SetPaletteIndexValue(InitialsEntryLayout.InkPaletteIndex, GamePalette.DefaultPaletteValues[InitialsEntryLayout.InkPaletteIndex]);
        palette.SetPaletteIndexValue(InitialsEntryLayout.InstructionPaletteIndex,
            GamePalette.DefaultPaletteValues[InitialsEntryLayout.InstructionPaletteIndex]);
    }
}
