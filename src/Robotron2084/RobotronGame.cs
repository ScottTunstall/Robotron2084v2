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

/// <summary>The application shell: it owns the window and the fixed-timestep loop and runs one game state at a time.</summary>
/// <remarks>Port-only (no arcade counterpart). Every state draws into a <see cref="ScreenSize.Width"/> x
/// <see cref="ScreenSize.Height"/> canvas, which is blitted to the window at ONE uniform scale and centred
/// there, so the picture is never stretched and the window may be any size. <b>F8</b> cycles the canvas fit
/// (Integer = the largest whole multiple, Fill = the exact fraction), <b>F11</b> and <b>Alt+Enter</b> toggle
/// full screen, <b>Escape</b> exits. It also owns the shared instances every state passes onward:
/// <see cref="SpriteSet"/>, the input source and the high score store, all created once in LoadContent.</remarks>
/// <seealso cref="Presentation"/>
/// <seealso cref="ScreenSize"/>
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
    private ScaleMode _scaleMode = ScaleMode.Integer;
    private bool _fullScreen;
    private int _windowedScale = 1;
    private Rectangle _canvas = new(0, 0, ScreenSize.Width, ScreenSize.Height);

    public RobotronGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsFixedTimeStep = true; // fixed timestep drives Update(GameTime); gameplay uses per-tick integer math
    }

    protected override void Initialize()
    {
        Window.Title = "ScottOTron 2084";
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += (_, _) => FitCanvas();

        // The windowed window is the canvas at the largest whole multiple that fits the
        // desktop; the author can drag it to any size afterwards and the fit follows.
        Point workArea = DisplayInfo.WorkArea;
        _windowedScale = ScreenSize.MaxIntegerScale(workArea.X, workArea.Y);
        ApplyBackBuffer(ScreenSize.Width * _windowedScale, ScreenSize.Height * _windowedScale, fullScreen: false);

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

        // ---- presentation keys (port-only) ----------------------------------------
        // F11 is the Windows convention for full screen and Alt+Enter is the game
        // convention; F8 cycles the canvas fit between the largest whole multiple
        // (crisp, the default) and the exact uniform fraction (fills the window).
        bool altHeld = state.IsKeyDown(Keys.LeftAlt) || state.IsKeyDown(Keys.RightAlt);
        if (Pressed(state, Keys.F11) || (altHeld && Pressed(state, Keys.Enter)))
        {
            ToggleFullScreen();
        }
        else if (Pressed(state, Keys.F8))
        {
            _scaleMode = Presentation.Next(_scaleMode);
            FitCanvas();
        }

        // ---- attract DEV KEYS (port-only; notes §97, re-keyed in §101) ---------
        // F5 and F6 drop straight into the attract sequence — F5 the storyline movie
        // (the family and the hulk), F6 the phony-player demo game — so a scene can be
        // inspected without sitting out the title's 12-second idle. F7 HELD fast-forwards
        // the movie, which is how the hulk's walk (ROM frame ~2574) is reached in seconds
        // rather than after the text crawl. F4 jumps to the high score table and F9 to the
        // end of a game, whose score ceremony (notes §116) is otherwise a whole game away.
        // They used to be F1/F2/F3, which are now the author's game-start keys (below).
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
        else if (Pressed(state, Keys.F9))
        {
            // The end of a game — GAME OVER, the CONG initials screen and the table (notes §116) —
            // is a whole game away otherwise, so F9 carries a score that has to qualify into it.
            GameSession session = GameSession.NewGame(GameMode.OnePlayer, _input, controls: _controlSettings);
            session.Current.Score = DevKeys.QualifyingScore;
            _stateManager.TransitionTo(GameOverState.FromSession(_input, _sprites, _highScoreStore, session));
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

        // 2. Blit the scene to the window at the canvas's fit: uniform, centred, the bars
        // black. The client area can change at any moment (a dragged window, a monitor
        // switch), so the fit is re-solved here — it is pure arithmetic.
        FitCanvas();
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp);
        _spriteBatch.Draw(_playfield, _canvas, Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    /// <summary>Sizes the backbuffer, and with it the window, and switches the screen mode with it.</summary>
    private void ApplyBackBuffer(int width, int height, bool fullScreen)
    {
        _fullScreen = fullScreen;
        _graphics.PreferredBackBufferWidth = width;
        _graphics.PreferredBackBufferHeight = height;
        _graphics.IsFullScreen = fullScreen;
        _graphics.ApplyChanges();
        FitCanvas();
    }

    /// <summary>Switches between the windowed window and borderless full screen at the desktop's own mode.</summary>
    private void ToggleFullScreen()
    {
        if (_fullScreen)
        {
            ApplyBackBuffer(ScreenSize.Width * _windowedScale, ScreenSize.Height * _windowedScale, fullScreen: false);
            return;
        }

        Point desktop = DisplayInfo.DesktopResolution;
        ApplyBackBuffer(desktop.X, desktop.Y, fullScreen: true);
    }

    /// <summary>Re-solves where the canvas sits in the window's current client area.</summary>
    private void FitCanvas() =>
        _canvas = Presentation.CanvasDestination(Window.ClientBounds.Width, Window.ClientBounds.Height, _scaleMode);

    /// <summary>True on the tick <paramref name="key"/> goes down (press, not hold).</summary>
    private bool Pressed(KeyboardState current, Keys key) =>
        !_previousKeyboardState.IsKeyDown(key) && current.IsKeyDown(key);
}
