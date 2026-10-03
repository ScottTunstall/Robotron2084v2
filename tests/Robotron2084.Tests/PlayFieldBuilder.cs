using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Level.Collisions;
using Robotron2084.Palette;

namespace Robotron2084.Tests;

/// <summary>
/// Builds the <see cref="PlayField"/> a test needs: a headless field with an empty wave, a fake input, the standard
/// bounds, a fixed seed and three lives, and only the parts a test cares about overridden.
/// </summary>
internal sealed class PlayFieldBuilder
{
    /// <summary>The inner play area every test field uses unless it says otherwise: the canvas less a 20 spec-pixel margin.</summary>
    public static readonly Rectangle DefaultBounds = new(
        ScreenSize.ToPortPixels(20),
        ScreenSize.ToPortPixels(20),
        ScreenSize.Width - ScreenSize.ToPortPixels(40),
        ScreenSize.Height - ScreenSize.ToPortPixels(40));

    private const int DefaultSeed = 1234;
    private const int DefaultLives = 3;

    private LevelParameters _parameters = new(LevelNumber: 1);
    private IPlayerInputSource _input = new FakeInputSource();
    private Rectangle _bounds = DefaultBounds;
    private Random _random = new(DefaultSeed);
    private int _lives = DefaultLives;
    private int _score;
    private int _rescues;
    private GamePalette? _palette;
    private bool _playerInvincible = true;
    private IPixelCollision? _pixelCollision;

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

    /// <summary>Starts the player with this many humans already rescued this life.</summary>
    /// <param name="rescues">The rescues carried in.</param>
    public PlayFieldBuilder WithRescues(int rescues)
    {
        _rescues = rescues;
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

    /// <summary>Builds the field.</summary>
    public PlayField Build() => new(
        TestSprites.Shared,
        _parameters,
        _input,
        _bounds,
        new WallColorCycle(),
        _random,
        _lives,
        _score,
        _rescues,
        palette: _palette,
        playerInvincibleForTesting: _playerInvincible,
        contactTest: _pixelCollision is null ? null : new PixelContactTest(_pixelCollision));
}
