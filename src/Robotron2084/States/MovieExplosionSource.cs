using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;

namespace Robotron2084.States;

/// <summary>
///     The EXP opcode's dead object: the ROM's `KILLOF` leaves the object's sprite
///     in place and `EXPP` sets the explosion's row to ACTHIT+6, so the fan's rect is
///     that sprite at (the object's column, row 166).
/// </summary>
internal sealed class MovieExplosionSource : IExplodable
{
    private readonly Texture2D _animationFrameTexture;
    private readonly Rectangle _bounds;

    public MovieExplosionSource(Texture2D animationFrameTexture, Rectangle bounds)
    {
        _animationFrameTexture = animationFrameTexture;
        _bounds = bounds;
    }

    public EntityLifeState LifeState => EntityLifeState.Dead;

    public IntVector2 Position => new(_bounds.X, _bounds.Y);

    public void Draw(SpriteBatch spriteBatch)
    {
    }

    public Rectangle GetBounds()
    {
        return _bounds;
    }

    public Texture2D GetCurrentAnimationFrameTexture()
    {
        return _animationFrameTexture;
    }

    public Texture2D GetExplosionAnimationFrameTexture()
    {
        return _animationFrameTexture;
    }

    public Rectangle GetExplosionBounds()
    {
        return _bounds;
    }

    public void Update(GameTime gameTime, PlayField playField)
    {
    }
}
