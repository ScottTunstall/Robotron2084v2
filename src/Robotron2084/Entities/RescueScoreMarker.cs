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
/// It has no beat. The <see cref="PlayField"/> calls <see cref="Update"/> on every tick, through
/// <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>.
/// The one time it does not is during the short freeze just after the player is killed.
///
/// <see cref="_ticksRemaining"/> counts down
/// the ticks until it goes.
///
/// <list type="bullet">
/// <item>Original source: <c>RRH11.ASM</c>, routine <c>HUMKIL</c> (the <c>PCFLG</c> path)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$03C5</c> (<c>RESCUE_FAMILY_POINTS_TABLE</c>)</item>
/// </list>
/// </remarks>
public sealed class RescueScoreMarker : IEntity
{
    /// <summary>How long the number stays on the field, in 50ths of a second. It is changed to ticks to set <see cref="_ticksRemaining"/>, which then counts down to nothing.</summary>
    private const int LifeRomFrames = 60;

    private static readonly int Size = ScreenSize.ToPortPixels(CollisionSizes.EntitySizeSpecPixels);

    /// <summary>Which number is shown, as a place in <see cref="SpriteSet.RescueScoreDisplays"/>. The first place is the 1000 and the last is the 5000.</summary>
    private readonly int _displayIndex;

    private readonly IntVector2 _position;
    private readonly SpriteSet _sprites;
    private int _ticksRemaining;

    /// <summary>Makes the number for one rescue.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where the family member was rescued.</param>
    /// <param name="rescuesThisLife">How many family members have been rescued in this life, counting this one. The number shown never goes above 5000.</param>
    public RescueScoreMarker(SpriteSet sprites, IntVector2 position, int rescuesThisLife)
    {
        _sprites = sprites;
        _position = position;
        _ticksRemaining = ArcadeClock.ToPortTicks(LifeRomFrames);
        _displayIndex = Math.Clamp(rescuesThisLife, 1, ScoreValues.RescueBonusMaxCount) - 1;
    }

    /// <summary>The box the number is drawn in.</summary>
    public Rectangle GetBounds() => new(_position.X, _position.Y, Size, Size);

    /// <summary>Alive until its time on the field runs out.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the family member was rescued.</summary>
    public IntVector2 Position => _position;

    /// <summary>Draws the number of points this rescue earned.</summary>
    /// <param name="spriteBatch">What the number is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, _sprites.RescueScoreDisplays[_displayIndex], GetBounds(), Color.White);
    }

    /// <summary>Counts down the time the number has left on the field.</summary>
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
