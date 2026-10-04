using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Graphics;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Level.Collisions;
using Robotron2084.Palette;
using Robotron2084.Persistence;
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
    /// <summary>The wave counter is one byte, so it wraps here (ROM <c>GEXX</c>). A player's wave number is divided by this and the remainder, plus one, becomes <see cref="PlayerSlot.Wave"/>, so the wave number goes back to the start after this many.</summary>
    private const int WaveCounterWrap = 255;

    private readonly LevelParameterGenerator _generator = new();
    private readonly HighScoreStore _highScoreStore;
    private readonly PauseToggle _pause = new();
    private readonly Random _random = new();
    private readonly GameSession _session;
    private readonly GameSettings _gameSettings;
    private readonly SpriteSet _sprites;
    private PlayField _field;
    private int _playerOutMessageTicks;

    // "PLAYER n GAME OVER" (ROM NAP $60)
    private int _playerOutNumber;

    private bool _restartHandled;       // one-shot so the death branch fires exactly once
    private int _turnMessageTicks;      // "PLAYER n" at a 2-player turn start (ROM NAP 115)

    /// <summary>Makes the state for the wave the current player is about to play.</summary>
    /// <param name="sprites">The sprite set that everything is drawn with.</param>
    /// <param name="highScoreStore">The high score table, which is offered the scores when the game ends.</param>
    /// <param name="session">The game in progress: its players, and whose turn it is.</param>
    /// <param name="isStartOfTurn">True when a turn is starting, which is at the start of the game. The arcade shows whose turn it is then, and after a death, but not when a player goes on from one wave to the next.</param>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>PLS000</c>, <c>LDA PCFLG / BNE PLS00C</c> ("NO DEATH.. NO MESSIE POOH"); <c>PCFLG</c> is set when a game starts and is left set when the player dies. Disassembly: <c>$27E7</c>.</remarks>
    public PlayingState(SpriteSet sprites, HighScoreStore highScoreStore, GameSession session, bool isStartOfTurn = false)
    {
        _sprites = sprites;
        _highScoreStore = highScoreStore;
        _session = session;
        _gameSettings = session.GameSettings;
        _field = BuildField();
        if (isStartOfTurn)
        {
            AnnounceTurn();
        }
    }

    /// <summary>
    /// Starts a game in one of the port's three modes (notes §101), giving each player
    /// their OWN input source — which is the point of the DEFINITIONS page, since player
    /// 2 no longer has to share player 1's controls. The mode is what F1/F2/F3 select on
    /// the title and from anywhere in the attract cycle; TWO PLAYER SIMULTANEOUS is
    /// carried as far as this call and the second input (the simultaneous field itself is not built yet).
    /// </summary>
    /// <param name="controls">The keys and buttons the players have chosen.</param>
    /// <param name="settings">
    /// The GAME ADJUSTMENT settings (notes §131): TURNS PER PLAYER is the men each player starts
    /// with, EXTRA MAN EVERY the score that earns a spare man, and DIFFICULTY OF PLAY nudges every
    /// wave's tuning.
    /// </param>
    /// <param name="mode">How many people are playing, and whether two of them take turns.</param>
    /// <param name="sprites">The sprite set that everything is drawn with.</param>
    /// <param name="highScoreStore">The high score table, which is offered the scores when the game ends.</param>
    public static PlayingState CreateNewGame(ControlSettings controls, GameSettings settings, GameMode mode, SpriteSet sprites, HighScoreStore highScoreStore)
    {
        var playerOne = new BoundPlayerInputSource(controls, 0);
        var playerTwo = new BoundPlayerInputSource(controls, 1);
        Sound.Play(StartSoundFor(mode));
        return new PlayingState(sprites, highScoreStore, GameSession.CreateNewGame(mode, playerOne, playerTwo, controls, settings), isStartOfTurn: true);
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        // While the arcade shows whose turn it is, the wave has not been set up: only the wall and the scores are on the screen.
        if (IsAnnouncingTurn())
        {
            _field.DrawWall(spriteBatch);
        }
        else
        {
            _field.Draw(spriteBatch);
        }

        ArcadeHud.DrawScoresAndMen(spriteBatch, _sprites, _session, PlayfieldLayout.GetInnerBounds());
        ArcadeHud.DrawWaveMessage(spriteBatch, _sprites, _session.Current.Wave);

        if (_pause.IsPaused)
        {
            // Port-only banner, in the wave's own message colour so it reads as part of
            // the cabinet's vocabulary rather than a debug overlay.
            ArcadeHud.DrawMessageText(
                spriteBatch,
                _sprites,
                "PAUSED",
                HudLayout.PausedMessageColumn,
                HudLayout.PausedMessageRow,
                WavePaletteTables.GetElectrodeSlot(_session.Current.Wave));
        }

        if (_playerOutMessageTicks > 0)
        {
            // ROM string 75: "PLAYER n" at $3F79 then "GAME OVER" at $3E86.
            int messageSlot = WavePaletteTables.GetElectrodeSlot(_session.Current.Wave);
            ArcadeHud.DrawMessageText(spriteBatch, _sprites, $"PLAYER {_playerOutNumber}", HudLayout.PlayerTurnMessageColumn, HudLayout.PlayerGameOverMessageRow, messageSlot);
            ArcadeHud.DrawMessageText(spriteBatch, _sprites, "GAME OVER", HudLayout.GameOverMessageColumn, HudLayout.GameOverMessageRow, messageSlot);
        }
        else if (IsAnnouncingTurn())
        {
            // ROM string 103: "PLAYER n" at $3F7A, in the wave's electrode colour
            // (PLS0D: LDA PSTCOL / STA TEXCOL), for NAP 115.
            ArcadeHud.DrawMessageText(
                spriteBatch,
                _sprites,
                $"PLAYER {_session.Current.Number}",
                HudLayout.PlayerTurnMessageColumn,
                HudLayout.PlayerTurnMessageRow,
                WavePaletteTables.GetElectrodeSlot(_session.Current.Wave));
        }
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        // The port's PAUSE (notes §101) — port-only, the arcade has none. The field is
        // frozen but the toggle still polls, or the key that paused could never unpause.
        InputSnapshot snapshot = InputSnapshot.Read();
        _pause.Tick(_session.ControlSettings.IsPauseHeld(snapshot.Keys, snapshot.PadOne, snapshot.PadTwo));
        if (_pause.IsPaused)
        {
            return;
        }

        // "PLAYER n GAME OVER" is a WAIT in the ROM (NAP $60): the field behind it
        // is frozen and the next player's wave has not been built yet.
        if (_playerOutMessageTicks > 0)
        {
            _playerOutMessageTicks--;
            return;
        }

        // "PLAYER n" is a wait too, and it comes before the wave is set up (ROM: NAP 115,PLS0B, then PLS0A), so the field does not start until it is over.
        if (IsAnnouncingTurn())
        {
            _turnMessageTicks--;
            return;
        }

        _field.Update(gameTime);

        // The HUD is drawn from the session's slots, and the ROM reads the score,
        // the men and SAVCNT out of the player's own data block every time it
        // draws — so hand the live counters over EVERY tick (notes §97). Syncing
        // only at a wave clear / death left the displayed score (and an earned
        // spare man) stale for the rest of the wave.
        SyncSlotFromField();

        PlayerInputState input = _session.Current.Input.Poll();

        // Wave clear — checked before the death check. The P key (the port's test
        // key) takes the same path so waves can be skipped.
        if (_field.IsLevelCleared() || input.SkipLevelHeld)
        {
            HandleWaveCleared(manager);
            return;
        }

        // Death animation finished this frame (one-shot via _restartHandled).
        if (_field.IsPlayerDead() && !_restartHandled)
        {
            _restartHandled = true;
            HandlePlayerDeath(manager);
        }
    }

    /// <summary>Gets the wave a player is about to play: what was left after their last death, or a fresh wave.</summary>
    /// <param name="slot">The player whose turn it is.</param>
    /// <remarks>
    /// Original source: <c>RRG23.ASM</c> <c>PLRES</c> brings back what <c>PLSAV</c> kept, then eases the first waves for a player on
    /// their last men (Bozo). The difficulty setting is applied once, when the wave is first made (<c>GETWV</c>), so a
    /// life after a death does not apply it again.
    /// </remarks>
    private LevelParameters GetWaveToPlay(PlayerSlot slot)
    {
        if (slot.SavedWaveParameters is { } saved)
        {
            return ApplyBozoMode(saved, slot);
        }

        // Bozo mercy first, then the difficulty adjustment — the ROM's own order ($2B26 before $2B7C).
        LevelParameters parameters = ApplyBozoMode(_generator.Generate(slot.Wave), slot);
        return DifficultyTuning.Apply(parameters, _gameSettings.Difficulty, slot.Lives);
    }

    /// <summary>Eases the wave for a player losing ships early, unless BOZO MODE is switched off on the GAME ADJUSTMENT page.</summary>
    /// <param name="parameters">The wave's parameters.</param>
    /// <param name="slot">The player whose turn it is.</param>
    private LevelParameters ApplyBozoMode(LevelParameters parameters, PlayerSlot slot) =>
        _gameSettings.BozoModeEnabled ? BozoMode.Apply(parameters, slot.SpareMen, _gameSettings.TurnsPerPlayer) : parameters;

    /// <summary>The start sound for a mode: <c>ST1SND</c> for one player, <c>ST2SND</c> for two (RRG23 <c>SST01</c>, from <c>PLRCNT</c>).</summary>
    private static SoundSequence StartSoundFor(GameMode mode) =>
        mode == GameMode.OnePlayer ? SoundTables.StartOnePlayer : SoundTables.StartTwoPlayers;

    /// <summary>Says whether the "PLAYER n" message is on the screen. The wave is not set up until it has gone.</summary>
    internal bool IsAnnouncingTurn() => _turnMessageTicks > 0;

    /// <summary>
    /// Starts the "PLAYER n" message at the start of a turn in a two-player game. The arcade prints it in the middle of the screen and waits before it rubs it out and sets the wave up.
    /// A one-player game has no such message.
    /// </summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>PLS0D</c>, <c>LDA PLRCNT / DECA / BEQ PLS0A</c> ("1 PLAYER GAME"), then <c>NAP 115,PLS0B</c> ("PLAYER UP MESSAGE"). Disassembly: <c>$2803</c> to <c>$281A</c>.</remarks>
    private void AnnounceTurn()
    {
        _turnMessageTicks = _session.IsTwoPlayer
            ? ArcadeClock.ToPortTicks(ScreenTuning.PlayerTurnMessageRomFrames)
            : 0;
    }

    /// <summary>Builds the playfield for whoever's turn it is, from their own state.</summary>
    private PlayField BuildField()
    {
        PlayerSlot slot = _session.Current;

        LevelParameters parameters = GetWaveToPlay(slot);

        WallColorCycle cycle = new();
        return new PlayField(
            _sprites,
            parameters,
            slot.Input,
            PlayfieldLayout.GetInnerBounds(),
            cycle,
            _random,
            slot.Lives,
            slot.Score,
            _sprites.Blitter.Palette,
            contactTest: new PixelContactTest(new SpriteCollision()),
            extraManEveryPoints: _gameSettings.ExtraManEveryPoints,
            tankShellBugEnabled: _gameSettings.TankShellBugEnabled,
            brainsChaseMikeyBugEnabled: _gameSettings.BrainsChaseMikeyBugEnabled);
    }

    /// <summary>
    /// ROM PLEND → PLEND3 → PLE1B. <see cref="Player.Kill"/> has already taken the
    /// man (the ROM's <c>PLSTRT</c> takes it when a life starts), so
    /// <see cref="PlayField.Player"/> already holds the remaining count.
    /// </summary>
    private void HandlePlayerDeath(GameStateManager manager)
    {
        PlayerSlot deadPlayerSlot = _session.Current;
        SyncSlotFromField();
        deadPlayerSlot.SavedWaveParameters = WaveSurvivors.GetFrom(_field);

        if (_session.IsTwoPlayer)
        {
            // ROM PLE1B: the turn passes to the other player while they have men.
            _session.SwitchToPlayerWithMen();
        }

        if (!_session.AnyPlayerSlotHasMen())
        {
            // The score that ends the game is the CURRENT player's — but a 2-player
            // game offers both scores to the high-score table (RRTESTC checks
            // ZP1SCR and ZP2SCR), so the session hands over all of them.
            manager.TransitionTo(GameOverState.CreateFromSession(_session.Current.Input, _sprites, _highScoreStore, _session));
            return;
        }

        if (!deadPlayerSlot.HasMen && _session.IsTwoPlayer)
        {
            // ROM PLEND3: this player is out and the other still has men — print
            // "PLAYER n GAME OVER" and wait NAP $60 before the turn passes.
            _playerOutNumber = deadPlayerSlot.Number;
            _playerOutMessageTicks = ArcadeClock.ToPortTicks(ScreenTuning.PlayerGameOverMessageRomFrames);
        }

        _field = BuildField();
        _restartHandled = false;
        AnnounceTurn();
    }

    /// <summary>
    /// ROM GEXEC0: the wave is cleared for whoever is playing — only THEIR wave
    /// counter advances (<c>INC PWAV,X</c>, skipping 0) and only their state
    /// carries over.
    /// </summary>
    private void HandleWaveCleared(GameStateManager manager)
    {
        PlayerSlot slot = _session.Current;
        SyncSlotFromField();

        // ROM GEXX/GEXX1: INC PWAV,X / BNE / INC PWAV,X — a byte counter that skips 0.
        slot.Wave = (slot.Wave % WaveCounterWrap) + 1;
        slot.SavedWaveParameters = null;

        manager.TransitionTo(new WaveClearState(_sprites, _highScoreStore, _session));
    }

    /// <summary>Copies the live field's counters back into the current player's slot.</summary>
    private void SyncSlotFromField() => _field.SyncInto(_session.Current);
}
