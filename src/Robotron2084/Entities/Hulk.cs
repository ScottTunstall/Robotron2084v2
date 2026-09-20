using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A hulk — the large, indestructible robot that slowly stalks the player or a human. It never dies
/// and cannot be shot: a laser only knocks it backwards a short distance (clamped at the wall, never
/// pushed through it — see <see cref="ApplyKnockback"/>), its <see cref="LifeState"/> is always
/// <see cref="EntityLifeState.Alive"/>, and walking over an electrode destroys the ELECTRODE, not
/// the hulk (the field resolves that — this class has no electrode-collision code of its own). A
/// hulk touching the player kills the player. See <see cref="IEntity"/> for the "beat" / "ROM frame"
/// / "..Timer" / "notes §NN" terminology used throughout this class.
///
/// It lumbers in big, slow steps rather than gliding smoothly — one step per speed cycle: 3 or 4
/// arcade px sideways (alternating between the two each step, in step with its walk animation), or a
/// flat 2 arcade px up or down. It only ever moves along ONE axis at a time (purely horizontal or
/// purely vertical, never diagonal). It picks a fresh direction ("re-aims") when its random step
/// timer runs out, or immediately if the wall is in the way of its next step (it simply stands still
/// for that cycle instead). Each re-aim flips which axis it travels on (horizontal becomes vertical
/// and vice versa) and then aims along that new axis at the target's coordinate plus a random
/// -16..+15 pixel offset, so it wanders toward its target rather than beelining precisely at it.
///
/// What it hunts is decided when it is created: either the player, or a named human that falls back
/// to the player once that member dies or is rescued (see the constructor's <c>target</c>).
/// </summary>
/// <remarks>
/// Ported from the arcade's own hulk behaviour, routine for routine (ROM: RRH11.ASM, the `HULK`
/// process — <c>HULKND</c> re-aims, <c>HNDX</c>/<c>HNDY</c> pick the direction, <c>CKLIMV</c> rejects
/// a step that would leave the playfield, <c>HULKIL</c> handles the laser knockback, and
/// <c>HLKSPD</c> is the step period).
///
/// One historical arcade quirk is worth knowing: each hulk decides who it hunts the instant it's
/// created, by a 50/50 coin flip. But hulks are created before the human family exists yet, so the
/// "hunt a human" branch has nothing to point at — it ends up hunting the player, same as the other
/// branch, just by a different (and in the original ROM, slightly buggy) path. This port reproduces
/// the same outcome (hunt the player) without reproducing the ROM's specific bug.
///
/// The walk animation (decoded from the ROM; see <see cref="LeftFrames"/>) is not a plain 1-2-3-4
/// cycle — each direction repeats a 4-step A-B-A-C pattern (frame A, then B, then A again, then C)
/// drawn from the hulk's 9 sprite frames, restarting at frame A every time the hulk changes direction.
/// </remarks>
public sealed class Hulk : IEntity, IArtSource
{
    /// <summary>The collision box: the hulk picture's own size, 14x16 arcade px, in port pixels,
    /// top-left anchored at <see cref="Position"/>.</summary>
    /// <remarks>The ROM picture's dimensions.</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.HulkCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.HulkCollisionSize.Height));

    private readonly Random _random;
    private readonly int _stepPeriod; // how long one step takes, in the fixed-point "fifths" clock (see IEntity) — converted from the ROM's HLKSPD frame count
    private readonly Func<IntVector2> _target;
    private bool _aimed;
    private bool _horizontal;
    private int _animEntry; // which of the 4 frames in the current walk pattern comes up next (0-3)
    private int _currentFrameIndex; // 0-based index into SpriteSet.HulkFrames
    private int _reaimStepsRemaining;
    private int _stepTimer;
    private Direction8 _direction;
    private IntVector2 _position;
    private Rectangle? _playfieldBounds; // cached from the last Update; used by ApplyKnockback

    /// <summary>Creates a hulk; it takes its first aim on its first update.</summary>
    /// <param name="position">Top-left of the hulk.</param>
    /// <param name="random">The random source, for the re-aim timer and the aim offsets.</param>
    /// <param name="hulkSpeedRomTicks">How many ROM frames between steps — this wave's hulk speed.</param>
    /// <param name="target">Returns who this hulk hunts right now: the player, or a human that falls back to the player once it is gone.</param>
    /// <remarks>The step period (ROM: HLKSPD) is 5-8 ROM frames — not a whole number of port ticks — so
    /// it goes through the exact fixed-point "fifths" conversion described on <see cref="IEntity"/>
    /// rather than being rounded, which would make a hulk step slightly too fast.</remarks>
    public Hulk(IntVector2 position, Random random, int hulkSpeedRomTicks, Func<IntVector2> target)
    {
        _position = position;
        _random = random;
        _stepPeriod = hulkSpeedRomTicks * 6;
        _target = target;
        _reaimStepsRemaining = random.Next(1, 32); // a fresh direction comes every 1-31 steps, chosen at random
        _direction = Direction8.Up; // placeholder — the first Update() call picks the real starting direction
        _currentFrameIndex = VerticalFrames[0];
    }

    /// <summary>The walk frames for each direction, as indices into <see cref="SpriteSet.HulkFrames"/> (the repo's hulk1..9).</summary>
    /// <remarks>
    /// Each direction plays 4 frames in an A-B-A-C pattern (not a simple 1-2-3-4 cycle), reusing only 3
    /// of the 9 hulk pictures: LEFT plays hulk1, hulk2, hulk1, hulk3; RIGHT plays hulk7, hulk8, hulk7,
    /// hulk9; UP and DOWN both play hulk4, hulk5, hulk4, hulk6. Verified against the ROM's own
    /// animation tables (HLKAL/HLKAR/HLKAD/HLKAU) and picture list (HLKLP1).
    /// </remarks>
    private static readonly int[] LeftFrames = { 0, 1, 0, 2 };
    private static readonly int[] RightFrames = { 6, 7, 6, 8 };
    private static readonly int[] VerticalFrames = { 3, 4, 3, 5 };

    private static int[] FramesFor(Direction8 direction) => direction switch
    {
        Direction8.Left => LeftFrames,
        Direction8.Right => RightFrames,
        _ => VerticalFrames, // the ROM draws DOWN and UP with the same set of pictures
    };

    /// <summary>Current walk frame, 0-based index into <see cref="SpriteSet.HulkFrames"/> (test hook).</summary>
    internal int AnimationFrameIndex => _currentFrameIndex;

    /// <summary>Current travel direction (test hook).</summary>
    internal Direction8 Direction => _direction;

    /// <summary>Top-left of the hulk; settable for tests.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position
    {
        get => _position;
        internal set => _position = value; // test hook
    }

    /// <summary>The hulk picture's own 14x16 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Always Alive — indestructible (the enum's Dying/Dead are simply never used here).</summary>
    public EntityLifeState LifeState => EntityLifeState.Alive;

    /// <summary>
    /// One hulk cycle: the first call aims it (as the ROM does when the object is created), and
    /// every later one either takes a step — showing that step's walk frame, moving by the block's
    /// delta and re-aiming when the timer runs out — or, if the wall is in the way, re-aims instead
    /// and stays put. Held completely still while <see cref="PlayField.RobotsFrozen"/>.
    /// </summary>
    /// <param name="gameTime">Unused — the steps are counted in ROM frames (HLKSPD).</param>
    /// <param name="field">The playfield: the wall, and the frozen flag.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        _playfieldBounds = field.Wall.PlayfieldBounds;
        if (field.RobotsFrozen)
        {
            return;
        }

        if (!_aimed)
        {
            // The very first move a hulk ever makes is always sideways, never up/down
            // (the arcade does the same on spawn).
            _aimed = true;
            _horizontal = true;
            PickDirection(field);
            _currentFrameIndex = FramesFor(_direction)[0]; // start the walk animation from its first frame
            return;
        }

        _stepTimer += 5;
        if (_stepTimer < _stepPeriod)
        {
            return;
        }

        _stepTimer -= _stepPeriod;

        // Take one step: show this cycle's walk frame, then move — sideways steps
        // alternate 3px and 4px, up/down steps are a flat 2px — and advance to the
        // next frame in the walk pattern.
        int[] frames = FramesFor(_direction);
        _currentFrameIndex = frames[_animEntry & 3];
        int stepArcadePx = _horizontal ? ((_animEntry & 1) == 0 ? 3 : 4) : 2;
        _animEntry = (_animEntry + 1) & 3;
        IntVector2 next = _position + _direction.ToIntVector() * ScreenSize.Scaled(stepArcadePx);
        if (field.Wall.Intersects(new Rectangle(next.X, next.Y, CollisionSize.Width, CollisionSize.Height)))
        {
            // That step would cross the wall — stay put this cycle and pick a new direction instead.
            Reaim(field);
            return;
        }

        _position = next;

        // Countdown to the next scheduled direction change, regardless of the wall check above.
        if (--_reaimStepsRemaining <= 0)
        {
            Reaim(field);
        }
    }

    /// <summary>Re-rolls the step timer, switches to the other axis and picks a new direction, resetting the walk to the block's first frame.</summary>
    /// <remarks>
    /// Restarts the random step-timer, switches from moving sideways to moving up/down (or back), aims
    /// along that new axis, and resets the walk animation to its first frame (ROM: HULKND / HND10).
    /// </remarks>
    private void Reaim(PlayField field)
    {
        _reaimStepsRemaining = _random.Next(1, 32);
        _horizontal = !_horizontal;
        PickDirection(field);
        // A fresh direction always restarts the walk animation from its first frame.
        _animEntry = 0;
        _currentFrameIndex = FramesFor(_direction)[0];
    }

    /// <summary>Aims along the current axis at the target's coordinate plus a random -16..+15 offset.</summary>
    /// <remarks>
    /// Aims at the target's position plus a small random offset (-16 to +15 px) on whichever axis the
    /// hulk is currently travelling on, so it wanders toward the target rather than walking a perfectly
    /// straight line at it. If that aim point would fall outside the playfield, it's adjusted back in:
    /// a sideways aim is pulled back inside the wall, and an aim above the top wall is redirected to
    /// the bottom wall instead (ROM: HNDX/HNDY).
    /// </remarks>
    private void PickDirection(PlayField field)
    {
        IntVector2 target = _target();
        int offset = _random.Next(-16, 16);
        Rectangle bounds = field.Wall.PlayfieldBounds;
        if (_horizontal)
        {
            int tx = Math.Clamp(target.X + offset, bounds.X, bounds.Right - CollisionSize.Width);
            _direction = tx <= _position.X ? Direction8.Left : Direction8.Right;
        }
        else
        {
            int ty = target.Y + offset;
            if (ty < bounds.Y) // aiming above the top wall — redirect to the bottom wall instead
            {
                ty = bounds.Bottom - CollisionSize.Height;
            }

            _direction = ty <= _position.Y ? Direction8.Up : Direction8.Down;
        }
    }

    /// <summary>
    /// Pushes the hulk along the laser's travel direction, stopping at the wall. A diagonal
    /// laser pushes both axes.
    /// </summary>
    /// <param name="direction">The laser's direction of travel, per axis (-1, 0 or +1).</param>
    /// <remarks>
    /// A laser can't kill a hulk, but it does shove it back a little (spec: "pushed back ... into the
    /// WALL but no further"). The push is small and randomised: sideways it's normally 1 arcade px, but
    /// doubles to 2px about half the time; up/down it's normally 1 arcade px, but quadruples to 4px
    /// about three-quarters of the time (ROM: HULKIL).
    /// </remarks>
    public void ApplyKnockback(IntVector2 direction)
    {
        int dx = direction.X != 0 && _random.Next(2) == 0 ? direction.X * 2 : direction.X;
        int dy = direction.Y != 0 && _random.Next(4) < 3 ? direction.Y * 4 : direction.Y;
        _position += new IntVector2(ScreenSize.Scaled(dx), ScreenSize.Scaled(dy));
        if (_playfieldBounds is { } bounds)
        {
            int x = Math.Clamp(_position.X, bounds.X, bounds.Right - CollisionSize.Width);
            int y = Math.Clamp(_position.Y, bounds.Top, bounds.Bottom - CollisionSize.Height);
            _position = new IntVector2(x, y);
        }
    }

    /// <summary>Draws the current walk picture solid; a hulk never flashes and never dies.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the hulk frames.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        // Always solid: no flash, no death animation (indestructible).
        sprites.DrawSprite(spriteBatch, sprites.HulkFrames[_currentFrameIndex], Bounds, Color.White);
    }

    /// <summary>This hulk's current walk picture, for the appear effect.</summary>
    /// <param name="sprites">The shared sprite set, which holds the hulk frames.</param>
    /// <returns>The texture for the current walk frame.</returns>
    /// <remarks>
    /// The hulk never shatters (it is indestructible), but it does still materialise at the start of a
    /// wave — every robot gets that "appear" effect, hulk included — so it still needs to supply its
    /// current art (see <see cref="IArtSource"/>; ROM: RRG23's APPEAR).
    /// </remarks>
    public Texture2D CurrentFrameArt(SpriteSet sprites) => sprites.HulkFrames[_currentFrameIndex];
}
