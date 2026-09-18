using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Persistence;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
/// The in-game state: owns the current <see cref="PlayField"/> for the player
/// whose turn it is, plus the <see cref="GameSession"/> that keeps every player's
/// score, lives and wave.
///
/// Turn handling is the arcade's (RRG23 PLEND/PLEND3/PLE1B): a death takes a man
/// and, in a 2-player game, hands the turn to the other player if they still have
/// men — who resumes at THEIR own wave. When nobody has men the game ends; when
/// only the player who just died is out, the ROM shows "PLAYER n GAME OVER"
/// before the other one resumes. A wave clear advances the CURRENT player's wave.
///
/// The HUD is the arcade's too (notes §58): each player's score in the top band at
/// their own column, their spare men as mini man icons to its right, and the wave
/// indicator at the bottom of the screen.
/// </summary>
public sealed class PlayingState : IGameState
{
    private static readonly int Margin = ScreenSize.Scaled(GameplayConstants.PlayfieldMarginSpecPixels);
    private static readonly Rectangle InnerBounds = new(Margin, Margin, ScreenSize.Width - 2 * Margin, ScreenSize.Height - 2 * Margin);

    private readonly SpriteSet _sprites;
    private readonly HighScoreStore _highScores;
    private readonly GameSession _session;
    private readonly LevelParameterGenerator _generator = new();
    private readonly Random _random = new();
    private PlayField _field;
    private bool _restartHandled;       // one-shot so the death branch fires exactly once
    private int _turnMessageTicks;      // "PLAYER n" at a 2-player turn start (ROM NAP 115)
    private int _playerOutMessageTicks; // "PLAYER n GAME OVER" (ROM NAP $60)
    private int _playerOutNumber;

    public PlayingState(SpriteSet sprites, HighScoreStore highScores, GameSession session)
    {
        _sprites = sprites;
        _highScores = highScores;
        _session = session;
        _field = BuildField();
        AnnounceTurn();
    }

    /// <summary>Builds the playfield for whoever's turn it is, from their own state.</summary>
    private PlayField BuildField()
    {
        PlayerSlot slot = _session.Current;
        LevelParameters parameters = _generator.Generate(slot.Wave);
        WallColorCycle cycle = new(GameplayConstants.DefaultWallPalette, TimeSpan.FromMilliseconds(GameplayConstants.WallStepDurationMilliseconds));
        _field = new PlayField(parameters, slot.Input, InnerBounds, cycle, _random, slot.Lives, slot.Score, slot.Rescues, _sprites.Palette);
        _restartHandled = false;
        return _field;
    }

    /// <summary>
    /// ROM RRG23 PLS0D: at the start of every 2-player turn the ROM prints
    /// "PLAYER n" at the screen centre and waits NAP 115 before erasing it. A
    /// 1-player game skips it entirely (<c>LDA PLRCNT / DECA / BEQ PLS0A</c>).
    /// </summary>
    private void AnnounceTurn()
    {
        _turnMessageTicks = _session.IsTwoPlayer
            ? GameplayConstants.PortTicks(GameplayConstants.PlayerTurnMessageRomFrames)
            : 0;
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        // "PLAYER n GAME OVER" is a WAIT in the ROM (NAP $60): the field behind it
        // is frozen and the next player's wave has not been built yet.
        if (_playerOutMessageTicks > 0)
        {
            _playerOutMessageTicks--;
            return;
        }

        if (_turnMessageTicks > 0)
        {
            _turnMessageTicks--;
        }

        _field.Update(gameTime);

        PlayerInputState input = _session.Current.Input.Poll();

        // Wave clear (Phase 11.3) — checked before the death check. The P key
        // (port test key, 2026-09-13 round 6) takes the same path so waves can be
        // playtested out of order.
        if (_field.IsLevelCleared || input.SkipLevelPressed)
        {
            HandleWaveCleared(manager);
            return;
        }

        // Death animation finished this frame (one-shot via _restartHandled).
        if (_field.Player.LifeState == EntityLifeState.Dead && !_restartHandled)
        {
            _restartHandled = true;
            HandlePlayerDeath(manager);
        }
    }

    /// <summary>
    /// ROM GEXEC0: the wave is cleared for whoever is playing — only THEIR wave
    /// counter advances (<c>INC PWAV,X</c>, skipping 0) and only their state
    /// carries over.
    /// </summary>
    private void HandleWaveCleared(GameStateManager manager)
    {
        PlayerSlot slot = _session.Current;
        int clearedWave = slot.Wave;
        SyncSlotFromField();

        // ROM GEXX/GEXX1: INC PWAV,X / BNE / INC PWAV,X — a byte counter that skips 0.
        slot.Wave = (slot.Wave % 255) + 1;

        manager.TransitionTo(new WaveClearState(_sprites, _highScores, _session, clearedWave));
    }

    /// <summary>
    /// ROM PLEND → PLEND3 → PLE1B. <see cref="Player.Kill"/> has already taken the
    /// man (the ROM's <c>PLSTRT</c> takes it when a life starts), so
    /// <see cref="PlayField.Player"/> already holds the remaining count.
    /// </summary>
    private void HandlePlayerDeath(GameStateManager manager)
    {
        PlayerSlot dead = _session.Current;
        SyncSlotFromField();
        dead.Rescues = 0; // ROM PLINIT clears SAVCNT

        if (_session.IsTwoPlayer)
        {
            // ROM PLE1B: the turn passes to the other player while they have men.
            _session.SwitchToPlayerWithMen();
        }

        if (!_session.AnyMenLeft)
        {
            // The score that ends the game is the CURRENT player's — but a 2-player
            // game offers both scores to the high-score table (RRTESTC checks
            // ZP1SCR and ZP2SCR), so the session hands over all of them.
            manager.TransitionTo(GameOverState.FromSession(_session.Current.Input, _sprites, _highScores, _session));
            return;
        }

        if (!dead.HasMen && _session.IsTwoPlayer)
        {
            // ROM PLEND3: this player is out and the other still has men — print
            // "PLAYER n GAME OVER" and wait NAP $60 before the turn passes.
            _playerOutNumber = dead.Number;
            _playerOutMessageTicks = GameplayConstants.PortTicks(GameplayConstants.PlayerGameOverMessageRomFrames);
        }

        _field = BuildField();
        AnnounceTurn();
    }

    /// <summary>Copies the live field's counters back into the current player's slot.</summary>
    private void SyncSlotFromField()
    {
        PlayerSlot slot = _session.Current;
        slot.Score = _field.Score.Score;
        slot.Lives = _field.Player.Lives;
        slot.Rescues = _field.RescuesThisLife;
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        _field.Draw(spriteBatch, _sprites);
        DrawHud(spriteBatch);
    }

    /// <summary>
    /// The arcade HUD (notes §58): in the top band, each player's score at their
    /// own column (P1 col 21, P2 col 85) with their spare men as mini man icons
    /// immediately to its right (P1 col 46, P2 col 110); at the bottom, the
    /// "<c>n  WAVE</c>" indicator.
    /// </summary>
    private void DrawHud(SpriteBatch spriteBatch)
    {
        // The ROM's HUD row is eight rows above the top wall (row 14 vs 22), so the
        // port measures from ITS wall band (inner bounds minus the wall thickness).
        int wallTop = InnerBounds.Top - ScreenSize.Scaled(GameplayConstants.WallThicknessSpecPixels);
        int hudY = wallTop - ScreenSize.Scaled(GameplayConstants.HudRowAboveWallPixels);

        foreach (PlayerSlot player in _session.Players)
        {
            bool isPlayerOne = player.Number == 1;
            int scoreColumn = isPlayerOne ? GameplayConstants.HudScoreOriginColumnP1 : GameplayConstants.HudScoreOriginColumnP2;
            int menColumn = isPlayerOne ? GameplayConstants.HudMenOriginColumnP1 : GameplayConstants.HudMenOriginColumnP2;

            // ROM $DC13/$DC19: the player whose turn it is blits their score with
            // $AA (slot 10 — one of the colour-CYCLING slots); an idle player's
            // uses $11 (slot 1).
            int slot = ReferenceEquals(player, _session.Current)
                ? GameplayConstants.HudScoreSlotCurrent
                : GameplayConstants.HudScoreSlotIdle;

            DrawScore(spriteBatch, player.Score, GameplayConstants.ArcadeX(scoreColumn * 2), hudY, slot);
            DrawSpareMen(spriteBatch, player.DisplayedMen, GameplayConstants.ArcadeX(menColumn * 2), hudY);
        }

        DrawWaveMessage(spriteBatch, _session.Current.Wave);

        if (_playerOutMessageTicks > 0)
        {
            // ROM string 75: "PLAYER n" at $3F79 then "GAME OVER" at $3E86.
            int messageSlot = GameplayConstants.PostSlotForWave(_session.Current.Wave);
            DrawMessageText(spriteBatch, $"PLAYER {_playerOutNumber}", GameplayConstants.PlayerTurnMessageColumn, GameplayConstants.PlayerGameOverMessageRow, messageSlot);
            DrawMessageText(spriteBatch, "GAME OVER", GameplayConstants.GameOverMessageColumn, GameplayConstants.GameOverMessageRow, messageSlot);
        }
        else if (_turnMessageTicks > 0)
        {
            // ROM string 103: "PLAYER n" at $3F7A, in the wave's POST colour
            // (PLS0D: LDA PSTCOL / STA TEXCOL), for NAP 115.
            DrawMessageText(
                spriteBatch,
                $"PLAYER {_session.Current.Number}",
                GameplayConstants.PlayerTurnMessageColumn,
                GameplayConstants.PlayerTurnMessageRow,
                GameplayConstants.PostSlotForWave(_session.Current.Wave));
        }
    }

    /// <summary>
    /// ROM $DC13 → $6096: the score as seven large-font glyphs at the cursor, a
    /// drawn digit advancing 7 px and a suppressed leading zero 6 px.
    /// </summary>
    private void DrawScore(SpriteBatch spriteBatch, int score, int originX, int y, int slot)
    {
        foreach (ScoreFormatter.ScoreGlyph glyph in ScoreFormatter.Layout(
            score,
            originX,
            ScreenSize.Scaled(GameplayConstants.HudScoreDigitAdvancePixels),
            ScreenSize.Scaled(GameplayConstants.HudScoreBlankAdvancePixels)))
        {
            _sprites.DrawGlyphSlot(spriteBatch, _sprites.FontLarge, glyph.Digit, glyph.X, y, slot);
        }
    }

    /// <summary>ROM $34E0: the spare-man icons, 8 px apart.</summary>
    private void DrawSpareMen(SpriteBatch spriteBatch, int count, int originX, int y)
    {
        int pitch = ScreenSize.Scaled(GameplayConstants.HudMenPitchPixels);

        for (int i = 0; i < count; i++)
        {
            _sprites.DrawMiniMan(spriteBatch, originX + (i * pitch), y);
        }
    }

    /// <summary>
    /// ROM string 104: the wave number in $AA at the BOTTOM of the screen (row
    /// 238, well below the playfield), then +6 px, then " WAVE" in $BB. The ROM
    /// draws it at each wave start and leaves it up while the wave runs.
    /// </summary>
    private void DrawWaveMessage(SpriteBatch spriteBatch, int wave)
    {
        int x = GameplayConstants.ArcadeX(GameplayConstants.HudWaveTextColumn * 2);
        int y = GameplayConstants.ArcadeY(GameplayConstants.HudWaveTextRow);
        const int numberSlot = GameplayConstants.HudScoreSlotCurrent;

        if (wave >= 10)
        {
            x = _sprites.DrawSmallFontText(spriteBatch, ((wave / 10) % 10).ToString(), x, y, numberSlot);
        }
        else
        {
            // PRINT_BCD_NUMBER with $D1 = 2 (string 104's $15 op): a leading zero
            // advances without drawing — 4 px in the small font.
            x += ScreenSize.Scaled(GameplayConstants.HudSmallFontBlankAdvancePixels);
        }

        x = _sprites.DrawSmallFontText(spriteBatch, (wave % 10).ToString(), x, y, numberSlot);
        x += ScreenSize.Scaled(GameplayConstants.HudWaveNumberGapPixels);
        _sprites.DrawSmallFontText(spriteBatch, " WAVE", x, y, GameplayConstants.HudWaveTextSlot);
    }

    /// <summary>Draws one of the ROM's message strings at its own cursor column/row.</summary>
    private void DrawMessageText(SpriteBatch spriteBatch, string text, int arcadeColumn, int arcadeRow, int slot)
    {
        _sprites.DrawSmallFontText(
            spriteBatch,
            text,
            GameplayConstants.ArcadeX(arcadeColumn * 2),
            GameplayConstants.ArcadeY(arcadeRow),
            slot);
    }
}
