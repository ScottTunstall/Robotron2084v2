using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The "1000" to "5000" number that pops up where you just rescued a family member, showing the points you earned.</summary>
/// <seealso cref="Human"/>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRH11.ASM</c>, routine <c>HUMKIL</c> (the <c>PCFLG</c> path)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$03C5</c> (<c>RESCUE_FAMILY_POINTS_TABLE</c>)</item>
/// </list>
/// </remarks>
public sealed class RescueScoreMarker : IEntity
{
    /// <summary>How long the display stays on the field.</summary>
    private const int LifeRomFrames = 60;

    private static readonly int Size = ScreenSize.ToPortPixels(CollisionSizes.EntitySizeSpecPixels);

    /// <summary>Index into <see cref="SpriteSet.RescueScoreDisplays"/> (0..4 = 1000..5000).</summary>
    private readonly int _displayIndex;

    private readonly IntVector2 _position;
    private readonly SpriteSet _sprites;
    private int _ticksRemaining;

    /// <summary>Shows the display for one rescue.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">The rescue spot.</param>
    /// <param name="rescuesThisLife">How many humans rescued this life, counting this one; the display caps at 5000.</param>
    public RescueScoreMarker(SpriteSet sprites, IntVector2 position, int rescuesThisLife)
    {
        _sprites = sprites;
        _position = position;
        _ticksRemaining = ArcadeClock.ToPortTicks(LifeRomFrames);
        _displayIndex = Math.Clamp(rescuesThisLife, 1, ScoreValues.RescueBonusMaxCount) - 1;
    }

    /// <summary>The display's own box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, Size, Size);

    /// <summary>Alive until the linger runs out.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The rescue spot.</summary>
    public IntVector2 Position => _position;

    /// <summary>Draws the "1000".."5000" sprite this rescue earned.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, _sprites.RescueScoreDisplays[_displayIndex], Bounds, Color.White);
    }

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
}
