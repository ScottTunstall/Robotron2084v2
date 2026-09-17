using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A cruise missile — the BRAIN's weapon (ROM RRB10 BRNSHT/GCMDIR/CMISL/CMMOV;
/// the Gospel decode is in arcade-fidelity-notes (18) and (46)). Fired at
/// brain + (3,4). Its body is NAP 2 (3 ROM ticks) and each body calls CMMOV
/// TWICE, so it travels <b>2 arcade px per body, one px per CMMOV</b> — with
/// the wall check repeated for each of the two steps, which is what lets it
/// turn around flush against a wall instead of overshooting into it.
///
/// GCMDIR gives it its odd, drunken character: X only moves when SEED's top
/// bit is set, and Y moves unless X moved AND LSEED's top bit is set — so
/// <b>50% of re-aims are Y-only, 25% X-only and 25% diagonal</b>, and the
/// missile is never stationary. A moving component's sign follows the
/// player's coordinate plus a (seed &amp; $F) - 6 nudge — asymmetric, -6..+9,
/// NOT ±6 — and ties move positive (the ROM compares with BHS). The re-aim
/// timer is RND(1..7) BODIES, decremented before the move.
///
/// No lifetime: it bounces until a laser takes it (25 pts, instant off —
/// missiles have no death animation). Contact with the player KILLS the
/// player. Missiles are missiles, not robots: they keep flying while
/// <see cref="PlayField.RobotsFrozen"/> (same convention as sparks/shells).
///
/// Not modelled: nothing — see below. CMMOV never blits the CMPIC/CMP1
/// pictures, so they are NOT the missile's appearance. It writes VIDEO MEMORY
/// directly instead, one 16-bit word per step: `$AAAA` (two pixels of palette
/// slot 10) at the new coordinate and `$DDDD` (two of slot 13) at the one it
/// left. The video address is column-major (`column*256 + row`), so a word is
/// two VERTICALLY adjacent pixels and the mark is 1px wide by 2px tall.
///
/// The trail is a RING OF NINE marks, not a snake: every step CMMOV also
/// erases the screen pixel it wrote nine steps ago, and CMKIL wipes the
/// remaining nine, so the tail vanishes with the missile. (Getting this wrong
/// twice is instructive: the ring first looked like "bookkeeping for the death
/// erase", then like an unbounded video-memory history — the author's "the
/// trail is too long" is what pointed at the erase in the middle of the loop.)
/// </summary>
public sealed class CruiseMissile : IEntity
{
    /// <summary>Collision box = the ROM "FAT PHONY GUY" (`CMPIC FCB 3,4` = 6x4 px), offset up-left.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.CruiseMissileCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.CruiseMissileCollisionSize.Height));

    /// <summary>
    /// ROM CMMOV step on X: `CMMV1 ADDA PD2,U` adds to the X COLUMN byte, so ONE
    /// COLUMN per call — **2 arcade px**, not the 1 the port moved (notes §51).
    /// </summary>
    private static readonly int StepColumns = ScreenSize.Scaled(2);

    /// <summary>
    /// ROM CMMOV step on Y: `ADDB PD2+1,U` adds to the Y ROW byte, so ONE ROW per
    /// call — 1 arcade px. There is NO halving like the player's or the tank's, so
    /// the missile really is twice as fast horizontally as vertically.
    /// </summary>
    private static readonly int StepRows = ScreenSize.Scaled(1);

    /// <summary>ROM body: NAP 2 plus the body execution vblank.</summary>
    private const int BodyPeriodRomTicks = 3;

    /// <summary>The body period in exact 6ths (notes §52, §65): 3 ROM frames = 3.6 ticks.</summary>
    private static int BodyFifths => BodyPeriodRomTicks * 6;

    /// <summary>ROM CMMOV calls per body (the missile moves twice per body).</summary>
    private const int MovesPerBody = 2;

    /// <summary>ROM GCMDIR: re-aim timer = RND(1..7) bodies.</summary>
    private const int ReAimMaxBodies = 7;

    /// <summary>ROM GCMDIR aim nudge: (seed &amp; $F) - 6, i.e. -6..+9.</summary>
    private const int AimNoiseBase = 6;
    private const int AimNoiseRange = 16;

    private readonly Random _random;

    /// <summary>
    /// The rolling tail: up to <see cref="GameplayConstants.MissileTrailMarks"/>
    /// positions, oldest first. The ROM's ring of coordinates, whose oldest
    /// entry it erases off the screen every step.
    /// </summary>
    private readonly List<IntVector2> _trail = new();

    private IntVector2 _position;
    private IntVector2 _velocity; // per-axis ±StepColumns/±StepRows (0 = axis inactive this re-aim)
    private int _bodyFifths;
    private int _reAimBodiesRemaining;

    /// <param name="origin">Fire point (brain + (3,4) arcade px).</param>
    /// <param name="playerPosition">For the initial aim bias.</param>
    public CruiseMissile(IntVector2 origin, IntVector2 playerPosition, Random random)
    {
        _position = origin;
        _random = random;
        _velocity = RollDirection(playerPosition);
        _reAimBodiesRemaining = 1 + _random.Next(ReAimMaxBodies);
    }

    public IntVector2 Position => _position;

    /// <summary>
    /// The FAT box, not the coordinate: CMMOV computes it as the true coordinate
    /// minus `#$0101`, i.e. one COLUMN and one ROW up-left (notes §51).
    /// </summary>
    public Rectangle Bounds => new(
        _position.X + ScreenSize.Scaled(GameplayConstants.CruiseMissileBoxOffsetColumns * 2),
        _position.Y + ScreenSize.Scaled(GameplayConstants.CruiseMissileBoxOffsetRows),
        CollisionSize.Width,
        CollisionSize.Height);

    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>
    /// Laser hit: instant off (no death animation), the field awards the 25.
    /// ROM CMKIL also wipes the remaining trail marks — the ring's nine
    /// coordinates are all written off-screen — so the tail goes with it.
    /// </summary>
    public void Destroy()
    {
        LifeState = EntityLifeState.Dead;
        _trail.Clear();
    }

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
    /// ROM CMMOV: one COLUMN on X and one ROW on Y, with a per-axis bounce off the
    /// playfield. The direction component is negated and the step taken the other
    /// way, so the missile turns around on the boundary rather than sticking to it.
    /// The position it leaves is marked in the trail colour.
    ///
    /// The bounce tests the TRUE COORDINATE (`CMPA #XMIN` / `#XMAX-1`, `CMPB #YMIN`
    /// / `#YMAX`) — the fat `OBJX` box is for collisions only, so it does NOT
    /// overhang the wall here. The port used to bound with the box.
    /// </summary>
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
    /// ROM GCMDIR. The control flow is a little trap: <c>LDA SEED / BPL GCMDY</c>
    /// jumps to the START of the Y block, so when SEED's top bit is clear the
    /// missile skips the X block <b>and the LSEED test as well</b> — it always
    /// seeks on Y in that case. Only when X is armed does LSEED get a say, and
    /// a set top bit then drops Y. Net: <b>Y-only 50%, X-only 25%,
    /// diagonal 25%, never stationary</b> — Y is the missile's favoured axis.
    /// An armed axis's sign is
    /// <c>(playerCoord + ((seed &amp; $F) - 6)) &gt;= myCoord ? +1 : -1</c> — the
    /// noise nudges the PLAYER's coordinate before the compare (it is -6..+9,
    /// not symmetric), and a tie moves positive (the ROM compares with BHS).
    /// </summary>
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

    /// <summary>ROM: (coord + (seed &amp; $F) - 6) >= mine ? +1 : -1.</summary>
    private int AimSign(int target, int mine)
    {
        int aim = target + _random.Next(AimNoiseRange) - AimNoiseBase;
        return aim >= mine ? 1 : -1;
    }

    /// <summary>The current per-CMMOV step on each axis (0 = that axis is idle this re-aim).</summary>
    internal IntVector2 Velocity => _velocity;

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
