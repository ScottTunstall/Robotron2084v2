using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Graphics;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Level.Models;
using Robotron2084.Level;
using Robotron2084.Level.Collisions;
using Robotron2084.Palette;
using Robotron2084.Persistence;
using Robotron2084.States.Models;

namespace Robotron2084.States;

/// <summary>
///     The arcade's FANCY ATTRACT MODE (notes §94; the listing gives this game no page label, §123) — the machine plays
///     itself. A one-player game driven by <see cref="DemoPlayerInputSource" />
///     (the port's phony player; the ROM's real one is the OS ROM's ATRSW2 writer).
///     The field, waves and death handling are the real <see cref="PlayField" /> /
///     <see cref="GameSession" /> machinery — the demo is a game, not a cutscene.
///     The arcade's attract rules, kept:
///     <list type="bullet">
///         <item>wave clears advance the wave, through the same tunnel state;</item>
///         <item>a death rebuilds the field for the next life;</item>
///         <item>
///             losing ALL lives silently starts a NEW game — never a high-score entry
///             (the OS ROM just replays the demo);
///         </item>
///         <item>
///             any human START/FIRE press breaks back to the title screen (the arcade
///             accepts coin/start any time attract is running).
///         </item>
///     </list>
/// </summary>
public sealed class AttractState : IGameState, IAttractState
{
    private readonly ButtonEdgeDetector _buttonEdgeDetector = new();
    private readonly DemoPlayerInputSource _demoPlayerInputSource;
    private readonly WaveParamsGenerator _waveParamsGenerator = new();
    private readonly HighScoreStore _highScoreStore;
    private readonly IPlayerInputSource _humanPlayerInputSource;
    private readonly Random _random;
    private readonly GameStateParams _gameStateParams;
    private readonly GameSession _gameSession;
    private readonly TextureSet _textureSet;
    private PlayField _playField;

    public AttractState(GameStateParams gameStateParams)
    {
        _gameStateParams = gameStateParams;
        _random = gameStateParams.Random;
        _demoPlayerInputSource = new DemoPlayerInputSource(_random);
        _textureSet = gameStateParams.TextureSet;
        _highScoreStore = gameStateParams.HighScoreStore;
        _humanPlayerInputSource = gameStateParams.PlayerInputSource;
        _gameSession = GameSession.CreateNewGame(_demoPlayerInputSource, 1);
        _playField = BuildField();
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        _playField.Draw(spriteBatch);
        ArcadeHud.DrawScoresAndLives(spriteBatch, _textureSet, _gameSession, PlayfieldLayout.GetInnerBounds(), false);
        ArcadeHud.DrawWaveMessage(spriteBatch, _textureSet, _gameSession.GetCurrent().Wave);
    }

    public void Update(GameTime gameTime, GameStateManager gameStateManager)
    {
        // A human at the coin door opens the menu page (arcade: start works
        // any time attract is running; port: the game is chosen on the menu).
        var humanInputState = _humanPlayerInputSource.Poll();
        if (_buttonEdgeDetector.Advance(humanInputState).AnyPressed())
        {
            gameStateManager.TransitionTo(new MainMenuState(_gameStateParams));
            return;
        }

        // The phony player reasons about the field BEFORE it moves this tick —
        // the same order PlayingState polls after its Update, except the AI needs
        // the pre-move sprite to decide its own move.
        _demoPlayerInputSource.Bind(_playField);
        _playField.Update(gameTime);

        // The demo's HUD is drawn from the session slot, whose score board and lives counter the field changes
        // directly (notes §97).

        if (_playField.IsWaveCleared())
        {
            var playerSlot = _gameSession.GetCurrent();
            playerSlot.Wave = playerSlot.Wave % 255 + 1; // ROM GEXX: skip 0
            playerSlot.SavedWaveParams = null;
            gameStateManager.TransitionTo(new WaveClearState(_textureSet, _highScoreStore, _gameSession, _random, true));
            return;
        }

        if (_playField.IsPlayerDead())
        {
            _gameSession.GetCurrent().SavedWaveParams = _playField.GetWaveParamsForNextLife();

            if (!_gameSession.AnyPlayerSlotHasLives())
            {
                // RRG23 PLEND: the last life gone ends the game — "GAME OVER", then
                // ENDPRC → GOV → LOGG1, whose first act is the high score TABLE
                // (`JSR SCRCLR / JSR TABORG`), and only then the family page again
                // (notes §98.1). The demo's own score is NOT posted (notes §98.4,
                // open question).
                gameStateManager.TransitionTo(new HighScoreTableState(_gameStateParams));
                return;
            }

            _playField = BuildField();
        }
    }

    private PlayField BuildField()
    {
        var slot = _gameSession.GetCurrent();
        // The demo is the same game, so a death keeps the survivors here too (notes §134). It is the arcade's demo, so it
        // plays the arcade's waves whatever the game style.
        var waveParams = BozoMode.Apply(slot.SavedWaveParams ?? _waveParamsGenerator.Generate(slot.Wave, GameStyle.Classic),
            slot.GetSpareLives());
        WallColourCycle wallColourCycle = new();
        return new PlayField(_textureSet, waveParams, _demoPlayerInputSource, PlayfieldLayout.GetInnerBounds(), wallColourCycle, _random,
            slot.LivesCounter, slot.ScoreBoard, _textureSet.BlitterDraw.GamePalette, false, new PixelContactTest(new SpriteCollision()));
    }
}
