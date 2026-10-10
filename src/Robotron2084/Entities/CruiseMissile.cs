using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A slow, wobbly missile fired by a brain robot. It chases the player and bounces off walls, leaving a trail behind it.</summary>
/// <seealso cref="Brain"/>
/// <seealso cref="PlayerLaser"/>
/// <remarks>
/// It acts on a beat. The <see cref="PlayField"/> calls <see cref="Update"/> on every tick, through
/// <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. The one time it does not is during the
/// short freeze just after the player is killed. <see cref="_beatTimer"/> gathers the ticks until it is time for
/// the next beat (see <see cref="ArcadeClock"/>).
///
/// <list type="bullet">
/// <item>Original source: <c>RRB10.ASM</c>, routine <c>CMISL</c> (fired via <c>BRNSHT</c>, aimed by
/// <c>GCMDIR</c>, moved by <c>CMMOV</c>)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$2006</c> (<c>CREATE_CRUISE_MISSILE</c>)</item>
/// </list>
/// </remarks>
public sealed class CruiseMissile : IEntity, IRemovable
{
    /// <summary>How far to the left of the player, or above the player, the missile's aim can be moved, in port pixels. A random number below <see cref="AimNoiseRange"/> is added to where the player is, and then this is taken off.</summary>
    private const int AimNoiseBase = 6;

    /// <summary>The random number that moves the missile's aim is picked from 0 up to one less than this (see <see cref="AimNoiseBase"/>).</summary>
    private const int AimNoiseRange = 16;

    /// <summary>How long one beat lasts.</summary>
    /// <remarks>ROM: <c>NAP 2</c>, plus the one the routine runs in.</remarks>
    private const int BeatIntervalRomFrames = 3;

    /// <summary>How many moves the missile makes on each beat.</summary>
    private const int MovesPerBeat = 2;

    /// <summary>The most beats a missile may go before it aims again. <see cref="_reAimBeatsRemaining"/> is set to a random number of beats from one up to this.</summary>
    private const int ReAimMaxBeats = 7;

    /// <summary>How big the missile's box is, in port pixels. The box is used to tell what the missile touches. It sits up and to the left of the missile's own position (see <see cref="GetBounds"/>).</summary>
    /// <remarks>The disassembly calls this box "FAT PHONY GUY". It is much bigger than the dot the missile is drawn as, which is
    /// <see cref="CruiseMissileTuning.MarkArcadeWidth"/> by <see cref="CruiseMissileTuning.MarkArcadeHeight"/> arcade pixels.</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.CruiseMissileCollisionSize.Width), ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.CruiseMissileCollisionSize.Height));

    /// <summary>How far the missile goes left or right on each move, in port pixels. It is one column.</summary>
    /// <remarks>The arcade moves it one column on each move. A column is 2 arcade pixels wide.</remarks>
    private static readonly int StepXPortPixels = ScreenSize.ToPortPixelsFromColumns(1);

    /// <summary>How far the missile goes up or down on each move, in port pixels. It is one row.</summary>
    private static readonly int StepYPortPixels = ScreenSize.ToPortPixelsFromArcadePixels(1);

    private readonly Random _random;
    private readonly SpriteSet _sprites;

    /// <summary>The places the missile has just been, oldest first. A mark is drawn at each one. There are never more than <see cref="CruiseMissileTuning.TrailMarks"/>.</summary>
    private readonly List<IntVector2> _trail = new();

    private int _beatTimer;

    private IntVector2 _position;

    private int _reAimBeatsRemaining;

    private IntVector2 _velocity; // How many pixels the missile goes left or right, and how many up or down, each time it moves.

    /// <summary>Makes a missile and aims it at the player.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="origin">Where it appears.</param>
    /// <param name="playerPosition">Where the player is, which the missile aims at first.</param>
    /// <param name="random">Where its random numbers come from. They change its aim and pick how long it goes before it aims again.</param>
    /// <remarks>ROM: it starts 3 columns to the right of the brain's top-left corner and 4 rows below it.</remarks>
    public CruiseMissile(SpriteSet sprites, IntVector2 origin, IntVector2 playerPosition, Random random)
    {
        _sprites = sprites;
        _position = origin;
        _random = random;
        _velocity = RollDirection(playerPosition);
        _reAimBeatsRemaining = 1 + _random.Next(ReAimMaxBeats);
    }

    /// <summary>The box used to tell what the missile touches. It starts a little up and to the left of the missile's own position.</summary>
    /// <remarks>ROM: the "FAT PHONY GUY" box, which sits up and to the left of the missile's own position.</remarks>
    public Rectangle GetBounds() => new(
        _position.X + ScreenSize.ToPortPixelsFromColumns(CollisionSizes.CruiseMissileBoxOffsetColumns),
        _position.Y + ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.CruiseMissileBoxOffsetRows),
        CollisionSize.Width,
        CollisionSize.Height);

    /// <summary>Alive until something hits it. It does not run out of time by itself.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the missile is. Its box is worked out from this (see <see cref="GetBounds"/>).</summary>
    public IntVector2 Position => _position;

    /// <summary>The places the missile has just been, oldest first. Tests use this.</summary>
    internal IReadOnlyList<IntVector2> Trail => _trail;

    /// <summary>How many pixels the missile goes left or right, and how many up or down, each time it moves. Tests use this.</summary>
    internal IntVector2 Velocity => _velocity;

    /// <summary>The time from one beat to the next, in clock units (see <see cref="ArcadeClock"/>). <see cref="_beatTimer"/> counts up to this. When it gets there, a beat happens and this is taken off it.</summary>
    private static readonly int BeatIntervalClockUnits = ArcadeClock.ToClockUnits(BeatIntervalRomFrames);

    /// <summary>Draws the trail marks and the missile's head.</summary>
    /// <param name="spriteBatch">What the missile is drawn with.</param>
    /// <remarks>Each mark is <see cref="CruiseMissileTuning.MarkArcadeWidth"/> by
    /// <see cref="CruiseMissileTuning.MarkArcadeHeight"/> arcade pixels, as it was on the arcade screen.
    /// The trail is drawn in one palette slot and the head in another.</remarks>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        int markWidth = ScreenSize.ToPortPixelsFromArcadePixels(CruiseMissileTuning.MarkArcadeWidth);
        int markHeight = ScreenSize.ToPortPixelsFromArcadePixels(CruiseMissileTuning.MarkArcadeHeight);

        Color trailColor = _sprites.Blitter.GetSlotColour(CruiseMissileTuning.TrailSlot);
        foreach (IntVector2 mark in _trail)
        {
            _sprites.Blitter.DrawSolidRectangle(spriteBatch, new Rectangle(mark.X, mark.Y, markWidth, markHeight), trailColor);
        }

        // The missile's head is drawn as a plain dot, as in the arcade. The arcade's missile sprite is only used for its size, to tell when the missile hits something.
        _sprites.Blitter.DrawSolidRectangle(
            spriteBatch,
            new Rectangle(_position.X, _position.Y, markWidth, markHeight),
            _sprites.Blitter.GetSlotColour(CruiseMissileTuning.HeadSlot));
    }

    /// <summary>Kills the missile at once. Its trail goes with it.</summary>
    /// <remarks>ROM: <c>CMKIL</c>. Nothing is left behind.</remarks>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
        _trail.Clear();
    }

    /// <summary>Runs one tick. On a beat, the missile aims at the player again if it is time to, and then moves.</summary>
    /// <param name="gameTime">Not used. The missile counts ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive())
        {
            return;
        }

        // Wait for the missile's next beat, which is its next turn to aim and move (see ArcadeClock).
        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < BeatIntervalClockUnits)
        {
            return;
        }

        _beatTimer -= BeatIntervalClockUnits;

        // On each beat the missile first aims at the Player again, if it is time to, and then moves (ROM: CMISL).
        if (--_reAimBeatsRemaining <= 0)
        {
            _velocity = RollDirection(field.Player.Position);
            _reAimBeatsRemaining = 1 + _random.Next(ReAimMaxBeats);
        }

        for (int move = 0; move < MovesPerBeat; move++)
        {
            Step(field);
        }
    }

    /// <summary>Works out which way to go to get nearer the player, either sideways or up-and-down. The spot it aims for is moved a little at random.</summary>
    /// <param name="target">Where the player is, measured sideways or up-and-down.</param>
    /// <param name="mine">Where the missile is, measured the same way.</param>
    /// <returns>1 to go right or down, or -1 to go left or up.</returns>
    private int AimSign(int target, int mine)
    {
        int aimedCoordinate = target + _random.Next(AimNoiseRange) - AimNoiseBase;
        return aimedCoordinate >= mine ? 1 : -1;
    }

    /// <summary>Picks at random which way the missile goes until it next aims: up or down only, sideways only, or diagonally. Whichever it is, it goes towards the player.</summary>
    /// <param name="player">Where the player is.</param>
    /// <returns>How far the missile goes on each move, sideways and up or down. A zero means it does not go that way.</returns>
    /// <remarks>Half the time it goes up or down only. A quarter of the time it goes sideways only, and a quarter of the time diagonally (ROM: <c>GCMDIR</c>).</remarks>
    private IntVector2 RollDirection(IntVector2 player)
    {
        if (_random.Next(2) != 0)
        {
            return new IntVector2(0, AimSign(player.Y, _position.Y) * StepYPortPixels);
        }

        int dx = AimSign(player.X, _position.X) * StepXPortPixels;
        int dy = _random.Next(2) == 0 ? AimSign(player.Y, _position.Y) * StepYPortPixels : 0;
        return new IntVector2(dx, dy);
    }

    /// <summary>Makes one move, bouncing off the walls, and leaves a trail mark where the missile was.</summary>
    /// <param name="field">The playfield, whose walls the missile bounces off.</param>
    /// <remarks>ROM: <c>CMMOV</c>. Sideways and up-and-down bounce separately. A move that would cross a wall is
    /// turned round and made the other way. The bounce is worked out from the missile's own position, not from
    /// its bigger box.</remarks>
    private void Step(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        IntVector2 positionBeforeMove = _position;
        int columnPixels = ScreenSize.ToPortPixelsFromColumns(1);
        int rowPixels = ScreenSize.ToPortPixelsFromArcadePixels(1);

        if (_velocity.X != 0)
        {
            int x = _position.X + _velocity.X;
            if (x < bounds.X || x > bounds.Right - columnPixels)
            {
                _velocity = _velocity with { X = -_velocity.X };
                x = _position.X + _velocity.X;
            }

            _position = _position with { X = Math.Clamp(x, bounds.X, bounds.Right - columnPixels) };
        }

        if (_velocity.Y != 0)
        {
            int y = _position.Y + _velocity.Y;
            if (y < bounds.Y || y > bounds.Bottom - rowPixels)
            {
                _velocity = _velocity with { Y = -_velocity.Y };
                y = _position.Y + _velocity.Y;
            }

            _position = _position with { Y = Math.Clamp(y, bounds.Y, bounds.Bottom - rowPixels) };
        }

        // Leave a mark where the missile just was. When there are too many marks, the oldest one is taken away.
        _trail.Add(positionBeforeMove);
        if (_trail.Count > CruiseMissileTuning.TrailMarks)
        {
            _trail.RemoveAt(0);
        }
    }
}
