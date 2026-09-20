using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Input;
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
        _input = new CompositePlayerInputSource(new KeyboardPlayerInputSource(), new GamePadPlayerInputSource(PlayerIndex.One));
        _highScoreStore = new HighScoreStore();
        _stateManager = new GameStateManager(new TitleScreenState(_input, _sprites, _highScoreStore));

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

        // ---- attract DEV KEYS (port-only; notes §97) -------------------------
        // F1 and F2 drop straight into the attract sequence — F1 the storyline
        // movie (the family and the hulk), F2 the phony-player demo game — so a
        // scene can be inspected without sitting out the title's 12-second idle.
        // F3 HELD fast-forwards the movie, which is how the hulk's walk (ROM
        // frame ~2574) is reached in seconds rather than after the text crawl.
        DevKeys.AttractFastForward = state.IsKeyDown(Keys.F3);
        if (Pressed(state, Keys.F1))
        {
            _stateManager.TransitionTo(new StorylineState(_sprites, _highScoreStore, _input, new Random()));
        }
        else if (Pressed(state, Keys.F2))
        {
            _stateManager.TransitionTo(new AttractState(_sprites, _highScoreStore, _input));
        }
        else if (Pressed(state, Keys.F4))
        {
            _stateManager.TransitionTo(new HighScoreTableState(_input, _sprites, _highScoreStore));
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
        _previousKeyboardState.IsKeyDown(key) && !current.IsKeyDown(key);
}
