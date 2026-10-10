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
///         <item>a death rebuilds the field for the next man;</item>
///         <item>
///             losing ALL men silently starts a NEW game — never a high-score entry
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
    private readonly ButtonEdgeDetector _buttons = new();
    private readonly DemoPlayerInputSource _demoInput = new();
    private readonly LevelParameterGenerator _generator = new();
    private readonly HighScoreStore _highScoreStore;
    private readonly IPlayerInputSource _humanInput;
    private readonly Random _random = new();
    private readonly GameServices _services;
    private readonly SpriteSet _sprites;
    private PlayField _field;
    private readonly GameSession _session;

    public AttractState(GameServices services)
    {
        _services = services;
        _sprites = services.Sprites;
        _highScoreStore = services.HighScoreStore;
        _humanInput = services.Input;
        _session = GameSession.CreateNewGame(_demoInput, 1);
        _field = BuildField();
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        _field.Draw(spriteBatch);
        ArcadeHud.DrawScoresAndMen(spriteBatch, _sprites, _session, PlayfieldLayout.GetInnerBounds(), false);
        ArcadeHud.DrawWaveMessage(spriteBatch, _sprites, _session.GetCurrent().Wave);
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        // A human at the coin door takes the machine back (arcade: start works
        // any time attract is running).
        var humanInputState = _humanInput.Poll();
        if (_buttons.Advance(humanInputState).AnyPressed())
        {
            manager.TransitionTo(new TitleScreenState(_services));
            return;
        }

        // The phony player reasons about the field BEFORE it moves this tick —
        // the same order PlayingState polls after its Update, except the AI needs
        // the pre-move sprite to decide its own move.
        _demoInput.Bind(_field);
        _field.Update(gameTime);

        // The demo's HUD is drawn from the session slot, so the live counters go
        // over every tick (notes §97) — a rescue's 1000-5000 points showed up in
        // the score only at the next wave clear / death before this.
        SyncSlotFromField();

        if (_field.IsLevelCleared())
        {
            var playerSlot = _session.GetCurrent();
            SyncSlotFromField();
            playerSlot.Wave = playerSlot.Wave % 255 + 1; // ROM GEXX: skip 0
            playerSlot.SavedWaveParameters = null;
            manager.TransitionTo(new WaveClearState(_sprites, _highScoreStore, _session, true));
            return;
        }

        if (_field.IsPlayerDead())
        {
            SyncSlotFromField();
            _session.GetCurrent().SavedWaveParameters = WaveSurvivors.GetFrom(_field);

            if (!_session.AnyPlayerSlotHasMen())
            {
                // RRG23 PLEND: the last man gone ends the game — "GAME OVER", then
                // ENDPRC → GOV → LOGG1, whose first act is the high score TABLE
                // (`JSR SCRCLR / JSR TABORG`), and only then the family page again
                // (notes §98.1). The demo's own score is NOT posted (notes §98.4,
                // open question).
                manager.TransitionTo(new HighScoreTableState(_services));
                return;
            }

            _field = BuildField();
        }
    }

    private PlayField BuildField()
    {
        var slot = _session.GetCurrent();
        // The demo is the same game, so a death keeps the survivors here too (notes §134).
        var parameters = BozoMode.Apply(slot.SavedWaveParameters ?? _generator.Generate(slot.Wave), slot.GetSpareMen());
        WallColorCycle cycle = new();
        return new PlayField(_sprites, parameters, _demoInput, PlayfieldLayout.GetInnerBounds(), cycle, _random,
            slot.Lives, slot.Score, _sprites.Blitter.Palette, false, new PixelContactTest(new SpriteCollision()));
    }

    private void SyncSlotFromField()
    {
        _field.SyncInto(_session.GetCurrent());
    }
}
