using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Graphics;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Level.Collisions;
using Robotron2084.Palette;
using Robotron2084.Persistence;

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
    private readonly DemoPlayerInputSource _demoPlayerInputSource = new();
    private readonly WaveParameterGenerator _waveParameterGenerator = new();
    private readonly HighScoreStore _highScoreStore;
    private readonly IPlayerInputSource _humanPlayerInputSource;
    private readonly Random _random = new();
    private readonly GameServices _gameServices;
    private readonly GameSession _gameSession;
    private readonly SpriteSet _spriteSet;
    private PlayField _playField;

    public AttractState(GameServices gameServices)
    {
        _gameServices = gameServices;
        _spriteSet = gameServices.SpriteSet;
        _highScoreStore = gameServices.HighScoreStore;
        _humanPlayerInputSource = gameServices.PlayerInputSource;
        _gameSession = GameSession.CreateNewGame(_demoPlayerInputSource, 1);
        _playField = BuildField();
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        _playField.Draw(spriteBatch);
        ArcadeHud.DrawScoresAndLives(spriteBatch, _spriteSet, _gameSession, PlayfieldLayout.GetInnerBounds(), false);
        ArcadeHud.DrawWaveMessage(spriteBatch, _spriteSet, _gameSession.GetCurrent().Wave);
    }

    public void Update(GameTime gameTime, GameStateManager gameStateManager)
    {
        // A human at the coin door takes the machine back (arcade: start works
        // any time attract is running).
        var humanInputState = _humanPlayerInputSource.Poll();
        if (_buttonEdgeDetector.Advance(humanInputState).AnyPressed())
        {
            gameStateManager.TransitionTo(new TitleScreenState(_gameServices));
            return;
        }

        // The phony player reasons about the field BEFORE it moves this tick —
        // the same order PlayingState polls after its Update, except the AI needs
        // the pre-move sprite to decide its own move.
        _demoPlayerInputSource.Bind(_playField);
        _playField.Update(gameTime);

        // The demo's HUD is drawn from the session slot, so the live counters go
        // over every tick (notes §97) — a rescue's 1000-5000 points showed up in
        // the score only at the next wave clear / death before this.
        SyncSlotFromField();

        if (_playField.IsWaveCleared())
        {
            var playerSlot = _gameSession.GetCurrent();
            SyncSlotFromField();
            playerSlot.Wave = playerSlot.Wave % 255 + 1; // ROM GEXX: skip 0
            playerSlot.SavedWaveParameters = null;
            gameStateManager.TransitionTo(new WaveClearState(_spriteSet, _highScoreStore, _gameSession, true));
            return;
        }

        if (_playField.IsPlayerDead())
        {
            SyncSlotFromField();
            _gameSession.GetCurrent().SavedWaveParameters = WaveSurvivors.GetFrom(_playField);

            if (!_gameSession.AnyPlayerSlotHasLives())
            {
                // RRG23 PLEND: the last life gone ends the game — "GAME OVER", then
                // ENDPRC → GOV → LOGG1, whose first act is the high score TABLE
                // (`JSR SCRCLR / JSR TABORG`), and only then the family page again
                // (notes §98.1). The demo's own score is NOT posted (notes §98.4,
                // open question).
                gameStateManager.TransitionTo(new HighScoreTableState(_gameServices));
                return;
            }

            _playField = BuildField();
        }
    }

    private PlayField BuildField()
    {
        var slot = _gameSession.GetCurrent();
        // The demo is the same game, so a death keeps the survivors here too (notes §134).
        var waveParameters = BozoMode.Apply(slot.SavedWaveParameters ?? _waveParameterGenerator.Generate(slot.Wave), slot.GetSpareLives());
        WallColorCycle wallColorCycle = new();
        return new PlayField(_spriteSet, waveParameters, _demoPlayerInputSource, PlayfieldLayout.GetInnerBounds(), wallColorCycle, _random,
            slot.Lives, slot.Score, _spriteSet.BlitterDraw.GamePalette, false, new PixelContactTest(new SpriteCollision()));
    }

    private void SyncSlotFromField()
    {
        _playField.SyncInto(_gameSession.GetCurrent());
    }
}
