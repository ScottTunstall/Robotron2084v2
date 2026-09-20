using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The "1000".."5000" number shown where the player rescued a human. Display only.</summary>
/// <seealso cref="Human"/>
/// <remarks>ROM: <c>HUMKIL</c>'s <c>PCFLG</c> path (RRH11.ASM) picks <c>P1000 + 4*min(SAVCNT,5)</c>
/// and holds it for 60 ROM frames.</remarks>
public sealed class RescueScoreMarker : IEntity
{
    /// <summary>How long the display stays on the field.</summary>
    private const int LifeRomTicks = 60;

    private static readonly int Size = ScreenSize.Scaled(GameplayConstants.EntitySizeSpecPixels);

    private readonly IntVector2 _position;

    /// <summary>Index into <see cref="SpriteSet.RescueScoreDisplays"/> (0..4 = 1000..5000).</summary>
    private readonly int _displayIndex;

    private int _ticksRemaining;

    /// <summary>Shows the display for one rescue.</summary>
    /// <param name="position">The rescue spot.</param>
    /// <param name="rescuesThisLife">How many humans rescued this life, counting this one; the display caps at 5000.</param>
    public RescueScoreMarker(IntVector2 position, int rescuesThisLife)
    {
        _position = position;
        _ticksRemaining = GameplayConstants.PortTicks(LifeRomTicks);
        _displayIndex = Math.Clamp(rescuesThisLife, 1, 5) - 1;
    }

    /// <summary>The rescue spot.</summary>
    public IntVector2 Position => _position;

    /// <summary>The display's own box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, Size, Size);

    /// <summary>Alive until the linger runs out.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Counts the linger down.</summary>
    /// <param name="gameTime">Unused — the linger is counted in ticks.</param>
    /// <param name="field">Unused.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (--_ticksRemaining <= 0)
        {
            LifeState = EntityLifeState.Dead;
        }
    }

    /// <summary>Draws the "1000".."5000" picture this rescue earned.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the display pictures.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        sprites.DrawSprite(spriteBatch, sprites.RescueScoreDisplays[_displayIndex], Bounds, Color.White);
    }
}
