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
/// Phase 12.1 (notes §94): the arcade's FANCY ATTRACT MODE — the machine plays
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
public sealed class AttractState : IGameState
{
    private static readonly int Margin = ScreenSize.Scaled(GameplayConstants.PlayfieldMarginSpecPixels);
    private static readonly Rectangle InnerBounds = new(Margin, Margin, ScreenSize.Width - 2 * Margin, ScreenSize.Height - 2 * Margin);

    private readonly SpriteSet _sprites;
    private readonly HighScoreStore _highScores;
    private readonly IPlayerInputSource _humanInput;
    private readonly DemoPlayerInputSource _demoInput = new();
    private readonly LevelParameterGenerator _generator = new();
    private readonly Random _random = new();
    private GameSession _session;
    private PlayField _field;
    private bool _previousFire;
    private bool _previousStartOne;
    private bool _previousStartTwo;

    public AttractState(SpriteSet sprites, HighScoreStore highScores, IPlayerInputSource humanInput)
    {
        _sprites = sprites;
        _highScores = highScores;
        _humanInput = humanInput;
        _session = GameSession.NewGame(_demoInput, 1);
        _field = BuildField();
    }

    private PlayField BuildField()
    {
        PlayerSlot slot = _session.Current;
        LevelParameters parameters = _generator.Generate(slot.Wave);
        WallColorCycle cycle = new(GameplayConstants.DefaultWallPalette, TimeSpan.FromMilliseconds(GameplayConstants.WallStepDurationMilliseconds));
        return new PlayField(parameters, _demoInput, InnerBounds, cycle, _random, slot.Lives, slot.Score, slot.Rescues, _sprites.Palette, playerInvincibleForTesting: false);
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        // A human at the coin door takes the machine back (arcade: start works
        // any time attract is running).
        PlayerInputState human = _humanInput.Poll();
        if ((human.FirePressed && !_previousFire) ||
            (human.StartOnePlayerPressed && !_previousStartOne) ||
            (human.StartTwoPlayersPressed && !_previousStartTwo))
        {
            manager.TransitionTo(new TitleScreenState(_humanInput, _sprites, _highScores));
            return;
        }
        _previousFire = human.FirePressed;
        _previousStartOne = human.StartOnePlayerPressed;
        _previousStartTwo = human.StartTwoPlayersPressed;

        // The phony player reasons about the field BEFORE it moves this tick —
        // the same order PlayingState polls after its Update, except the AI needs
        // the pre-move picture to decide its own move.
        _demoInput.Bind(_field);
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
            manager.TransitionTo(new WaveClearState(_sprites, _highScores, _session, slot.Wave - 1, attract: true));
            return;
        }

        if (_field.Player.LifeState == EntityLifeState.Dead)
        {
            SyncSlotFromField();
            _session.Current.Rescues = 0; // ROM PLINIT clears SAVCNT

            if (!_session.AnyMenLeft)
            {
                // The OS ROM's behaviour: the demo just starts over, silently.
                _session = GameSession.NewGame(_demoInput, 1);
            }

            _field = BuildField();
        }
    }

    private void SyncSlotFromField() => _field.SyncInto(_session.Current);

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        _field.Draw(spriteBatch, _sprites);
        ArcadeHud.DrawScoresAndMen(spriteBatch, _sprites, _session, InnerBounds);
        ArcadeHud.DrawWaveMessage(spriteBatch, _sprites, _session.Current.Wave);
    }
}
