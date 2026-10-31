using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The little skull left on the ground where a robot killed a family member. It fades away after a while.</summary>
/// <seealso cref="Human" />
/// <remarks>
///     It has no <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <see cref="_lifeTimer" /> says when it is time for it to go.
///     <list type="bullet">
///         <item>
///             Original source: <c>RRH11.ASM</c>, routine <c>HUMKIL</c> (draws <c>SKULP</c> and plays
///             <c>HKSND</c>)
///         </item>
///         <item>Disassembly: Not separately labelled in <c>asm/robomame.asm</c>.</item>
///     </list>
/// </remarks>
public sealed class SkullMarker : IEntity
{
    /// <summary>How long the skull stays on the field.</summary>
    private const int LifeArcadeFrames = 90;

    /// <summary>How big the skull is, in port pixels. It is the size of the skull's sprite.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.SkullCollisionSize.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.SkullCollisionSize.Height));

    private readonly ArcadeFrameTimer _lifeArcadeFrameTimer = new();
    private readonly IntVector2 _position;
    private readonly SpriteSet _spriteSet;

    /// <summary>Leaves a skull where a family member was killed.</summary>
    /// <param name="spriteSet">The shared sprite set.</param>
    /// <param name="position">Where the family member was killed.</param>
    public SkullMarker(SpriteSet spriteSet, IntVector2 position)
    {
        _spriteSet = spriteSet;
        _position = position;
    }

    /// <summary>Alive until its time on the field runs out.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the family member was killed.</summary>
    public IntVector2 Position => _position;

    /// <summary>Draws the skull in the sprite's own colours.</summary>
    /// <param name="spriteBatch">What the skull is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        _spriteSet.BlitterDraw.DrawSprite(spriteBatch, _spriteSet.SkullSprite, GetBounds(), Color.White);
    }

    /// <summary>The box the skull is drawn in.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);
    }

    /// <summary>Counts down the time the skull has left on the field.</summary>
    /// <param name="gameTime">Not used. The time is counted in <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="playField">Not used.</param>
    public void Update(GameTime gameTime, PlayField playField)
    {
        if (_lifeArcadeFrameTimer.Tick(LifeArcadeFrames))
            LifeState = EntityLifeState.Dead;
    }
}
