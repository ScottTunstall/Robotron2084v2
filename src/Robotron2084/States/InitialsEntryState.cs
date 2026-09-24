using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Persistence;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
/// The CONG screen — the arcade's "YOU ARE A ROBOTRON HERO / ENTER YOUR INITIALS:" page (notes §116).
/// It collects one player's initials through <see cref="InitialsEntryModel"/>, offers the score to the
/// table under them, and hands the ceremony on to its next screen.
/// </summary>
/// <remarks>
/// ROM RRTESTC <c>EGSUB</c> printing RRET message 95 (<c>CONGP</c>): <c>COLOR $44</c>, "PLAYER n" at
/// (64, 16), "YOU ARE A ROBOTRON HERO" at (41, 48), "ENTER YOUR INITIALS:" at (45, 88), then
/// <c>TELSUB</c>'s small-font white instructions under the letters' echo region. Cursors, slots and
/// cell geometry are <see cref="InitialsEntryLayout"/>'s.
/// </remarks>
public sealed class InitialsEntryState : IGameState
{
    private readonly GameServices _services;
    private readonly ScoreEntryCeremony _ceremony;
    private readonly FinalScore _score;
    private readonly SpriteSet _sprites;
    private readonly IPlayerInputSource _input;
    private readonly InitialsEntryModel _entry = new();

    /// <summary>Builds the screen for one qualifying score.</summary>
    /// <param name="services">The attract screens' bundle: sprites, the store, the controls and player 1's input.</param>
    /// <param name="ceremony">The ceremony this screen belongs to, which the entered score is written back into.</param>
    /// <param name="score">The score being entered, whose player number the page prints.</param>
    public InitialsEntryState(GameServices services, ScoreEntryCeremony ceremony, FinalScore score)
    {
        _services = services;
        _ceremony = ceremony;
        _score = score;
        _sprites = services.Sprites;
        _input = services.Input;
        RestorePalette();
    }

    /// <summary>
    /// The two entries the page draws with, put back on their CRTAB values: the screen it follows may
    /// have left them holding anything (the in-play animator and the wave colours own slots 0-15), and
    /// the page's own colours are CONGP's slot 4 and TELSUB's slot 9.
    /// </summary>
    private void RestorePalette()
    {
        if (_sprites.Palette is not { } palette)
        {
            return;
        }

        palette.SetSlot(InitialsEntryLayout.InkSlot, GamePalette.DefaultSlots[InitialsEntryLayout.InkSlot]);
        palette.SetSlot(InitialsEntryLayout.InstructionSlot, GamePalette.DefaultSlots[InitialsEntryLayout.InstructionSlot]);
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        PlayerInputState input = _input.Poll();
        if (!_entry.Tick(input))
        {
            return;
        }

        HighScoreTable.SubmitResult result = _ceremony.Submit(_score, _entry.Initials);
        manager.TransitionTo(result.EntriesMaximum
            ? new EntriesMaximumState(_services, _ceremony)
            : _ceremony.NextScreen());
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        DrawPage(spriteBatch);
        DrawCells(spriteBatch);
    }

    /// <summary>CONGP's three large-font lines and TELSUB's two small-font instructions.</summary>
    private void DrawPage(SpriteBatch spriteBatch)
    {
        DrawLarge(spriteBatch, $"PLAYER {_score.PlayerNumber}", InitialsEntryLayout.PlayerColumn, InitialsEntryLayout.PlayerRow);
        DrawLarge(spriteBatch, "YOU ARE A ROBOTRON HERO", InitialsEntryLayout.HeroColumn, InitialsEntryLayout.HeroRow);
        DrawLarge(spriteBatch, "ENTER YOUR INITIALS:", InitialsEntryLayout.PromptColumn, InitialsEntryLayout.PromptRow);
        DrawSmall(spriteBatch, "USE -MOVE- TO SELECT LETTER", InitialsEntryLayout.SelectColumn, InitialsEntryLayout.SelectRow);
        DrawSmall(spriteBatch, "-FIRE UP- TO ENTER LETTER", InitialsEntryLayout.FireColumn, InitialsEntryLayout.FireRow);
    }

    /// <summary>
    /// The three cells of the echo region: each letter, the cursor's cell showing its preview — the ROM's
    /// own rub marker when that is what the cell holds — and G0SUB's white dash under every cell.
    /// </summary>
    private void DrawCells(SpriteBatch spriteBatch)
    {
        for (int cell = 0; cell < InitialsEntryModel.LetterCount; cell++)
        {
            int x = InitialsEntryLayout.CellX(cell);
            DrawCell(spriteBatch, cell, x);
            DrawMarker(spriteBatch, x);
        }
    }

    private void DrawCell(SpriteBatch spriteBatch, int cell, int x)
    {
        if (cell == _entry.Position && _entry.PreviewIsRub)
        {
            _sprites.DrawRubMarker(spriteBatch, x, InitialsEntryLayout.EchoY, InitialsEntryLayout.InkSlot);
            return;
        }

        _sprites.DrawLargeFontText(spriteBatch, _entry.Initials[cell].ToString(), x, InitialsEntryLayout.EchoY, InitialsEntryLayout.InkSlot);
    }

    /// <summary>G0SUB's "frob" marker: a two-pixel dash one row of the arcade below its cell.</summary>
    private void DrawMarker(SpriteBatch spriteBatch, int x) =>
        _sprites.DrawSolidRectangle(
            spriteBatch,
            new Rectangle(x, InitialsEntryLayout.MarkerY, InitialsEntryLayout.MarkerWidthPixels, InitialsEntryLayout.MarkerHeightPixels),
            _sprites.SlotColor(InitialsEntryLayout.InstructionSlot));

    private void DrawLarge(SpriteBatch spriteBatch, string text, int column, int row) =>
        _sprites.DrawLargeFontText(spriteBatch, text, GameplayConstants.ArcadeColumnX(column), GameplayConstants.ArcadeY(row), InitialsEntryLayout.InkSlot);

    private void DrawSmall(SpriteBatch spriteBatch, string text, int column, int row) =>
        _sprites.DrawSmallFontText(spriteBatch, text, GameplayConstants.ArcadeColumnX(column), GameplayConstants.ArcadeY(row), InitialsEntryLayout.InstructionSlot);
}
