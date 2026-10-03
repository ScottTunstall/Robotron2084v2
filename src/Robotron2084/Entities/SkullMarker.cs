using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The little skull left on the ground where a robot killed a family member. It fades away after a while.</summary>
/// <seealso cref="Human"/>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRH11.ASM</c>, routine <c>HUMKIL</c> (draws <c>SKULP</c> and plays <c>HKSND</c>)</item>
/// <item>Disassembly: Not separately labelled in <c>asm/robomame.asm</c>.</item>
/// </list>
/// </remarks>
public sealed class SkullMarker : IEntity
{
    /// <summary>How long the skull stays on the field.</summary>
    private const int LifeRomFrames = 90;

    /// <summary>The skull sprite's own 12x11 arcade px box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.SkullCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.SkullCollisionSize.Height));

    private readonly IntVector2 _position;
    private readonly SpriteSet _sprites;
    private int _ticksRemaining;

    /// <summary>Leaves a skull at the given position.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where the human was killed.</param>
    public SkullMarker(SpriteSet sprites, IntVector2 position)
    {
        _sprites = sprites;
        _position = position;
        _ticksRemaining = ArcadeClock.ToPortTicks(LifeRomFrames);
    }

    /// <summary>The skull sprite's own box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until the linger runs out.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The death spot.</summary>
    public IntVector2 Position => _position;

    /// <summary>Draws the skull in the sprite's own colours.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, _sprites.Skull, Bounds, Color.White);
    }

    /// <summary>Counts the linger down.</summary>
    /// <param name="gameTime">Unused — the linger is counted in ticks, not seconds.</param>
    /// <param name="field">Unused.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (--_ticksRemaining <= 0)
        {
            LifeState = EntityLifeState.Dead;
        }
    }
}
