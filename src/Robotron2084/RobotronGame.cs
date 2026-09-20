using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Persistence;
using Robotron2084.Rendering;
using Robotron2084.States;

namespace Robotron2084;

/// <summary>
/// Application shell (Phase 10.6). The whole game is a ScreenSize.Width x
/// ScreenSize.Height image (the spec's 320x200 space widened by SpecScale)
/// rendered into a render target, then blitted to the window
/// at an integer scale — F11 cycles the scale, Escape exits. Owns the three
/// shared instances every state transition passes onward: SpriteSet,
/// CompositePlayerInputSource, and HighScoreStore (created once in
/// LoadContent).
/// </summary>
public sealed class RobotronGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private RenderTarget2D _playfield = null!;
    private SpriteBatch _spriteBatch = null!;
    private SpriteFont _font = null!;
    private SpriteSet _sprites = null!;
    private PaletteAnimator _paletteAnimator = null!;
    private IPlayerInputSource _input = null!;
    private HighScoreStore _highScoreStore = null!;
    private ControlSettings _controlSettings = null!;
    private ControlSettingsStore _controlSettingsStore = null!;
    private GameServices _services = null!;
    private GameStateManager _stateManager = null!;

    private KeyboardState _previousKeyboardState;
    private int _maxScale = 1;
    private int _scale = 1;

    public RobotronGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsFixedTimeStep = true; // fixed timestep drives Update(GameTime); gameplay uses per-tick integer math
    }

    protected override void Initialize()
    {
        Window.Title = "ScottOTron 2084";
        Window.AllowUserResizing = false;

        Point workArea = DisplayInfo.WorkArea;
        _maxScale = ScreenSize.MaxIntegerScale(workArea.X, workArea.Y);
        _scale = _maxScale;
        ApplyScale();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _playfield = new RenderTarget2D(GraphicsDevice, ScreenSize.Width, ScreenSize.Height);
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("Fonts/Default");

        // Arcade-fidelity colour engine (M4): 16-slot palette + the six ROM
        // colour processes replayed per tick, and the pixel shader that maps
        // sprite marker colours to the slots' live colours. The wall cycles on
        // palette slot 11 (RGB process); sprites cycle through the M4 shader
        // (Content/Effects/ColorCycle.fx — notes §3.3, §34).
        GamePalette palette = new();
        _paletteAnimator = new PaletteAnimator(palette);
        _sprites = new SpriteSet(GraphicsDevice, Content)
        {
            Palette = palette,
            ColorCycleEffect = Content.Load<Effect>("Effects/ColorCycle"),
        };
        // The port's control definitions (notes §101) are read from controls.ini at
        // startup and edited by the DEFINE INPUTS page; player 1's are what the menus
        // and the attract sequence read.
        _controlSettingsStore = new ControlSettingsStore();
        _controlSettings = _controlSettingsStore.Load();
        _input = new BoundPlayerInputSource(_controlSettings, playerIndex: 0);
        _highScoreStore = new HighScoreStore();
        _services = new GameServices(_sprites, _highScoreStore, _controlSettings, _input);
        _stateManager = new GameStateManager(new TitleScreenState(_services));

        // Arcade-faithful sound (notes §36.2): the single sound-board voice as
        // a priority sequencer, ticked once per port tick. NOTE the
        // note→frequency map is sound-board hardware not in the CPU ROM —
        // the sink currently plays a stub scale (BLOCKED on the author).
        Sound.Initialize(new MonoGameSoundSink());
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState state = Keyboard.GetState();

        if (state.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        // F11 press (not held): cycle integer scale up, wrapping to 1x.
        if (_previousKeyboardState.IsKeyDown(Keys.F11) && !state.IsKeyDown(Keys.F11))
        {
            _scale = _scale == _maxScale ? 1 : _scale + 1;
            ApplyScale();
        }

        // ---- attract DEV KEYS (port-only; notes §97, re-keyed in §101) ---------
        // F5 and F6 drop straight into the attract sequence — F5 the storyline movie
        // (the family and the hulk), F6 the phony-player demo game — so a scene can be
        // inspected without sitting out the title's 12-second idle. F7 HELD fast-forwards
        // the movie, which is how the hulk's walk (ROM frame ~2574) is reached in seconds
        // rather than after the text crawl. They used to be F1/F2/F3, which are now the
        // author's game-start keys (below).
        DevKeys.AttractFastForward = state.IsKeyDown(Keys.F7);
        if (Pressed(state, Keys.F5))
        {
            _stateManager.TransitionTo(new StorylineState(_services, new Random()));
        }
        else if (Pressed(state, Keys.F6))
        {
            _stateManager.TransitionTo(new AttractState(_services));
        }
        else if (Pressed(state, Keys.F4))
        {
            _stateManager.TransitionTo(new HighScoreTableState(_services));
        }

        // ---- the author's start keys, live on EVERY attract screen (notes §101) ---
        // F1 one player, F2 two players alternating turns, F3 the arcade's two-player
        // game (selected now, played later), F10 the DEFINE INPUTS page. Handling them
        // here rather than in the title means the attract movie, the demo and the high
        // score table can all be interrupted by a real player sitting down.
        if (_stateManager.Current is IAttractState)
        {
            GameMode? mode = Pressed(state, Keys.F1) ? GameMode.OnePlayer
                : Pressed(state, Keys.F2) ? GameMode.TwoPlayerAlternate
                : Pressed(state, Keys.F3) ? GameMode.TwoPlayerSimultaneous
                : null;

            if (mode is { } chosen)
            {
                _stateManager.TransitionTo(PlayingState.StartNewGame(_controlSettings, chosen, _sprites, _highScoreStore));
            }
            else if (Pressed(state, Keys.F10))
            {
                _stateManager.TransitionTo(new DefineInputsState(_services, _controlSettingsStore));
            }
        }

        _previousKeyboardState = state;

        _paletteAnimator.Update();
        _stateManager.Update(gameTime);
        Sound.Tick();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // 1. Render the ScreenSize.Width x ScreenSize.Height scene (states draw onto the already-cleared target).
        GraphicsDevice.SetRenderTarget(_playfield);
        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp);
        _stateManager.Draw(_spriteBatch, _font);
        _spriteBatch.End();

        // 2. Blit the scene to the window (integer-scaled, point-sampled).
        // The render target is ScreenSize.Width x ScreenSize.Height; the
        // backbuffer is that times _scale, so the blit must draw it into the
        // full backbuffer (a 1:1 draw leaves the scene in the top-left
        // 1/_scale corner — the "fraction of the window" playtest bug).
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp);
        _spriteBatch.Draw(
            _playfield,
            new Rectangle(0, 0, ScreenSize.Width * _scale, ScreenSize.Height * _scale),
            Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void ApplyScale()
    {
        // Resizing the backbuffer resizes the (non-resizable) window to match.
        _graphics.PreferredBackBufferWidth = ScreenSize.Width * _scale;
        _graphics.PreferredBackBufferHeight = ScreenSize.Height * _scale;
        _graphics.ApplyChanges();
    }

    /// <summary>True on the tick <paramref name="key"/> goes down (press, not hold).</summary>
    private bool Pressed(KeyboardState current, Keys key) =>
        !_previousKeyboardState.IsKeyDown(key) && current.IsKeyDown(key);
}
