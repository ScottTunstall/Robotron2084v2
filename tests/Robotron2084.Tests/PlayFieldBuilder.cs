using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Level.Collisions;
using Robotron2084.Palette;

namespace Robotron2084.Tests;

/// <summary>
///     Builds the <see cref="PlayField" /> a test needs: a headless field with an empty wave, a fake input, the standard
///     bounds, a fixed seed and three lives, and only the parts a test cares about overridden.
/// </summary>
internal sealed class PlayFieldBuilder
{
    private const int DefaultSeed = 1234;
    private const int DefaultLives = 3;

    /// <summary>The inner play area every test field uses unless it says otherwise: the canvas less a 20 arcade pixel margin.</summary>
    public static readonly Rectangle DefaultBounds = new(
        ScreenSize.ToPortPixelsFromArcadePixels(20),
        ScreenSize.ToPortPixelsFromArcadePixels(20),
        ScreenSize.Width - ScreenSize.ToPortPixelsFromArcadePixels(40),
        ScreenSize.Height - ScreenSize.ToPortPixelsFromArcadePixels(40));

    private Rectangle _bounds = DefaultBounds;
    private bool _brainsChaseMikeyBug = true;
    private IReadOnlyList<int> _familySlotsLeftOver = [];
    private IPlayerInputSource _input = new FakeInputSource();
    private int _lives = DefaultLives;
    private GamePalette? _palette;

    private LevelParameters _parameters = new(1);
    private IPixelCollision? _pixelCollision;
    private bool _playerInvincible = true;
    private Random _random = new(DefaultSeed);
    private int _score;
    private bool _tankShellBugEnabled = true;

    /// <summary>Uses these wave parameters.</summary>
    /// <param name="parameters">The wave.</param>
    public PlayFieldBuilder WithParameters(LevelParameters parameters)
    {
        _parameters = parameters;
        return this;
    }

    /// <summary>Uses this input for the player.</summary>
    /// <param name="input">The input source.</param>
    public PlayFieldBuilder WithInput(IPlayerInputSource input)
    {
        _input = input;
        return this;
    }

    /// <summary>Uses these inner bounds.</summary>
    /// <param name="bounds">The play area.</param>
    public PlayFieldBuilder WithBounds(Rectangle bounds)
    {
        _bounds = bounds;
        return this;
    }

    /// <summary>Seeds the field's random source.</summary>
    /// <param name="seed">The seed.</param>
    public PlayFieldBuilder WithSeed(int seed)
    {
        _random = new Random(seed);
        return this;
    }

    /// <summary>Uses this random source.</summary>
    /// <param name="random">The source.</param>
    public PlayFieldBuilder WithRandom(Random random)
    {
        _random = random;
        return this;
    }

    /// <summary>Gives the player this many lives.</summary>
    /// <param name="lives">The lives.</param>
    public PlayFieldBuilder WithLives(int lives)
    {
        _lives = lives;
        return this;
    }

    /// <summary>Starts the player with this score.</summary>
    /// <param name="score">The score carried in.</param>
    public PlayFieldBuilder WithScore(int score)
    {
        _score = score;
        return this;
    }


    /// <summary>Wires in a live palette.</summary>
    /// <param name="palette">The palette.</param>
    public PlayFieldBuilder WithPalette(GamePalette palette)
    {
        _palette = palette;
        return this;
    }

    /// <summary>Sets whether the player is the invincible playtest player.</summary>
    /// <param name="invincible">True for the playtest player; false makes the player mortal.</param>
    public PlayFieldBuilder WithPlayerInvincible(bool invincible)
    {
        _playerInvincible = invincible;
        return this;
    }

    /// <summary>Uses pixel-perfect collision, or the collision boxes when null.</summary>
    /// <param name="pixelCollision">The collision test.</param>
    public PlayFieldBuilder WithPixelCollision(IPixelCollision? pixelCollision)
    {
        _pixelCollision = pixelCollision;
        return this;
    }

    /// <summary>Sets whether a fizzled shell stays on the wave's shell count, as in the arcade.</summary>
    /// <param name="bug">True to keep the arcade's bug.</param>
    public PlayFieldBuilder WithTankShellBug(bool bug)
    {
        _tankShellBugEnabled = bug;
        return this;
    }

    /// <summary>Sets whether every brain starts the wave chasing the first Mikey, as in the arcade.</summary>
    /// <param name="bug">True to keep the arcade's bug.</param>
    public PlayFieldBuilder WithBrainsChaseMikeyBug(bool bug)
    {
        _brainsChaseMikeyBug = bug;
        return this;
    }

    /// <summary>Says which places in the family list still held a family member when the last wave or life ended.</summary>
    /// <param name="slots">The places.</param>
    public PlayFieldBuilder WithFamilySlotsLeftOver(params int[] slots)
    {
        _familySlotsLeftOver = slots;
        return this;
    }

    /// <summary>Builds the field.</summary>
    public PlayField Build()
    {
        return new PlayField(
            TestSprites.Shared,
            _parameters,
            _input,
            _bounds,
            new WallColorCycle(),
            _random,
            _lives,
            _score,
            _palette,
            _playerInvincible,
            _pixelCollision is null ? null : new PixelContactTest(_pixelCollision),
            tankShellBugEnabled: _tankShellBugEnabled,
            brainsChaseMikeyBugEnabled: _brainsChaseMikeyBug,
            familySlotsLeftOver: _familySlotsLeftOver);
    }
}
