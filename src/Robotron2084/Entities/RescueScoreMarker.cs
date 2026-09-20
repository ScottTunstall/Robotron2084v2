using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The "1000".."5000" display left behind where the player rescued a human. It shows which
/// rescue chain this was, is display only (no collisions, no score of its own — the points
/// go to the session), and removes itself after a fixed linger.
/// </summary>
/// <remarks>
/// `HUMKIL`'s `PCFLG` path in RRH11.ASM: the picture is `P1000 + 4*min(SAVCNT,5)`, and it
/// stays for 60 ticks.
///
/// The ROM also clamps the marker's X to XMAX-6 so the 12-pixel display cannot run off the
/// right edge; here the display is centred in a box whose position is already wall-clamped
/// (the human's), so the same result holds without an explicit clamp.
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
