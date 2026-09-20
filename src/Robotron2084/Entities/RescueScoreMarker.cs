using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The floating "1000".."5000" number that pops up where the player rescues a human (see
/// <see cref="HumanKind"/>). Its value climbs with how many humans have been rescued this
/// life — 1000, 2000, 3000, 4000, capping at 5000 for the fifth rescue and beyond. Display
/// only: it awards no score itself (that's already added elsewhere) and removes itself after
/// a fixed time.
/// </summary>
/// <remarks>
/// ROM: `HUMKIL`'s `PCFLG` path (RRH11.ASM) picks `P1000 + 4*min(SAVCNT,5)` and holds it for
/// 60 ROM frames, converted to port ticks by <see cref="GameplayConstants.PortTicks"/>. The
/// ROM also clamps X to XMAX-6 so the display can't run off the right edge; here the human's
/// already-wall-clamped position gives the same result without an explicit clamp.
/// </remarks>
public sealed class RescueScoreMarker : IEntity
{
    /// <summary>How long the display stays on the field, in ROM ticks.</summary>
    /// <remarks>ROM: 60 ticks (HUMKIL's `PCFLG` path).</remarks>
    private const int LifeRomTicks = 60;

    private static readonly int Size = ScreenSize.Scaled(GameplayConstants.EntitySizeSpecPixels);

    private readonly IntVector2 _position;

    /// <summary>Index into <see cref="SpriteSet.RescueScoreDisplays"/> (0..4 = 1000..5000).</summary>
    private readonly int _displayIndex;

    private int _ticksRemaining;

    /// <summary>Shows the display for one rescue.</summary>
    /// <param name="position">The rescue spot (the human's top-left).</param>
    /// <param name="rescuesThisLife">
    /// How many humans this player has rescued this life, counting this one. The DISPLAY is
    /// capped at 5000, which is not the same as capping the count, so the picture index is
    /// clamped to 1..5.
    /// </param>
    public RescueScoreMarker(IntVector2 position, int rescuesThisLife)
    {
        _position = position;
        _ticksRemaining = GameplayConstants.PortTicks(LifeRomTicks);
        _displayIndex = Math.Clamp(rescuesThisLife, 1, 5) - 1;
    }

    /// <summary>The rescue spot, taken from the human that was saved.</summary>
    public IntVector2 Position => _position;

    /// <summary>The display's own box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, Size, Size);

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
