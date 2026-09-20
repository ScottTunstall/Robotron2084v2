using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The skull left where a robot killed a human. Display only; it lingers, then goes.</summary>
/// <seealso cref="Human"/>
/// <remarks>
/// ROM: <c>HUMKIL</c> (RRH11.ASM) draws <c>SKULP</c> (12x11 px), plays <c>HKSND</c> and sets a
/// 90-ROM-frame countdown (PD2).
/// </remarks>
public sealed class SkullMarker : IEntity
{
    /// <summary>How long the skull stays on the field.</summary>
    private const int LifeRomTicks = 90;

    /// <summary>The skull picture's own 12x11 arcade px box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.SkullCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.SkullCollisionSize.Height));

    private readonly IntVector2 _position;
    private int _ticksRemaining;

    /// <summary>Leaves a skull at the given position.</summary>
    /// <param name="position">Where the human was killed.</param>
    public SkullMarker(IntVector2 position)
    {
        _position = position;
        _ticksRemaining = GameplayConstants.PortTicks(LifeRomTicks);
    }

    /// <summary>The death spot.</summary>
    public IntVector2 Position => _position;

    /// <summary>The skull picture's own box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until the linger runs out.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

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

    /// <summary>Draws the skull in the picture's own colours.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the skull picture.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        sprites.DrawSprite(spriteBatch, sprites.Skull, Bounds, Color.White);
    }
}
