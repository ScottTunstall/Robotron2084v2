using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The skull and crossbones left behind where a robot killed a human. It is display only:
/// it never collides with anything, scores nothing, and takes itself off the field after a
/// fixed linger.
/// </summary>
/// <remarks>
/// `HUMKIL` in RRH11.ASM: it draws the `SKULP` picture (12x11 px), plays `HKSND`, and sets a
/// 90-tick countdown (PD2).
/// </remarks>
public sealed class SkullMarker : IEntity
{
    /// <summary>How long the skull stays on the field, in ROM ticks.</summary>
    /// <remarks>ROM: PD2 = 90 ticks (HUMKIL, at HKIL10).</remarks>
    private const int LifeRomTicks = 90;

    /// <summary>The display box: the skull picture's own size, 12x11 arcade px, in port pixels.</summary>
    /// <remarks>The ROM's `SKULP` picture.</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.SkullCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.SkullCollisionSize.Height));

    private readonly IntVector2 _position;
    private int _ticksRemaining;

    /// <summary>Leaves a skull at the given position.</summary>
    /// <param name="position">Where the human was killed (its top-left).</param>
    public SkullMarker(IntVector2 position)
    {
        _position = position;
        _ticksRemaining = GameplayConstants.PortTicks(LifeRomTicks);
    }

    /// <summary>The death spot, taken from the human that was killed.</summary>
    public IntVector2 Position => _position;

    /// <summary>The skull picture's own box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until the linger runs out (see <see cref="EntityLifeState"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Counts the linger down; the marker is pruned when it runs out.</summary>
    /// <param name="gameTime">Unused — the linger is counted in ticks, not seconds.</param>
    /// <param name="field">Unused — the marker touches nothing on the playfield.</param>
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
