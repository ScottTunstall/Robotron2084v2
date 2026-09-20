using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The missile a <see cref="Brain"/> fires at the player when it gets close enough. It does not fly
/// straight or home in smoothly: every so often it re-aims, and each re-aim moves it on ONE axis only
/// — or on both — so it lurches along in a drunken zigzag that is still biased toward the player. It
/// bounces off the walls instead of leaving the field, leaves a short trail of small marks (a rough
/// visual streak, drawn as solid rectangles, not sprite art) behind it that fades as it moves, and is
/// removed instantly by a laser hit (worth 25 points) — there is no death animation. Touching the
/// player kills the player. See <see cref="IEntity"/> for the "beat" / "ROM frame" / "..Timer" /
/// "notes §NN" terminology used throughout this class.
/// </summary>
/// <remarks>
/// Ported from the arcade's own missile behaviour (ROM: RRB10.ASM, the `BRNSHT`/`GCMDIR`/`CMISL`/`CMMOV`
/// routines; the full decode is in arcade-fidelity-notes §18 and §46). It's fired from a point just
/// below and right of the brain that shot it. Its update runs on its own short clock, and each update
/// takes two one-pixel steps rather than one two-pixel step — the wall bounce is checked after each of
/// the two, which is what lets it turn around flush against a wall instead of overshooting into it.
///
/// The re-aim gives it its odd, lurching character: on each re-aim there's a 50% chance it moves only
/// vertically, a 25% chance only horizontally, and a 25% chance it moves on both axes at once (a true
/// diagonal) — so it's never fully stationary between re-aims. Whichever axis is active steers loosely
/// toward the player: the direction is the player's coordinate on that axis plus a small random nudge
/// (biased slightly positive), compared against the missile's own coordinate.
///
/// It has no lifetime of its own — it just keeps bouncing until a laser hits it, worth 25 points, with
/// an instant removal and no death animation. Touching the player kills the player. Missiles are
/// missiles, not robots, so they keep flying even while <see cref="PlayField.RobotsFrozen"/> is set
/// (the same rule that applies to sparks and shells).
///
/// The missile's own picture data in the ROM is never actually drawn — it exists purely to define the
/// (small) collision box. What's drawn instead is a direct video-memory write of a colored dot at the
/// new position each step, and on the original arcade hardware that write happens to color two pixels
/// stacked vertically rather than side by side (an artifact of how the video memory was addressed), which
/// is why each mark on screen is 1 pixel wide but 2 pixels tall. This port reproduces that by drawing
/// small solid rectangles (see <see cref="Draw"/>) instead of sprite frames.
///
/// The trail behind the missile is a fixed-size ring of the 9 most recent positions, not a
/// continuously growing snake: each step also erases the mark from 9 steps ago, and destroying the
/// missile wipes the rest of the ring at once, so the whole tail disappears along with the missile head.
/// </remarks>
public sealed class CruiseMissile : IEntity
{
    /// <summary>The collision box's size, 6x4 arcade px, in port pixels; the box itself is offset up-left.</summary>
    /// <remarks>The disassembled ROM source affectionately labels this hitbox "FAT PHONY GUY" — it's
    /// much bigger than the missile's actual 1x2px visible mark, offset up and to the left of the
    /// tracked point, presumably to make the missile easier to hit with a laser.</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.CruiseMissileCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.CruiseMissileCollisionSize.Height));

    /// <summary>
    /// How far the missile steps along X per move, in port pixels: two arcade pixels (one whole
    /// column) — twice the Y step.
    /// </summary>
    /// <remarks>
    /// The arcade moves the missile one whole video-memory "column" per step on this axis, and a
    /// column is 2 arcade pixels wide (notes §51).
    /// </remarks>
    private static readonly int StepColumns = ScreenSize.Scaled(2);

    /// <summary>
    /// How far the missile steps along Y per move, in port pixels: one arcade pixel. There is
    /// no halving like the player's or the tank's, so the missile really is twice as fast
    /// horizontally as vertically.
    /// </summary>
    /// <remarks>
    /// The arcade moves the missile one video-memory "row" per step on this axis, which is 1
    /// arcade pixel — with no halving like the player's or the tank's vertical speed, so the
    /// missile really does move twice as fast horizontally as vertically.
    /// </remarks>
    private static readonly int StepRows = ScreenSize.Scaled(1);

    /// <summary>How many ROM frames one beat takes: the beat's own execution plus its sleep.</summary>
    /// <remarks>ROM beat: NAP 2 plus the beat execution vblank.</remarks>
    private const int BeatPeriodRomTicks = 3;

    /// <summary>The beat period, converted to fifth-ticks (see <see cref="IEntity"/>): 3 ROM frames = 3.6 port ticks.</summary>
    /// <remarks>Notes §52, §65.</remarks>
    private static int BeatPeriod => BeatPeriodRomTicks * 6;

    /// <summary>How many moves the missile makes per beat: twice.</summary>
    /// <remarks>ROM CMMOV calls per beat.</remarks>
    private const int MovesPerBeat = 2;

    /// <summary>The re-aim timer's upper bound, in beats: the timer is rolled from 1 to this.</summary>
    /// <remarks>ROM GCMDIR: re-aim timer = RND(1..7) beats.</remarks>
    private const int ReAimMaxBeats = 7;

    /// <summary>Subtracted from each aim roll, making the random nudge run -6..+9 (not a symmetric ±6).</summary>
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
    private IntVector2 _velocity; // Current per-step move on each axis: ±StepColumns / ±StepRows, or 0 if that axis is idle this re-aim.
    private int _beatTimer;
    private int _reAimBeatsRemaining;

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
        _reAimBeatsRemaining = 1 + _random.Next(ReAimMaxBeats);
    }

    /// <summary>The missile's TRUE coordinate (the fat box below is derived from it).</summary>
    public IntVector2 Position => _position;

    /// <summary>
    /// The collision box: the missile's true corner shifted one pixel up and left, because the box is
    /// bigger than the point the missile tracks.
    /// </summary>
    /// <remarks>The "FAT PHONY GUY" hitbox (see <see cref="CollisionSize"/>), offset up and left (notes §51).</remarks>
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
    /// <remarks>Wipes the trail's remaining marks off-screen along with the missile itself, so nothing
    /// is left behind (ROM: `CMKIL`).</remarks>
    public void Destroy()
    {
        LifeState = EntityLifeState.Dead;
        _trail.Clear();
    }

    /// <summary>
    /// Runs one beat when its 3-frame clock says so: re-aim if the timer has run out, then take the
    /// beat's two steps — each marking the position it leaves and dropping the ring's oldest mark.
    /// </summary>
    /// <param name="gameTime">Unused — the beat is counted in ROM frames.</param>
    /// <param name="field">The playfield: the player it aims at and the wall it bounces off.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        // A beat's length in port ticks isn't a whole number (3.6, not 3), so it's tracked with
        // the fixed-point fifths trick rather than rounded down — rounding down made the
        // missile move 20% too fast (notes §52).
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

    /// <summary>
    /// Takes one step: moves up to one column on X and one row on Y, bouncing per axis off the
    /// playfield. A component that would leave the field is negated and the step taken the other
    /// way, so the missile turns around on the boundary rather than sticking to it. The position
    /// it leaves is added to the trail.
    /// </summary>
    /// <remarks>
    /// One column of movement on X and one row on Y, with each axis bouncing off the playfield
    /// independently. When a step would cross the boundary, that axis's direction flips and the
    /// step is retaken the other way, so the missile turns around right at the wall rather than
    /// sticking to it. The mark left behind uses the trail's own colour.
    ///
    /// The bounce check uses the missile's true tracked point, not its oversized "FAT PHONY GUY"
    /// collision box — that box exists only for laser hits and must not make the missile bounce
    /// early.
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

        // The position the missile just left gets a trail mark, and the oldest mark (from 9
        // steps back) is dropped in the same pass, so the tail never grows past 9 marks.
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
    /// A coin flip decides whether the missile only chases vertically (50% of re-aims), or considers
    /// horizontal movement at all; when it does consider horizontal movement, a second coin flip decides
    /// whether vertical movement is added on top too. Net effect: Y-only 50% of the time, X-only 25%,
    /// and a true diagonal 25% — vertical is the missile's favoured axis (ROM: RRB10.ASM `GCMDIR`).
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
    /// <remarks>If the nudged target coordinate is at or beyond the missile's own coordinate, it moves
    /// in the positive direction; otherwise negative.</remarks>
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
    /// <remarks>Each mark is drawn 1 arcade pixel wide and 2 tall, matching the shape the original
    /// hardware's video-memory writes produced. The trail uses one palette slot and the head another.</remarks>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        // Each mark is 1 arcade pixel wide and 2 tall (see the remarks above).
        int markWidth = ScreenSize.Scaled(GameplayConstants.MissileMarkArcadeWidth);
        int markHeight = ScreenSize.Scaled(GameplayConstants.MissileMarkArcadeHeight);

        Color trailColor = sprites.SlotColor(GameplayConstants.MissileTrailSlot);
        foreach (IntVector2 mark in _trail)
        {
            sprites.DrawSolidRectangle(spriteBatch, new Rectangle(mark.X, mark.Y, markWidth, markHeight), trailColor);
        }

        // The missile's own ROM picture data is never actually drawn — it only defines the
        // collision box — so its "sprite" here is just this solid dot.
        sprites.DrawSolidRectangle(
            spriteBatch,
            new Rectangle(_position.X, _position.Y, markWidth, markHeight),
            sprites.SlotColor(GameplayConstants.MissileHeadSlot));
    }
}
