using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Entities;
using Robotron2084.Graphics;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Palette;
using Robotron2084.Persistence;

namespace Robotron2084.States;

/// <summary>
/// The arcade's FANCY ATTRACT MODE (notes §94; the listing gives this game no page label, §123) — the machine plays
/// itself. A one-player game driven by <see cref="DemoPlayerInputSource"/>
/// (the port's phony player; the ROM's real one is the OS ROM's ATRSW2 writer).
/// The field, waves and death handling are the real <see cref="PlayField"/> /
/// <see cref="GameSession"/> machinery — the demo is a game, not a cutscene.
///
/// The arcade's attract rules, kept:
/// <list type="bullet">
/// <item>wave clears advance the wave, through the same tunnel state;</item>
/// <item>a death rebuilds the field for the next man;</item>
/// <item>losing ALL men silently starts a NEW game — never a high-score entry
/// (the OS ROM just replays the demo);</item>
/// <item>any human START/FIRE press breaks back to the title screen (the arcade
/// accepts coin/start any time attract is running).</item>
/// </list>
/// </summary>
public sealed class AttractState : IGameState, IAttractState
{
    private readonly ButtonEdgeDetector _buttons = new();
    private readonly DemoPlayerInputSource _demoInput = new();
    private readonly LevelParameterGenerator _generator = new();
    private readonly HighScoreStore _highScores;
    private readonly IPlayerInputSource _humanInput;
    private readonly Random _random = new();
    private readonly GameServices _services;
    private readonly SpriteSet _sprites;
    private PlayField _field;
    private GameSession _session;
    private WaveCompleteEffect? _waveComplete;

    /// <summary>Starts the phony-player demo.
    /// </summary>
    /// <param name="services">The shared game services.</param>
    /// <param name="waveComplete">The wave-complete marquee a cleared wave handed over, or null.</param>
    public AttractState(GameServices services, WaveCompleteEffect? waveComplete = null)
    {
        _services = services;
        _sprites = services.Sprites;
        _highScores = services.HighScores;
        _humanInput = services.Input;
        _session = GameSession.NewGame(_demoInput, 1);
        _waveComplete = waveComplete;
        _field = BuildField();
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        _field.Draw(spriteBatch);
        _waveComplete?.Draw(spriteBatch, _sprites);
        ArcadeHud.DrawScoresAndMen(spriteBatch, _sprites, _session, PlayfieldLayout.InnerBounds, showSpareMen: false);
        ArcadeHud.DrawWaveMessage(spriteBatch, _sprites, _session.Current.Wave);
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        // A human at the coin door takes the machine back (arcade: start works
        // any time attract is running).
        PlayerInputState human = _humanInput.Poll();
        if (_buttons.Advance(human).Any)
        {
            manager.TransitionTo(new TitleScreenState(_services));
            return;
        }

        // The phony player reasons about the field BEFORE it moves this tick —
        // the same order PlayingState polls after its Update, except the AI needs
        // the pre-move picture to decide its own move.
        _demoInput.Bind(_field);
        AdvanceWaveComplete();
        _field.Update(gameTime);

        // The demo's HUD is drawn from the session slot, so the live counters go
        // over every tick (notes §97) — a rescue's 1000-5000 points showed up in
        // the score only at the next wave clear / death before this.
        SyncSlotFromField();

        if (_field.IsLevelCleared)
        {
            PlayerSlot slot = _session.Current;
            SyncSlotFromField();
            slot.Wave = (slot.Wave % 255) + 1; // ROM GEXX: skip 0
            manager.TransitionTo(new WaveClearState(_sprites, _highScores, _session, attract: true));
            return;
        }

        if (_field.Player.LifeState == EntityLifeState.Dead)
        {
            SyncSlotFromField();
            _session.Current.Rescues = 0; // ROM PLINIT clears SAVCNT

            if (!_session.AnyMenLeft)
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

    /// <summary>
    /// Finishes the wave-complete marquee the wave clear handed over (notes §128), exactly as
    /// <see cref="PlayingState"/> does: it erases itself over the demo's field and the palette goes
    /// back when the last ring is drawn.
    /// </summary>
    private void AdvanceWaveComplete()
    {
        if (_waveComplete is null)
        {
            return;
        }

        _waveComplete.Update();
        if (_waveComplete.Finished)
        {
            _waveComplete = null;
        }
    }

    private PlayField BuildField()
    {
        PlayerSlot slot = _session.Current;
        LevelParameters parameters = BozoMode.Apply(_generator.Generate(slot.Wave), slot.SpareMen);
        WallColorCycle cycle = new();
        return new PlayField(_sprites, parameters, _demoInput, PlayfieldLayout.InnerBounds, cycle, _random, slot.Lives, slot.Score, slot.Rescues, _sprites.Blitter.Palette, playerInvincibleForTesting: false, pixelCollision: new SpriteCollision());
    }

    private void SyncSlotFromField() => _field.SyncInto(_session.Current);
}
