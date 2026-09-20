using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Persistence;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
/// The arcade title screen (notes §94.1): the ROM draws the playfield wall +
/// the player's lives + the score FIRST ($26D2, wall colour $CC), then prints
/// string 128 — "ROBOTRON 2084" — and, in its blink loop, string 129 —
/// "SAVE THE LAST HUMAN FAMILY" — both in the LARGE font, colour slot 10
/// (the ROM's $AA). The ROM's tagline is SAVE, not PROTECT (RRET.ASM:982).
///
/// Port conventions kept on top: **1** starts a one-player game and **2** a
/// two-player game (the arcade's START 1 / START 2 buttons, ROM RRG23
/// START1/START2; fire is the one-player alias), a blinking start prompt, and
/// every 5 seconds the prompt swaps for the saved top-10 high-score list
/// (Phase 11.7). After <see cref="GameplayConstants.TitleIdleSeconds"/> of
/// no start press the arcade's attract demo takes over (Phase 12.1, notes §94).
/// </summary>
public sealed class TitleScreenState : IGameState
{
    private static readonly int Margin = ScreenSize.Scaled(GameplayConstants.PlayfieldMarginSpecPixels);
    private static readonly Rectangle InnerBounds = new(Margin, Margin, ScreenSize.Width - 2 * Margin, ScreenSize.Height - 2 * Margin);

    private const string TitleLineOne = "ROBOTRON 2084";
    private const string TitleLineTwo = "SAVE THE LAST HUMAN FAMILY";

    private readonly IPlayerInputSource _input;
    private readonly SpriteSet _sprites;
    private readonly HighScoreStore _highScores;
    private readonly PlayfieldWall _titleWall;
    private readonly GameSession _titleSession;
    private readonly TimeSpan _blinkDuration = TimeSpan.FromSeconds(GameplayConstants.TitleBlinkIntervalSeconds);
    private readonly TimeSpan _idleDuration = TimeSpan.FromSeconds(GameplayConstants.TitleIdleSeconds);
    private TimeSpan _blinkElapsed;
    private TimeSpan _idleElapsed;
    private bool _showPrompt = true;
    private bool _previousFire;
    private bool _previousStartOne;
    private bool _previousStartTwo;

    public TitleScreenState(IPlayerInputSource input, SpriteSet sprites, HighScoreStore highScores)
    {
        _input = input;
        _sprites = sprites;
        _highScores = highScores;

        // The ROM's title wall is a solid $CC (slot 12), not a wave colour.
        _titleWall = new PlayfieldWall(InnerBounds, new WallColorCycle(
            GameplayConstants.DefaultWallPalette,
            TimeSpan.FromMilliseconds(GameplayConstants.WallStepDurationMilliseconds)));

        // A 1P session at score 0 with the starting men: the ROM prints the
        // player's score and spare men under the title exactly as in play.
        _titleSession = GameSession.NewGame(input, 1);
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        PlayerInputState input = _input.Poll();

        // The arcade's coin-door buttons: START 1 / START 2 pick the number of
        // players (ROM RRG23 START1/START2 → PLRCNT), and the ROM then runs the
        // alternating 2-player game. Fire is kept as a 1-player alias (a port
        // convention — spec.txt's "press fire", which the arcade does not have).
        int playerCount = 0;
        if (input.StartOnePlayerPressed && !_previousStartOne)
        {
            playerCount = 1;
        }
        else if (input.StartTwoPlayersPressed && !_previousStartTwo)
        {
            playerCount = 2;
        }
        else if (input.FirePressed && !_previousFire)
        {
            playerCount = 1;
        }

        if (playerCount > 0)
        {
            manager.TransitionTo(new PlayingState(_sprites, _highScores, GameSession.NewGame(_input, playerCount)));
            return;
        }

        _previousFire = input.FirePressed;
        _previousStartOne = input.StartOnePlayerPressed;
        _previousStartTwo = input.StartTwoPlayersPressed;

        // Any button held means a human is at the machine — the arcade's
        // attract only runs while the cabinet sits idle.
        if (input.FirePressed || input.StartOnePlayerPressed || input.StartTwoPlayersPressed)
        {
            _idleElapsed = TimeSpan.Zero;
        }

        // Blink the prompt every second (spec).
        _blinkElapsed += gameTime.ElapsedGameTime;
        while (_blinkElapsed >= _blinkDuration)
        {
            _blinkElapsed -= _blinkDuration;
            _showPrompt = !_showPrompt;
        }

        // The port's old "top ten" swap lived here; the arcade's table is its
        // own screen now (`HighScoreTableState`, notes §98) and the ROM's title
        // page (`FAMPAG`/`SPGSUB`) shows the title and nothing else.

        // Idle long enough: the machine starts playing itself (notes §94.3) — and
        // the arcade plays its STORY first: the ROM's FAMPAG/SPGSUB prints the
        // title, then the page script runs HISTO, whose DONE2 hands over to the
        // phony-player game (notes §95.1/§96).
        _idleElapsed += gameTime.ElapsedGameTime;
        if (_idleElapsed >= _idleDuration)
        {
            _idleElapsed = TimeSpan.Zero;
            manager.TransitionTo(new StorylineState(_sprites, _highScores, _input, new Random()));
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        // The caller clears to black. ROM order: wall + lives + score FIRST
        // ($26D2), the title strings on top.
        _titleWall.Draw(spriteBatch, _sprites.WallPixel, _sprites.SlotColor(GameplayConstants.TitleWallSlot));
        ArcadeHud.DrawScoresAndMen(spriteBatch, _sprites, _titleSession, InnerBounds);

        int slot = GameplayConstants.HudScoreSlotCurrent; // the ROM's $AA (slot 10)
        int lineOneY = GameplayConstants.ArcadeY(54);     // string 128's cursor row
        DrawCenteredLargeText(spriteBatch, TitleLineOne, lineOneY, slot);
        DrawCenteredLargeText(spriteBatch, TitleLineTwo, lineOneY + ScreenSize.Scaled(14), slot);

        if (_showPrompt)
        {
            const float promptScale = 1.0f;
            spriteBatch.DrawString(
                font,
                "Press 1 or 2 to start",
                CenteredHorizontal(font.MeasureString("Press 1 or 2 to start"), promptScale, ScreenSize.Scaled(110)),
                Color.LightGray,
                0f,
                Vector2.Zero,
                promptScale,
                SpriteEffects.None,
                0f);
        }
    }

    /// <summary>Centres a large-font line horizontally and prints it in one slot.</summary>
    private void DrawCenteredLargeText(SpriteBatch spriteBatch, string text, int y, int slot)
    {
        int width = 0;
        foreach (char character in text)
        {
            if (character == ' ')
            {
                width += ScreenSize.Scaled(GameplayConstants.HudSmallFontBlankAdvancePixels);
                continue;
            }

            int index = SpriteSet.GlyphIndex(character);
            if (index < 0 || index >= _sprites.FontLarge.Length)
            {
                continue;
            }

            width += ScreenSize.Scaled(_sprites.FontLarge[index].Width + GameplayConstants.HudSmallFontGlyphGapPixels);
        }

        _sprites.DrawLargeFontText(spriteBatch, text, (ScreenSize.Width - width) / 2, y, slot);
    }

    private static Vector2 CenteredHorizontal(Vector2 unscaledSize, float scale, float y) =>
        new((ScreenSize.Width - unscaledSize.X * scale) / 2, y);
}
