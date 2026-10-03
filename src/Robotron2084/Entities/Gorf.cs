using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>Gorf is a robot of the author's own that stands where it is put and flaps between two animation frames. It does not move or shoot yet.</summary>
/// <remarks>
/// A new kind of robot with no arcade routine behind it (notes §138.2). All it does for now is animate, die to a laser and kill
/// the player on touch; how it behaves in a wave is still to be decided.
/// </remarks>
public sealed class Gorf : IExplodable, IRemovable
{
    /// <summary>How many ROM frames each of its two animation frames shows for before the other takes over.</summary>
    private const int AnimationFrameRomFrames = 8;

    /// <summary>The robot's own box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.GorfCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.GorfCollisionSize.Height));

    /// <summary>How many timer units each animation frame shows for (a tick adds 5; an arcade frame is 6 units).</summary>
    private static readonly int AnimationFrameClockUnits = ArcadeClock.ToClockUnits(AnimationFrameRomFrames);

    private readonly SpriteSet _sprites;
    private int _animationFrameIndex;
    private int _animationTimer;
    private IntVector2 _position;

    /// <summary>Creates a Gorf.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the Gorf.</param>
    public Gorf(SpriteSet sprites, IntVector2 position)
    {
        _sprites = sprites;
        _position = position;
    }

    /// <summary>The robot's own box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until shot or killed on contact; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Top-left of the Gorf.</summary>
    public IntVector2 Position => _position;

    /// <summary>Which of the two animation frames is showing, 0 or 1 (test hook).</summary>
    internal int AnimationFrameIndex => _animationFrameIndex;

    /// <summary>Gets the animation frame the Gorf is showing, for drawing and for the appear and explosion effects.</summary>
    /// <returns>The texture for the current frame.</returns>
    public Texture2D GetCurrentAnimationFrame() => _sprites.GorfAnimationFrames[_animationFrameIndex];

    /// <summary>Draws the current animation frame in its own colours.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), Bounds, Color.White);
    }

    /// <summary>Kills the Gorf outright: no flash, no death animation.</summary>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Runs one tick: after enough of them, swaps to the other animation frame.</summary>
    /// <param name="gameTime">Unused — the animation is counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive() || field.RobotsFrozen)
        {
            return;
        }

        _animationTimer += ArcadeClock.UnitsPerPortTick;
        if (_animationTimer < AnimationFrameClockUnits)
        {
            return;
        }

        _animationTimer -= AnimationFrameClockUnits;
        _animationFrameIndex = (_animationFrameIndex + 1) % _sprites.GorfAnimationFrames.Length;
    }
}
