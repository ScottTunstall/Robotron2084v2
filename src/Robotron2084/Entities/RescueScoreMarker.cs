using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The rescue score display ("1000".."5000") left where the player rescued
/// a human (ROM HUMKIL PCFLG path: picture = P1000 + 4*min(SAVCNT,5),
/// 60-tick timer). Pure display: no collisions, no score — drawn at the
/// rescue spot for 60 ROM ticks, then vanishes.
/// The ROM clamps the marker X to XMAX-6 so the 12-arcade-px display never
/// overflows the right edge; here the display is drawn centered in a box
/// whose position is already wall-clamped (the human's), so the same result
/// holds without an explicit clamp.
/// </summary>
public sealed class RescueScoreMarker : IEntity
{
    /// <summary>ROM rescue display linger: 60 game ticks (HUMKIL PCFLG path).</summary>
    private const int LifeRomTicks = 60;

    private static readonly int Size = ScreenSize.Scaled(GameplayConstants.EntitySizeSpecPixels);

    private readonly IntVector2 _position;

    /// <summary>Index into <see cref="SpriteSet.RescueScoreDisplays"/> (0..4 = 1000..5000).</summary>
    private readonly int _displayIndex;

    private int _ticksRemaining;

    /// <param name="position">Rescue spot (the human's top-left).</param>
    /// <param name="rescuesThisLife">
    /// Running rescue count (SAVCNT) at rescue time — the display shows
    /// min(count,5): the ROM caps the DISPLAY at 5000, not the counter.
    /// </param>
    public RescueScoreMarker(IntVector2 position, int rescuesThisLife)
    {
        _position = position;
        _ticksRemaining = GameplayConstants.PortTicks(LifeRomTicks);
        _displayIndex = Math.Clamp(rescuesThisLife, 1, 5) - 1;
    }

    public IntVector2 Position => _position;

    public Rectangle Bounds => new(_position.X, _position.Y, Size, Size);

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

        sprites.DrawSprite(spriteBatch, sprites.RescueScoreDisplays[_displayIndex], Bounds, Color.White);
    }
}
