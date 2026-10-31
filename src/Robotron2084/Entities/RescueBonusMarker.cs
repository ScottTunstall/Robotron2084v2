using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
///     The "1000" to "5000" number that pops up where you just rescued a family member, showing the rescue bonus you
///     earned.
/// </summary>
/// <seealso cref="FamilyMember" />
/// <remarks>
///     It has no <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <see cref="_lifeArcadeFrameTimer" /> says when it is time for it to go.
///     <list type="bullet">
///         <item>Original source: <c>RRH11.ASM</c>, routine <c>HUMKIL</c> (the <c>PCFLG</c> path)</item>
///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>$03C5</c> (<c>RESCUE_FAMILY_POINTS_TABLE</c>)</item>
///     </list>
/// </remarks>
public sealed class RescueBonusMarker : IEntity
{
    /// <summary>How long the number stays on the field.</summary>
    private const int LifeArcadeFrames = 60;

    private static readonly int Size = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.EntitySizeArcadePixels);

    /// <summary>
    ///     Which number is shown, as a place in <see cref="TextureSet.RescueBonusAnimationFrameTextures" />. The first place is the 1000
    ///     and the last is the 5000.
    /// </summary>
    private readonly int _displayIndex;

    private readonly ArcadeFrameTimer _lifeArcadeFrameTimer = new();
    private readonly IntVector2 _position;
    private readonly TextureSet _textureSet;

    /// <summary>Makes the number for one rescue.</summary>
    /// <param name="textureSet">The shared texture set.</param>
    /// <param name="position">Where the family member was rescued.</param>
    /// <param name="rescuesThisLife">
    ///     How many family members have been rescued in this life, counting this one. The number
    ///     shown never goes above 5000.
    /// </param>
    public RescueBonusMarker(TextureSet textureSet, IntVector2 position, int rescuesThisLife)
    {
        _textureSet = textureSet;
        _position = position;
        _displayIndex = Math.Clamp(rescuesThisLife, 1, ScoreValues.RescueBonusMaxCount) - 1;
    }

    /// <summary>Alive until its time on the field runs out.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the family member was rescued.</summary>
    public IntVector2 Position => _position;

    /// <summary>Draws the number of points this rescue earned.</summary>
    /// <param name="spriteBatch">What the number is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        _textureSet.BlitterDraw.DrawSprite(spriteBatch, _textureSet.RescueBonusAnimationFrameTextures[_displayIndex], GetBounds(), Color.White);
    }

    /// <summary>The box the number is drawn in.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, Size, Size);
    }

    /// <summary>Counts down the time the number has left on the field.</summary>
    /// <param name="gameTime">Not used. The time is counted in <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="playField">Not used.</param>
    public void Update(GameTime gameTime, PlayField playField)
    {
        if (_lifeArcadeFrameTimer.Tick(LifeArcadeFrames)) LifeState = EntityLifeState.Dead;
    }
}
