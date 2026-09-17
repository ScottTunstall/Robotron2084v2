using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The skull &amp; crossbones left where a robot killed a human (ROM HUMKIL:
/// SKULP picture, 90-tick timer, HKSND). Pure display: no collisions, no
/// score — drawn for 90 ROM ticks at the death spot, then vanishes.
/// </summary>
public sealed class SkullMarker : IEntity
{
    /// <summary>ROM skull linger: PD2 = 90 game ticks (HUMKIL HKIL10).</summary>
    private const int LifeRomTicks = 90;

    /// <summary>Display box = the ROM skull picture dimensions (12x11 arcade px).</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.SkullCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.SkullCollisionSize.Height));

    private readonly IntVector2 _position;
    private int _ticksRemaining;

    public SkullMarker(IntVector2 position)
    {
        _position = position;
        _ticksRemaining = GameplayConstants.PortTicks(LifeRomTicks);
    }

    public IntVector2 Position => _position;

    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    public void Update(GameTime gameTime, PlayField field)
    {
        if (--_ticksRemaining <= 0)
        {
            LifeState = EntityLifeState.Dead;
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        sprites.DrawSprite(spriteBatch, sprites.Skull, Bounds, Color.White);
    }
}
