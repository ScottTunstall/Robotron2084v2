using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
///     A slow, wobbly missile fired by a brain robot. It chases the player and bounces off walls, leaving a trail
///     behind it.
/// </summary>
/// <seealso cref="Brain" />
/// <seealso cref="PlayerLaser" />
/// <remarks>
///     It acts on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. <see cref="_beatTimer" /> says when it is time for the next <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <list type="bullet">
///         <item>
///             Original source: <c>RRB10.ASM</c>, routine <c>CMISL</c> (fired via <c>BRNSHT</c>, steered by
///             <c>GCMDIR</c>, moved by <c>CMMOV</c>)
///         </item>
///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>$2006</c> (<c>CREATE_CRUISE_MISSILE</c>)</item>
///     </list>
/// </remarks>
public sealed class CruiseMissile : IEntity, IRemovable
{
    /// <summary>How long one <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> lasts.</summary>
    /// <remarks>ROM: <c>NAP 2</c>, plus the one the routine runs in.</remarks>
    private const int BeatIntervalArcadeFrames = 3;

    /// <summary>
    ///     How far to the left of the player, or above the player, the spot the missile heads for can be moved, in port pixels.
    /// </summary>
    private const int DirectionNoiseBase = 6;

    /// <summary>How wide the range is that the spot the missile heads for can be moved across, in port pixels.</summary>
    private const int DirectionNoiseRange = 16;

    /// <summary>How many moves the missile makes on each <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.</summary>
    private const int MovesPerBeat = 2;

    /// <summary>
    ///     The most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> a missile may go before it picks a new direction.
    /// </summary>
    private const int NewDirectionMaxBeats = 7;

    /// <summary>
    ///     How big the missile's box is, in port pixels. The box sits up and to the left of the missile's own position.
    /// </summary>
    /// <remarks>
    ///     The disassembly calls this box "FAT PHONY GUY". It is much bigger than the dot the missile is drawn as, which is
    ///     <see cref="CruiseMissileTuning.MarkArcadeWidth" /> by <see cref="CruiseMissileTuning.MarkArcadeHeight" /> arcade
    ///     pixels.
    /// </remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.CruiseMissileCollisionSize.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.CruiseMissileCollisionSize.Height));

    /// <summary>How far the missile goes left or right on each move, in port pixels. It is one column.</summary>
    /// <remarks>The arcade moves it one column on each move. A column is 2 arcade pixels wide.</remarks>
    private static readonly int StepXPortPixels = ScreenSize.ToPortPixelsFromColumns(1);

    /// <summary>How far the missile goes up or down on each move, in port pixels. It is one row.</summary>
    private static readonly int StepYPortPixels = ScreenSize.ToPortPixelsFromArcadePixels(1);

    private readonly ArcadeFrameTimer _beatArcadeFrameTimer = new();
    private readonly Random _random;
    private readonly SpriteSet _spriteSet;

    /// <summary>
    ///     The places the missile has just been, oldest first. There are never more than
    ///     <see cref="CruiseMissileTuning.TrailMarks" />.
    /// </summary>
    private readonly List<IntVector2> _trail = new();

    private int _newDirectionBeatsRemaining;
    private IntVector2 _position;
    private IntVector2 _velocity; // How many pixels the missile goes left or right, and how many up or down, each time it moves.

    /// <summary>Makes a missile and sends it towards the player.</summary>
    /// <param name="spriteSet">The shared sprite set.</param>
    /// <param name="origin">Where it appears.</param>
    /// <param name="playerPosition">Where the player is, which the missile heads for first.</param>
    /// <param name="random">
    ///     Where its random numbers come from. They pick which way it goes, and how long it goes before it picks a new direction.
    /// </param>
    /// <remarks>ROM: it starts 3 columns to the right of the brain's top-left corner and 4 rows below it.</remarks>
    public CruiseMissile(SpriteSet spriteSet, IntVector2 origin, IntVector2 playerPosition, Random random)
    {
        _spriteSet = spriteSet;
        _position = origin;
        _random = random;
        _velocity = GetRandomDirection(playerPosition);
        _newDirectionBeatsRemaining = ArcadeRandom.PickUpTo(_random, NewDirectionMaxBeats);
    }

    /// <summary>Alive until something hits it. It does not run out of time by itself.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the missile is. Its box is worked out from this (see <see cref="GetBounds" />).</summary>
    public IntVector2 Position => _position;

    /// <summary>The places the missile has just been, oldest first.</summary>
    internal IReadOnlyList<IntVector2> Trail => _trail;

    /// <summary>How many pixels the missile goes left or right, and how many up or down, each time it moves.</summary>
    internal IntVector2 Velocity => _velocity;

    /// <summary>Draws the trail marks and the missile's head.</summary>
    /// <param name="spriteBatch">What the missile is drawn with.</param>
    /// <remarks>
    ///     Each mark is <see cref="CruiseMissileTuning.MarkArcadeWidth" /> by
    ///     <see cref="CruiseMissileTuning.MarkArcadeHeight" /> arcade pixels, as it was on the arcade screen.
    ///     The trail is drawn in one palette index and the head in another.
    /// </remarks>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        var markWidth = ScreenSize.ToPortPixelsFromArcadePixels(CruiseMissileTuning.MarkArcadeWidth);
        var markHeight = ScreenSize.ToPortPixelsFromArcadePixels(CruiseMissileTuning.MarkArcadeHeight);

        var trailColor = _spriteSet.BlitterDraw.GetPaletteIndexColour(CruiseMissileTuning.TrailPaletteIndex);
        foreach (var mark in _trail)
            _spriteSet.BlitterDraw.DrawSolidRectangle(spriteBatch, new Rectangle(mark.X, mark.Y, markWidth, markHeight),
                trailColor);

        // The missile's head is drawn as a plain dot, as in the arcade. The arcade's missile sprite is only used for its size, to tell when the missile hits something.
        _spriteSet.BlitterDraw.DrawSolidRectangle(
            spriteBatch,
            new Rectangle(_position.X, _position.Y, markWidth, markHeight),
            _spriteSet.BlitterDraw.GetPaletteIndexColour(CruiseMissileTuning.HeadPaletteIndex));
    }

    /// <summary>
    ///     The box the missile takes up on the screen. It starts a little up and to the left of the missile's own
    ///     position.
    /// </summary>
    /// <remarks>ROM: the "FAT PHONY GUY" box, which sits up and to the left of the missile's own position.</remarks>
    public Rectangle GetBounds()
    {
        return new Rectangle(
            _position.X + ScreenSize.ToPortPixelsFromColumns(CollisionSizes.CruiseMissileBoxOffsetColumns),
            _position.Y + ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.CruiseMissileBoxOffsetRows),
            CollisionSize.Width,
            CollisionSize.Height);
    }

    /// <summary>Kills the missile at once. Its trail goes with it.</summary>
    /// <remarks>ROM: <c>CMKIL</c>. Nothing is left behind.</remarks>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
        _trail.Clear();
    }

    /// <summary>Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. On a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>, the missile turns towards the player again if it is time to, and then moves.</summary>
    /// <param name="gameTime">Not used. The missile counts <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="playField">The playfield.</param>
    public void Update(GameTime gameTime, PlayField playField)
    {
        if (!this.IsAlive()) return;

        if (_beatArcadeFrameTimer.Tick(BeatIntervalArcadeFrames)) ActOnBeat(playField);
    }

    /// <summary>
    ///     What the missile does on its <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. It first turns towards the player again, if it is time to, and then moves
    ///     (ROM: <c>CMISL</c>).
    /// </summary>
    /// <param name="playField">The playfield.</param>
    private void ActOnBeat(PlayField playField)
    {
        if (--_newDirectionBeatsRemaining <= 0)
        {
            _velocity = GetRandomDirection(playField.Player.Position);
            _newDirectionBeatsRemaining = ArcadeRandom.PickUpTo(_random, NewDirectionMaxBeats);
        }

        for (var move = 0; move < MovesPerBeat; move++) MoveOnce(playField);
    }

    /// <summary>
    ///     Picks at random which way the missile goes until it next picks a new direction: up or down only, sideways only, or diagonally.
    ///     Whichever it is, it goes towards the player.
    /// </summary>
    /// <param name="playerPosition">Where the player is.</param>
    /// <returns>How far the missile goes on each move, sideways and up or down. A zero means it does not go that way.</returns>
    /// <remarks>
    ///     Half the time it goes up or down only. A quarter of the time it goes sideways only, and a quarter of the time
    ///     diagonally (ROM: <c>GCMDIR</c>).
    /// </remarks>
    private IntVector2 GetRandomDirection(IntVector2 playerPosition)
    {
        if (_random.Next(2) != 0) return new IntVector2(0, GetRandomDirectionSign(playerPosition.Y, _position.Y) * StepYPortPixels);

        var dx = GetRandomDirectionSign(playerPosition.X, _position.X) * StepXPortPixels;
        var dy = 0;
        var isMovingDiagonally = _random.Next(2) == 0;
        if (isMovingDiagonally) dy = GetRandomDirectionSign(playerPosition.Y, _position.Y) * StepYPortPixels;

        return new IntVector2(dx, dy);
    }

    /// <summary>
    ///     Works out which way to go to get nearer the player, either sideways or up-and-down. The spot it heads for is
    ///     moved a little at random.
    /// </summary>
    /// <param name="target">Where the player is, measured sideways or up-and-down.</param>
    /// <param name="mine">Where the missile is, measured the same way.</param>
    /// <returns>1 to go right or down, or -1 to go left or up.</returns>
    private int GetRandomDirectionSign(int target, int mine)
    {
        var noisyTargetCoordinate = target + _random.Next(DirectionNoiseRange) - DirectionNoiseBase;
        return noisyTargetCoordinate >= mine ? 1 : -1;
    }

    /// <summary>Makes one move, bouncing off the walls, and leaves a trail mark where the missile was.</summary>
    /// <param name="playField">The playfield, whose walls the missile bounces off.</param>
    /// <remarks>
    ///     ROM: <c>CMMOV</c>. Sideways and up-and-down bounce separately. A move that would cross a wall is
    ///     turned round and made the other way. The bounce is worked out from the missile's own position, not from
    ///     its bigger box.
    /// </remarks>
    private void MoveOnce(PlayField playField)
    {
        var bounds = playField.PlayfieldWall.PlayfieldBounds;
        var positionBeforeMove = _position;
        var columnPixels = ScreenSize.ToPortPixelsFromColumns(1);
        var rowPixels = ScreenSize.ToPortPixelsFromArcadePixels(1);

        if (_velocity.X != 0)
        {
            var x = _position.X + _velocity.X;
            if (x < bounds.X || x > bounds.Right - columnPixels)
            {
                _velocity = _velocity with { X = -_velocity.X };
                x = _position.X + _velocity.X;
            }

            _position = _position with { X = Math.Clamp(x, bounds.X, bounds.Right - columnPixels) };
        }

        if (_velocity.Y != 0)
        {
            var y = _position.Y + _velocity.Y;
            if (y < bounds.Y || y > bounds.Bottom - rowPixels)
            {
                _velocity = _velocity with { Y = -_velocity.Y };
                y = _position.Y + _velocity.Y;
            }

            _position = _position with { Y = Math.Clamp(y, bounds.Y, bounds.Bottom - rowPixels) };
        }

        // Leave a mark where the missile just was. When there are too many marks, the oldest one is taken away.
        _trail.Add(positionBeforeMove);
        if (_trail.Count > CruiseMissileTuning.TrailMarks) _trail.RemoveAt(0);
    }
}
