using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Audio;
using Robotron2084.Audio.Synthesis;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Palette;
using Robotron2084.Persistence;
using Robotron2084.States;

namespace Robotron2084;

/// <summary>The application shell: it owns the window and the fixed-timestep loop and runs one game state at a time.</summary>
/// <remarks>Port-only (no arcade counterpart). Every state draws into a <see cref="ScreenSize.Width"/> x
/// <see cref="ScreenSize.Height"/> canvas, which is blitted to the window at ONE uniform scale and centred
/// there, so the game screen is never stretched and the window may be any size. <b>F8</b> cycles the canvas fit
/// (Integer = the largest whole multiple, Fill = the exact fraction), <b>F11</b> and <b>Alt+Enter</b> toggle
/// full screen, <b>Escape</b> exits. It also owns the shared instances every state passes onward:
/// <see cref="SpriteSet"/>, the input source and the high score store, all created once in LoadContent.</remarks>
/// <seealso cref="Presentation"/>
/// <seealso cref="ScreenSize"/>
public sealed class RobotronGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private Rectangle _canvasBounds = new(0, 0, ScreenSize.Width, ScreenSize.Height);
    private ControlSettings _controlSettings = null!;
    private ControlSettingsStore _controlSettingsStore = null!;
    private SpriteFont _font = null!;
    private bool _isFullScreen;
    private GameSettings _gameSettings = null!;
    private GameSettingsStore _gameSettingsStore = null!;
    private HighScoreStore _highScoreStore = null!;
    private IPlayerInputSource _input = null!;
    private PaletteAnimator _paletteAnimator = null!;
    private RenderTarget2D _renderTarget = null!;
    private KeyboardState _previousKeyboardState;
    private ScaleMode _scaleMode = ScaleMode.Integer;
    private GameServices _services = null!;
    private SpriteBatch _spriteBatch = null!;
    private SpriteSet _sprites = null!;
    private GameStateManager _stateManager = null!;
    private int _windowedScale = 1;

    public RobotronGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsFixedTimeStep = true; // fixed timestep drives Update(GameTime); gameplay uses per-tick integer math
    }

    protected override void Draw(GameTime gameTime)
    {
        // 1. Render the ScreenSize.Width x ScreenSize.Height scene (states draw onto the already-cleared target).
        GraphicsDevice.SetRenderTarget(_renderTarget);
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
        _spriteBatch.Draw(_renderTarget, _canvasBounds, Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    protected override void Initialize()
    {
        Window.Title = "ScottOTron 2084";
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += (_, _) => FitCanvas();

        // The windowed window is the canvas at the largest whole multiple that fits the
        // desktop; the window can be dragged to any size afterwards and the fit follows.
        Point workArea = DisplayInfo.GetWorkArea();
        _windowedScale = ScreenSize.ComputeMaxIntegerScale(workArea.X, workArea.Y);
        ApplyBackBuffer(ScreenSize.Width * _windowedScale, ScreenSize.Height * _windowedScale, isFullScreen: false);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _renderTarget = new RenderTarget2D(GraphicsDevice, ScreenSize.Width, ScreenSize.Height);
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("Fonts/Default");

        // Arcade-fidelity colour engine: 16-slot palette + the six ROM
        // colour processes replayed per tick, and the pixel shader that maps
        // sprite marker colours to the slots' live colours. The wall cycles on
        // palette slot 11 (RGB process); sprites cycle through the colour-cycle shader
        // (Content/Effects/ColorCycle.fx — notes §3.3, §34).
        GamePalette palette = new();
        _paletteAnimator = new PaletteAnimator(palette);
        _sprites = new SpriteSet(new ContentSpriteSource(GraphicsDevice, Content));
        _sprites.Blitter.Palette = palette;
        _sprites.Blitter.ColorCycleEffect = Content.Load<Effect>("Effects/ColorCycle");
        // The port's control definitions (notes §101) are read from controls.ini at
        // startup and edited by the DEFINE INPUTS page; player 1's are what the menus
        // and the attract sequence read.
        _controlSettingsStore = new ControlSettingsStore();
        _controlSettings = _controlSettingsStore.Load();

        // The GAME ADJUSTMENT settings (notes §131) are read from settings.ini at
        // startup and edited by the settings page (F5); the title hands them to the
        // next game, so a change is live from the next START.
        _gameSettingsStore = new GameSettingsStore();
        _gameSettings = _gameSettingsStore.Load();
        _input = new BoundPlayerInputSource(_controlSettings, playerIndex: 0);
        _highScoreStore = new HighScoreStore();
        _services = new GameServices(_sprites, _highScoreStore, _controlSettings, _input, _gameSettings);
        _stateManager = new GameStateManager(new TitleScreenState(_services));

        StartSound();
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState state = Keyboard.GetState();

        if (state.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        HandlePresentationKeys(state);
        HandleAttractDevKeys(state);
        HandleStartKeys(state);

        _previousKeyboardState = state;

        // The attract-cycle sound switch (notes §131): while the attract sequence is on screen and
        // ATTRACT MODE SOUND is off, the sound service ignores every request — a real game keeps its
        // sound.
        Sound.AttractMuted = _gameSettings.IsAttractSilent(_stateManager.Current is IAttractState);

        _paletteAnimator.Update();
        _stateManager.Update(gameTime);
        Sound.Tick();
        base.Update(gameTime);
    }

    /// <summary>
    /// Switches the sound board on (notes §130). Without a sound output the game runs silently.
    /// </summary>
    private static void StartSound()
    {
        try
        {
            Sound.Initialize(new SoundBoardAudioSink(new SoundBoard()));
        }
        catch (NoAudioHardwareException exception)
        {
            Debug.WriteLine($"No sound: {exception.Message}");
        }
    }

    /// <summary>Sizes the backbuffer, and with it the window, and switches the screen mode with it.</summary>
    private void ApplyBackBuffer(int width, int height, bool isFullScreen)
    {
        _isFullScreen = isFullScreen;
        _graphics.PreferredBackBufferWidth = width;
        _graphics.PreferredBackBufferHeight = height;
        _graphics.IsFullScreen = isFullScreen;
        _graphics.ApplyChanges();
        FitCanvas();
    }

    /// <summary>Re-solves where the canvas sits in the window's current client area.</summary>
    private void FitCanvas() =>
        _canvasBounds = Presentation.CanvasDestination(Window.ClientBounds.Width, Window.ClientBounds.Height, _scaleMode);

    /// <summary>
    /// The port-only attract dev keys (notes §97, re-keyed in §101, §131 and §137): End the storyline movie,
    /// Home the demo game, Insert the high score table, Delete the end of a game, and Page Up HELD to
    /// fast-forward the movie.
    /// </summary>
    /// <param name="state">This tick's keyboard.</param>
    /// <remarks>End and Home drop straight into the attract sequence so a scene can be inspected without
    /// sitting out the title's 12-second idle, and Page Up is how the hulk's walk (ROM frame ~2574) is reached
    /// in seconds rather than after the text crawl. They are live on the attract screens only: Insert is also
    /// the skip-a-wave key and Delete clears a line on the DEFINE INPUTS page, so they must not act anywhere else.
    /// The function keys are left to the game's own start and settings keys (F1/F2/F3, F5, F10) and the display
    /// keys (F8, F11).</remarks>
    private void HandleAttractDevKeys(KeyboardState state)
    {
        bool onAttractScreen = _stateManager.Current is IAttractState;
        DevKeys.AttractFastForward = onAttractScreen && state.IsKeyDown(Keys.PageUp);
        if (!onAttractScreen)
        {
            return;
        }

        if (WasPressed(state, Keys.End))
        {
            _stateManager.TransitionTo(new StorylineState(_services, new Random()));
        }
        else if (WasPressed(state, Keys.Home))
        {
            _stateManager.TransitionTo(new AttractState(_services));
        }
        else if (WasPressed(state, Keys.Insert))
        {
            _stateManager.TransitionTo(new HighScoreTableState(_services));
        }
        else if (WasPressed(state, Keys.Delete))
        {
            StartEndOfGameFlow();
        }
    }

    /// <summary>The port-only presentation keys: full screen, and the canvas fit.</summary>
    /// <param name="state">This tick's keyboard.</param>
    private void HandlePresentationKeys(KeyboardState state)
    {
        // F11 is the Windows convention for full screen and Alt+Enter is the game
        // convention; F8 cycles the canvas fit between the largest whole multiple
        // (crisp, the default) and the exact uniform fraction (fills the window).
        bool altHeld = state.IsKeyDown(Keys.LeftAlt) || state.IsKeyDown(Keys.RightAlt);
        if (WasPressed(state, Keys.F11) || (altHeld && WasPressed(state, Keys.Enter)))
        {
            ToggleFullScreen();
        }
        else if (WasPressed(state, Keys.F8))
        {
            _scaleMode = Presentation.NextScaleMode(_scaleMode);
            FitCanvas();
        }
    }

    /// <summary>
    /// The game's start keys, live on EVERY attract screen (notes §101): F1 one player, F2 two players
    /// alternating turns, F3 the arcade's two-player game (selected now, played later), F5 the GAME
    /// ADJUSTMENT page (notes §131), F10 the DEFINE INPUTS page.
    /// </summary>
    /// <param name="state">This tick's keyboard.</param>
    /// <remarks>Handling them here rather than in the title means the attract movie, the demo and the high
    /// score table can all be interrupted by a real player sitting down — or by the operator opening
    /// the settings.</remarks>
    private void HandleStartKeys(KeyboardState state)
    {
        if (_stateManager.Current is not IAttractState)
        {
            return;
        }

        GameMode? mode = WasPressed(state, Keys.F1) ? GameMode.OnePlayer
            : WasPressed(state, Keys.F2) ? GameMode.TwoPlayerAlternate
            : WasPressed(state, Keys.F3) ? GameMode.TwoPlayerSimultaneous
            : null;

        if (mode is { } chosen)
        {
            _stateManager.TransitionTo(PlayingState.CreateNewGame(_controlSettings, _gameSettings, chosen, _sprites, _highScoreStore));
        }
        else if (WasPressed(state, Keys.F5))
        {
            _stateManager.TransitionTo(new SettingsState(_services, _gameSettingsStore));
        }
        else if (WasPressed(state, Keys.F10))
        {
            _stateManager.TransitionTo(new DefineInputsState(_services, _controlSettingsStore));
        }
    }

    /// <summary>True on the tick <paramref name="key"/> goes down (press, not hold).</summary>
    private bool WasPressed(KeyboardState current, Keys key) =>
        !_previousKeyboardState.IsKeyDown(key) && current.IsKeyDown(key);

    /// <summary>Drops straight into the end of a game, with a score that has to qualify (notes §116).</summary>
    /// <remarks>The GAME OVER page, the CONG initials screen and the table are otherwise a whole game away,
    /// which is how the ceremony was verified.</remarks>
    private void StartEndOfGameFlow()
    {
        GameSession session = GameSession.CreateNewGame(GameMode.OnePlayer, _input, controls: _controlSettings, settings: _gameSettings);
        session.Current.Score = DevKeys.QualifyingScore;
        _stateManager.TransitionTo(GameOverState.CreateFromSession(_input, _sprites, _highScoreStore, session));
    }

    /// <summary>Switches between the windowed window and borderless full screen at the desktop's own mode.</summary>
    private void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            ApplyBackBuffer(ScreenSize.Width * _windowedScale, ScreenSize.Height * _windowedScale, isFullScreen: false);
            return;
        }

        Point desktopSize = DisplayInfo.GetDesktopResolution();
        ApplyBackBuffer(desktopSize.X, desktopSize.Y, isFullScreen: true);
    }
}
