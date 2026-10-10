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
/// It has no beat. The <see cref="PlayField"/> calls <see cref="Update"/> on every tick, through
/// <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>.
/// The one time it does not is during the short freeze just after the player is killed.
///
/// <see cref="_ticksRemaining"/> counts down
/// the ticks until it goes.
///
/// <list type="bullet">
/// <item>Original source: <c>RRH11.ASM</c>, routine <c>HUMKIL</c> (draws <c>SKULP</c> and plays
/// <c>HKSND</c>)</item>
/// <item>Disassembly: Not separately labelled in <c>asm/robomame.asm</c>.</item>
/// </list>
/// </remarks>
public sealed class SkullMarker : IEntity
{
    /// <summary>How long the skull stays on the field. It is turned into ticks to set <see cref="_ticksRemaining"/>, which then counts down to nothing.</summary>
    private const int LifeRomFrames = 90;

    /// <summary>How big the skull is, in port pixels. It is the size of the skull's sprite.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.SkullCollisionSize.Width), ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.SkullCollisionSize.Height));

    private readonly IntVector2 _position;
    private readonly SpriteSet _sprites;
    private int _ticksRemaining;

    /// <summary>Leaves a skull where a family member was killed.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where the family member was killed.</param>
    public SkullMarker(SpriteSet sprites, IntVector2 position)
    {
        _sprites = sprites;
        _position = position;
        _ticksRemaining = ArcadeClock.ToPortTicks(LifeRomFrames);
    }

    /// <summary>The box the skull is drawn in.</summary>
    public Rectangle GetBounds() => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until its time on the field runs out.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the family member was killed.</summary>
    public IntVector2 Position => _position;

    /// <summary>Draws the skull in the sprite's own colours.</summary>
    /// <param name="spriteBatch">What the skull is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, _sprites.SkullSprite, GetBounds(), Color.White);
    }

    /// <summary>Counts down the time the skull has left on the field.</summary>
    /// <param name="gameTime">Not used. The time is counted in ticks.</param>
    /// <param name="field">Not used.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (--_ticksRemaining <= 0)
        {
            LifeState = EntityLifeState.Dead;
        }
    }
}
