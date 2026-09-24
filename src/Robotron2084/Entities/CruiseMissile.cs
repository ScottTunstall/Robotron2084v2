using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The missile a brain fires: a slow, lurching homing shot that bounces off the walls.</summary>
/// <seealso cref="Brain"/>
/// <seealso cref="PlayerLaser"/>
/// <remarks>ROM: RRB10.ASM's <c>BRNSHT</c>/<c>GCMDIR</c>/<c>CMISL</c>/<c>CMMOV</c> (notes §18). It
/// starts just below and right of the brain. Each beat it re-aims (timer 1..7 beats) and then takes
/// two one-pixel steps, checking the bounce after each — which is what lets it turn flush against a
/// wall. A re-aim is Y-only half the time, X-only a quarter and both axes a quarter; each active axis
/// aims at the player's coordinate plus a random -6..+9 nudge. It has no lifetime of its own: only
/// being hit removes it, leaving no animation, and it keeps flying while the robots are frozen. Its
/// trail is a ring of the 9 most recent positions — each step erases the mark from 9 steps ago —
/// drawn as solid rectangles 1 arcade px wide and 2 tall, because the arcade's own video-memory write
/// coloured two stacked pixels; the ROM's missile picture exists only to define the collision box.
/// Timers count 5 per tick and 6 per arcade frame, so an interval of N frames is due at 6 x N.
/// </remarks>
public sealed class CruiseMissile : IEntity, IRemovable
{
    private readonly SpriteSet _sprites;    /// <summary>The collision box's size, 6x4 arcade px, in port pixels; the box itself is offset up-left.</summary>
    /// <remarks>The disassembly labels this hitbox "FAT PHONY GUY" — far bigger than the missile's
    /// 1x2px visible mark, and offset up and left of the tracked point.</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.CruiseMissileCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.CruiseMissileCollisionSize.Height));

    /// <summary>How far the missile steps along X per move, in port pixels — two arcade px.</summary>
    /// <remarks>The arcade moves it one video-memory column per step; a column is 2 arcade px wide.</remarks>
    private static readonly int StepColumns = ScreenSize.Scaled(2);

    /// <summary>How far the missile steps along Y per move, in port pixels — one arcade px.</summary>
    private static readonly int StepRows = ScreenSize.Scaled(1);

    /// <summary>How many ROM frames one beat takes (NAP 2 plus the execution vblank).</summary>
    private const int BeatPeriodRomTicks = 3;

    /// <summary>How many timer units between beats (a tick adds 5; an arcade frame is 6 units).</summary>
    private static int BeatPeriod => BeatPeriodRomTicks * 6;

    /// <summary>How many moves the missile makes per beat.</summary>
    private const int MovesPerBeat = 2;

    /// <summary>The re-aim timer's upper bound, in beats (rolled 1..this).</summary>
    private const int ReAimMaxBeats = 7;

    /// <summary>Subtracted from each aim roll, making the random nudge run -6..+9.</summary>
    private const int AimNoiseBase = 6;
    /// <summary>How many values an aim roll draws from.</summary>
    private const int AimNoiseRange = 16;

    private readonly Random _random;

    /// <summary>The rolling tail of up to <see cref="GameplayConstants.MissileTrailMarks"/> positions, oldest first.</summary>
    private readonly List<IntVector2> _trail = new();

    private IntVector2 _position;
    private IntVector2 _velocity; // Current per-step move on each axis: ±StepColumns / ±StepRows, or 0 if that axis is idle this re-aim.
    private int _beatTimer;
    private int _reAimBeatsRemaining;

    /// <summary>Fires a missile, with its first direction already rolled.</summary>
    /// <param name="origin">Where it appears.</param>
    /// <param name="playerPosition">The player's position, used for the first aim.</param>
    /// <param name="random">The random source for the aim and the re-aim timer.</param>
    /// <remarks>ROM: fired at brain + (3,4) arcade px.</remarks>
    public CruiseMissile(SpriteSet sprites, IntVector2 origin, IntVector2 playerPosition, Random random)
    {
        _sprites = sprites;
        _position = origin;
        _random = random;
        _velocity = RollDirection(playerPosition);
        _reAimBeatsRemaining = 1 + _random.Next(ReAimMaxBeats);
    }

    /// <summary>The missile's true coordinate (the collision box is derived from it).</summary>
    public IntVector2 Position => _position;

    /// <summary>The collision box: the tracked point shifted one pixel up and left.</summary>
    /// <remarks>ROM: the "FAT PHONY GUY" hitbox, offset up and left of the tracked point.</remarks>
    public Rectangle Bounds => new(
        _position.X + ScreenSize.Scaled(GameplayConstants.CruiseMissileBoxOffsetColumns * 2),
        _position.Y + ScreenSize.Scaled(GameplayConstants.CruiseMissileBoxOffsetRows),
        CollisionSize.Width,
        CollisionSize.Height);

    /// <summary>Alive until it is hit (it has no life timer of its own).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Removes the missile instantly and wipes its trail with it.</summary>
    /// <remarks>ROM: <c>CMKIL</c> — nothing is left behind.</remarks>
    public void Kill()
    {
        LifeState = EntityLifeState.Dead;
        _trail.Clear();
    }

    /// <summary>Runs one beat: re-aim if due, then take the beat's two steps.</summary>
    /// <param name="gameTime">Unused — the beat is counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        // Counts up to the next beat: 5 per tick, 6 per arcade frame.
        _beatTimer += 5;
        if (_beatTimer < BeatPeriod)
        {
            return;
        }

        _beatTimer -= BeatPeriod;

        // The re-aim timer is checked first, then the two steps for this beat are taken (ROM: `CMISL`).
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

    /// <summary>Takes one step, bouncing each axis off the playfield, and marks the position left.</summary>
    /// <remarks>ROM: <c>CMMOV</c> — each axis bounces independently: a step crossing the boundary
    /// flips that axis and is retaken the other way. The bounce uses the true tracked point, not the
    /// oversized hitbox.</remarks>
    private void Step(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        IntVector2 leaving = _position;
        int columnPixels = ScreenSize.Scaled(2);
        int rowPixels = ScreenSize.Scaled(1);

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

        // A trail mark for the position just left, then drop the oldest (the tail is 9 marks).
        _trail.Add(leaving);
        if (_trail.Count > GameplayConstants.MissileTrailMarks)
        {
            _trail.RemoveAt(0);
        }
    }

    /// <summary>Test hook: the rolling tail, oldest first.</summary>
    internal IReadOnlyList<IntVector2> Trail => _trail;

    /// <summary>Rolls the next stretch's direction: Y only half the time, else X (and maybe Y too).</summary>
    /// <param name="player">The player's position, which each active axis aims at.</param>
    /// <returns>The per-step velocity on each axis; a zero component means that axis is idle.</returns>
    /// <remarks>Y-only 50%, X-only 25%, diagonal 25% — vertical is the favoured axis (ROM: <c>GCMDIR</c>).</remarks>
    private IntVector2 RollDirection(IntVector2 player)
    {
        if (_random.Next(2) != 0)
        {
            return new IntVector2(0, AimSign(player.Y, _position.Y) * StepRows);
        }

        int dx = AimSign(player.X, _position.X) * StepColumns;
        int dy = _random.Next(2) == 0 ? AimSign(player.Y, _position.Y) * StepRows : 0;
        return new IntVector2(dx, dy);
    }

    /// <summary>Aims one axis: the sign to move in, from a nudged target coordinate.</summary>
    private int AimSign(int target, int mine)
    {
        int aim = target + _random.Next(AimNoiseRange) - AimNoiseBase;
        return aim >= mine ? 1 : -1;
    }

    /// <summary>The step the missile takes on each axis right now (0 = that axis is idle this re-aim).</summary>
    internal IntVector2 Velocity => _velocity;

    /// <summary>Draws the trail marks and the missile's head.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set.</param>
    /// <remarks>Each mark is 1 arcade px wide and 2 tall, as the hardware's video writes produced.
    /// The trail uses one palette slot and the head another.</remarks>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        int markWidth = ScreenSize.Scaled(GameplayConstants.MissileMarkArcadeWidth);
        int markHeight = ScreenSize.Scaled(GameplayConstants.MissileMarkArcadeHeight);

        Color trailColor = _sprites.SlotColor(GameplayConstants.MissileTrailSlot);
        foreach (IntVector2 mark in _trail)
        {
            _sprites.DrawSolidRectangle(spriteBatch, new Rectangle(mark.X, mark.Y, markWidth, markHeight), trailColor);
        }

        // The ROM's missile picture only defines the collision box; the head is a solid dot.
        _sprites.DrawSolidRectangle(
            spriteBatch,
            new Rectangle(_position.X, _position.Y, markWidth, markHeight),
            _sprites.SlotColor(GameplayConstants.MissileHeadSlot));
    }
}
