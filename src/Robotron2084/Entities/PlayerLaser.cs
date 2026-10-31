using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The player's laser shot: a straight bolt that flies until it hits a wall or a robot, then vanishes.</summary>
/// <seealso cref="LaserSlots" />
/// <remarks>
///     It has no <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> and no timer.
///     <list type="bullet">
///         <item>
///             Original source: <c>RRG23.ASM</c>, routine <c>LTAB</c> (the table of laser sprites:
///             <c>LLPC</c>/<c>ULPC</c>/<c>DLLPC</c>/<c>ULLPC</c>)
///         </item>
///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>$3237</c> (<c>LASER_DESCRIPTOR TABLE</c>)</item>
///     </list>
/// </remarks>
public sealed class PlayerLaser : IEntity, IAnimationFrameSource, IRemovable
{
    private static readonly int Size = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.MissileSizeArcadePixels);
    private readonly TextureSet _textureSet;
    private IntVector2 _position;

    /// <summary>Makes a laser that flies in the given direction.</summary>
    /// <param name="textureSet">The shared texture set.</param>
    /// <param name="position">Where it starts. The player works this out from the way it is fired.</param>
    /// <param name="direction">The way it flies. This never changes.</param>
    public PlayerLaser(TextureSet textureSet, IntVector2 position, Direction8 direction)
    {
        _textureSet = textureSet;
        _position = position;
        Direction = direction;
    }

    /// <summary>The way the laser flies. It never changes.</summary>
    public Direction8 Direction { get; }

    /// <summary>Alive until it reaches the wall or hits something. Then it is dead at once.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the top-left corner of the laser's box is.</summary>
    public IntVector2 Position => _position;

    /// <summary>Draws the sprite for the way this laser is flying.</summary>
    /// <param name="spriteBatch">What the laser is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        // The laser's palette index keeps changing colour, so the whole laser flashes.
        _textureSet.BlitterDraw.DrawSpriteSolid(spriteBatch, GetCurrentAnimationFrameTexture(), GetBounds(),
            _textureSet.BlitterDraw.GetPaletteIndexColour(PlayerTuning.LaserPaletteIndex));
    }

    /// <summary>The box the laser takes up on the screen.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, Size, Size);
    }

    /// <summary>The sprite for the way this laser is flying. The arcade has four laser sprites (<c>LTAB</c>, notes §19).</summary>
    public Texture2D GetCurrentAnimationFrameTexture()
    {
        return Direction switch
        {
            Direction8.Left or Direction8.Right => _textureSet.LaserBarTexture,
            Direction8.Up or Direction8.Down => _textureSet.LaserColumnTexture,
            Direction8.UpLeft or Direction8.DownRight => _textureSet.LaserDiagonalMainTexture,
            Direction8.DownLeft or Direction8.UpRight => _textureSet.LaserDiagonalAntiTexture,
            _ => throw new InvalidOperationException($"Unexpected laser direction {Direction}")
        };
    }

    /// <summary>Kills the laser at once, which frees its slot for a new laser.</summary>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. The laser flies one step the way it is going, and dies if it reaches the wall.</summary>
    /// <param name="gameTime">Not used. The laser goes the same distance on every <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>.</param>
    /// <param name="playField">The playfield. It tells the laser when it has reached the wall, and makes the flash there.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c>, routine <c>LASDIE</c>, where a laser dies at the wall</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>LASER_HIT_WALL</c> (<c>$3389</c>)</item>
    ///     </list>
    /// </remarks>
    public void Update(GameTime gameTime, PlayField playField)
    {
        if (!this.IsAlive()) return;

        // The wall is checked along the whole of this move, not just where the laser ends up. Otherwise a laser could jump right over the wall.
        var boundsBeforeMove = GetBounds();
        _position += Direction.ToIntVector() * PlayerTuning.LaserSpeed;
        if (playField.HitsWall(Rectangle.Union(boundsBeforeMove, GetBounds())))
        {
            // The laser stops at the wall and leaves a short flash there (ROM: RRG23 LASDIE).
            playField.SpawnLaserWallFlare(GetBounds(), Direction);
            Kill();
        }
    }
}
