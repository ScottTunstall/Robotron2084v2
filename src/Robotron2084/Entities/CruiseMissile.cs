using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The missile a brain fires at the player. It does not fly straight: every so often it re-aims, and
/// each re-aim moves it on ONE axis only — or on both — so it lurches along in a drunken zigzag rather
/// than homing in. It bounces off the walls instead of leaving the field, leaves a short trail of
/// marks behind it, and is removed instantly by a laser hit (25 points).
/// </summary>
/// <remarks>
/// ROM RRB10 `BRNSHT`/`GCMDIR`/`CMISL`/`CMMOV`; the Gospel decode is in arcade-fidelity-notes (18) and
/// (46). Fired at brain + (3,4). Its body is NAP 2 (3 ROM ticks) and each body calls `CMMOV` TWICE, so
/// it travels 2 arcade px per body, one px per `CMMOV` — with the wall check repeated for each of the two
/// steps, which is what lets it turn around flush against a wall instead of overshooting into it.
///
/// `GCMDIR` gives it its odd character: X only moves when SEED's top bit is set, and Y moves unless X moved
/// AND LSEED's top bit is set — so <b>50% of re-aims are Y-only, 25% X-only and 25% diagonal</b>, and the
/// missile is never stationary. A moving component's sign follows the player's coordinate plus a
/// (seed &amp; $F) - 6 nudge — asymmetric, -6..+9, not ±6 — and ties move positive (the ROM compares with
/// `BHS`). The re-aim timer is RND(1..7) BODIES, decremented before the move.
///
/// No lifetime: it bounces until a laser takes it (25 pts, instant off — missiles have no death
/// animation). Contact with the player KILLS the player. Missiles are missiles, not robots: they keep
/// flying while <see cref="PlayField.RobotsFrozen"/> (the same convention as sparks and shells).
///
/// CMMOV never blits the CMPIC/CMP1 pictures, so they are NOT the missile's appearance. It writes VIDEO
/// MEMORY directly instead, one 16-bit word per step: `$AAAA` (two pixels of palette slot 10) at the new
/// coordinate and `$DDDD` (two of slot 13) at the one it left. The video address is column-major
/// (`column*256 + row`), so a word is two VERTICALLY adjacent pixels and the mark is 1px wide by 2px tall.
///
/// The trail is a RING OF NINE marks, not a snake: every step `CMMOV` also erases the screen pixel it wrote
/// nine steps ago, and `CMKIL` wipes the remaining nine, so the tail vanishes with the missile. (Getting this
/// wrong twice is instructive: the ring first looked like "bookkeeping for the death erase", then like an
/// unbounded video-memory history — the author's "the trail is too long" is what pointed at the erase in the
/// middle of the loop.)
/// </remarks>
public sealed class CruiseMissile : IEntity
{
    /// <summary>The collision box's size, 6x4 arcade px, in port pixels; the box itself is offset up-left.</summary>
    /// <remarks>The ROM's "FAT PHONY GUY" (`CMPIC FCB 3,4` = 6x4 px), offset up-left.</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.CruiseMissileCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.CruiseMissileCollisionSize.Height));

    /// <summary>
    /// How far the missile steps along X per move, in port pixels: two arcade pixels (one whole
    /// column) — twice the Y step.
    /// </summary>
    /// <remarks>
    /// ROM CMMOV step on X: `CMMV1 ADDA PD2,U` adds to the X COLUMN byte, so ONE
    /// COLUMN per call — **2 arcade px**, not the 1 the port moved (notes §51).
    /// </remarks>
    private static readonly int StepColumns = ScreenSize.Scaled(2);

    /// <summary>
    /// How far the missile steps along Y per move, in port pixels: one arcade pixel. There is
    /// no halving like the player's or the tank's, so the missile really is twice as fast
    /// horizontally as vertically.
    /// </summary>
    /// <remarks>
    /// ROM CMMOV step on Y: `ADDB PD2+1,U` adds to the Y ROW byte, so ONE ROW per
    /// call — 1 arcade px. There is NO halving like the player's or the tank's, so
    /// the missile really is twice as fast horizontally as vertically.
    /// </remarks>
    private static readonly int StepRows = ScreenSize.Scaled(1);

    /// <summary>How many ROM frames one body takes: the body's own execution plus its sleep.</summary>
    /// <remarks>ROM body: NAP 2 plus the body execution vblank.</remarks>
    private const int BodyPeriodRomTicks = 3;

    /// <summary>The body period in exact sixths of a port tick: 3 ROM frames = 3.6 ticks.</summary>
    /// <remarks>Notes §52, §65.</remarks>
    private static int BodyFifths => BodyPeriodRomTicks * 6;

    /// <summary>How many moves the missile makes per body: twice.</summary>
    /// <remarks>ROM CMMOV calls per body.</remarks>
    private const int MovesPerBody = 2;

    /// <summary>The re-aim timer's upper bound, in bodies: the timer is rolled from 1 to this.</summary>
    /// <remarks>ROM GCMDIR: re-aim timer = RND(1..7) bodies.</remarks>
    private const int ReAimMaxBodies = 7;

    /// <summary>Subtracted from each aim roll, making the nudge -6..+9.</summary>
    /// <remarks>ROM GCMDIR aim nudge: (seed &amp; $F) - 6, i.e. -6..+9.</remarks>
    private const int AimNoiseBase = 6;
    /// <summary>How many values an aim roll draws from (0..this-1).</summary>
    private const int AimNoiseRange = 16;

    private readonly Random _random;

    /// <summary>
    /// The rolling tail: up to <see cref="GameplayConstants.MissileTrailMarks"/>
    /// positions, oldest first.
    /// </summary>
    /// <remarks>The ROM's ring of coordinates, whose oldest entry it erases off the screen
    /// every step.</remarks>
    private readonly List<IntVector2> _trail = new();

    private IntVector2 _position;
    private IntVector2 _velocity; // per-axis ±StepColumns/±StepRows (0 = axis inactive this re-aim)
    private int _bodyFifths;
    private int _reAimBodiesRemaining;

    /// <summary>Fires a missile, with its first direction already rolled.</summary>
    /// <param name="origin">Where it appears.</param>
    /// <param name="playerPosition">The player's position, used for the first aim.</param>
    /// <param name="random">The random source for the aim and the re-aim timer.</param>
    /// <remarks>ROM: fired at brain + (3,4) arcade px.</remarks>
    public CruiseMissile(IntVector2 origin, IntVector2 playerPosition, Random random)
    {
        _position = origin;
        _random = random;
        _velocity = RollDirection(playerPosition);
        _reAimBodiesRemaining = 1 + _random.Next(ReAimMaxBodies);
    }

    /// <summary>The missile's TRUE coordinate (the fat box below is derived from it).</summary>
    public IntVector2 Position => _position;

    /// <summary>
    /// The collision box: the missile's true corner shifted one pixel up and left, because the box is
    /// bigger than the point the missile tracks.
    /// </summary>
    /// <remarks>The ROM's "FAT PHONY GUY" box (`CMPIC FCB 3,4` = 6x4 px), offset up-left (notes §51).</remarks>
    public Rectangle Bounds => new(
        _position.X + ScreenSize.Scaled(GameplayConstants.CruiseMissileBoxOffsetColumns * 2),
        _position.Y + ScreenSize.Scaled(GameplayConstants.CruiseMissileBoxOffsetRows),
        CollisionSize.Width,
        CollisionSize.Height);

    /// <summary>Alive until a laser takes it (it has no life timer of its own).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>
    /// Removes the missile instantly — there is no death animation — and wipes its trail with it. The
    /// field awards the 25 points.
    /// </summary>
    /// <remarks>ROM `CMKIL`: wipes the ring's remaining nine marks off-screen ("instant off").</remarks>
    public void Destroy()
    {
        LifeState = EntityLifeState.Dead;
        _trail.Clear();
    }

    /// <summary>
    /// Runs one body when its 3-frame clock says so: re-aim if the timer has run out, then take the
    /// body's two steps — each marking the position it leaves and dropping the ring's oldest mark.
    /// </summary>
    /// <param name="gameTime">Unused — the body is counted in ROM frames.</param>
    /// <param name="field">The playfield: the player it aims at and the wall it bounces off.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        // ROM body: 3 ROM frames = 3.6 port ticks (exact 6ths, notes §52), not
        // the truncated PortTicks(3) = 3 that made the missile 20% fast.
        _bodyFifths += 5;
        if (_bodyFifths < BodyFifths)
        {
            return;
        }

        _bodyFifths -= BodyFifths;

        // ROM CMISL: the re-aim timer runs out first, then two CMMOVs.
        if (--_reAimBodiesRemaining <= 0)
        {
            _velocity = RollDirection(field.Player.Position);
            _reAimBodiesRemaining = 1 + _random.Next(ReAimMaxBodies);
        }

        for (int move = 0; move < MovesPerBody; move++)
        {
            Step(field);
        }
    }

    /// <summary>
    /// Takes one step: moves up to one column on X and one row on Y, bouncing per axis off the
    /// playfield. A component that would leave the field is negated and the step taken the other
    /// way, so the missile turns around on the boundary rather than sticking to it. The position
    /// it leaves is added to the trail.
    /// </summary>
    /// <remarks>
    /// ROM CMMOV: one COLUMN on X and one ROW on Y, with a per-axis bounce off the
    /// playfield. The direction component is negated and the step taken the other
    /// way, so the missile turns around on the boundary rather than sticking to it.
    /// The position it leaves is marked in the trail colour.
    ///
    /// The bounce tests the TRUE COORDINATE (`CMPA #XMIN` / `#XMAX-1`, `CMPB #YMIN`
    /// / `#YMAX`) — the fat `OBJX` box is for collisions only, so it does NOT
    /// overhang the wall here. The port used to bound with the box.
    /// </remarks>
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

        // ROM CMMOV: `LDY #$DDDD / STY [OX16,X]` — the position just left gets
        // the trail mark (two pixels of slot 13), and the ring entry nine steps
        // back is erased in the same pass (`LDY #0 / STY [A,U]`), so the tail
        // never exceeds the ring.
        _trail.Add(leaving);
        if (_trail.Count > GameplayConstants.MissileTrailMarks)
        {
            _trail.RemoveAt(0);
        }
    }

    /// <summary>Test hook: the rolling tail, oldest first.</summary>
    internal IReadOnlyList<IntVector2> Trail => _trail;

    /// <summary>
    /// Rolls the direction for the next stretch of flight: Y only half the time; otherwise X, and Y as
    /// well half of the remaining half. The missile is never stationary.
    /// </summary>
    /// <param name="player">The player's position, which each active axis aims at.</param>
    /// <returns>The per-step velocity on each axis; a zero component means that axis is idle.</returns>
    /// <remarks>
    /// ROM `GCMDIR` — and its control flow is a little trap: <c>LDA SEED / BPL GCMDY</c> jumps to the START
    /// of the Y block, so when SEED's top bit is clear the missile skips the X block AND the LSEED test as
    /// well, meaning it always seeks on Y in that case. Only when X is armed does LSEED get a say, and a set
    /// top bit then drops Y. Net: Y-only 50%, X-only 25%, diagonal 25% — Y is the missile's favoured axis.
    /// </remarks>
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

    /// <summary>Aims one axis: the nudged roll is added to the target's coordinate and compared with
    /// this object's own coordinate — the sign to move in.</summary>
    /// <remarks>ROM: (coord + (seed &amp; $F) - 6) >= mine ? +1 : -1.</remarks>
    private int AimSign(int target, int mine)
    {
        int aim = target + _random.Next(AimNoiseRange) - AimNoiseBase;
        return aim >= mine ? 1 : -1;
    }

    /// <summary>The step the missile takes on each axis right now (0 = that axis is idle this re-aim).</summary>
    internal IntVector2 Velocity => _velocity;

    /// <summary>Draws the trail marks and the missile's head.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which supplies the two slots' live colours.</param>
    /// <remarks>ROM `CMMOV`: `LDD #$AAAA / LDY OX16,X / STD ,Y` — a 16-bit video write, so the mark is
    /// 1 arcade px wide and 2 TALL (column-major memory). The trail marks are slot 13 and the head slot 10.</remarks>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        // ROM CMMOV: `LDD #$AAAA / LDY OX16,X / STD ,Y` — a 16-bit video write,
        // i.e. the mark is 1 arcade px wide and 2 TALL (column-major memory).
        int markWidth = ScreenSize.Scaled(GameplayConstants.MissileMarkArcadeWidth);
        int markHeight = ScreenSize.Scaled(GameplayConstants.MissileMarkArcadeHeight);

        Color trailColor = sprites.SlotColor(GameplayConstants.MissileTrailSlot);
        foreach (IntVector2 mark in _trail)
        {
            sprites.DrawSolidRectangle(spriteBatch, new Rectangle(mark.X, mark.Y, markWidth, markHeight), trailColor);
        }

        // The CMPIC/CMP1 pictures are only ever the collision/erase descriptors.
        sprites.DrawSolidRectangle(
            spriteBatch,
            new Rectangle(_position.X, _position.Y, markWidth, markHeight),
            sprites.SlotColor(GameplayConstants.MissileHeadSlot));
    }
}
