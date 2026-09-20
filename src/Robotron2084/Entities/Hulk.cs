using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A hulk — the indestructible robot. It never dies and cannot be shot: a laser only knocks it
/// backwards (clamped at the wall, never pushed through it), its <see cref="LifeState"/> is always
/// <see cref="EntityLifeState.Alive"/>, and walking over an electrode destroys the ELECTRODE, not
/// the hulk (the field resolves that).
///
/// It lumbers in big, slow steps — one per speed cycle: 3 or 4 arcade px sideways, alternating with
/// its animation, or 2 arcade px up or down. It re-aims when its random step timer runs out, or
/// immediately when the wall is in the way (no move that cycle). Each re-aim flips the axis it
/// travels on and then aims along the new axis at the target's coordinate plus a random -16..+15
/// offset, so it wanders rather than homing precisely.
///
/// What it hunts is decided when it is created: either the player, or a named human that falls back
/// to the player once that member dies or is rescued (see the constructor's <c>target</c>).
/// </summary>
/// <remarks>
/// RRH11.ASM's `HULK` process, ported step for step: `HULKND` re-aims, `HNDX`/`HNDY` pick the
/// direction, `CKLIMV` rejects a step that would leave the playfield, `HULKIL` handles the laser
/// knockback, and `HLKSPD` is the step period.
///
/// The target roll (R5 $017C): each hulk rolls 50/50 at spawn — a family-list slot, which is EMPTY
/// at spawn time because the ROM spawns hulks before the family (so the target is NULL and the hulk
/// hunts the player; the ROM's actual NULL read chases a phantom at $7E01 — a documented bug the
/// port skips), or the last family-list slot, i.e. the last-spawned human.
///
/// Animation (decoded 2026-09-13 from the ROM; see <see cref="LeftFrames"/>): each direction block
/// walks an ABAC sequence over the nine verified hulk frames, restarting at the block's first frame
/// on every direction change (`HND10`: CLRA; STA PD4,U; OPICT = HLKLP1 + first image).
/// </remarks>
public sealed class Hulk : IEntity, IArtSource
{
    /// <summary>The collision box: the hulk picture's own size, 14x16 arcade px, in port pixels,
    /// top-left anchored at <see cref="Position"/>.</summary>
    /// <remarks>The ROM picture's dimensions.</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.HulkCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.HulkCollisionSize.Height));

    private readonly Random _random;
    private readonly int _stepFifthsPerStep; // HLKSPD ROM frames in exact 6ths (notes §52)
    private readonly Func<IntVector2> _target;
    private bool _aimed;
    private bool _horizontal;
    private int _animEntry; // ROM PD4, 0..3: the next entry of the direction block
    private int _currentFrameIndex; // 0-based index into SpriteSet.HulkFrames
    private int _reaimStepsRemaining;
    private int _stepFifths;
    private Direction8 _direction;
    private IntVector2 _position;
    private Rectangle? _playfieldBounds; // cached from the last Update; used by ApplyKnockback

    /// <summary>Creates a hulk; it takes its first aim on its first update.</summary>
    /// <param name="position">Top-left of the hulk.</param>
    /// <param name="random">The random source, for the re-aim timer and the aim offsets.</param>
    /// <param name="hulkSpeedRomTicks">How many ROM frames between steps — this wave's hulk speed.</param>
    /// <param name="target">Returns who this hulk hunts right now: the player, or a human that falls back to the player once it is gone.</param>
    /// <remarks>ROM `HLKSPD` (notes §11.2), which is 5..8 ROM frames — so the exact-6ths clock matters here too.</remarks>
    public Hulk(IntVector2 position, Random random, int hulkSpeedRomTicks, Func<IntVector2> target)
    {
        _position = position;
        _random = random;
        // HLKSPD is 5..8 ROM frames, i.e. 6..9.6 ticks: the exact-6ths clock is
        // what keeps a wave-1 hulk (8 frames) from stepping 7% early.
        _stepFifthsPerStep = hulkSpeedRomTicks * 6;
        _target = target;
        _reaimStepsRemaining = random.Next(1, 32); // ROM PD5 = (LSEED & $1F) + 1
        _direction = Direction8.Up; // overwritten by the first aim (ROM runs HULKND at spawn)
        _currentFrameIndex = VerticalFrames[0];
    }

    /// <summary>The walk frames for each direction, as indices into <see cref="SpriteSet.HulkFrames"/> (the repo's hulk1..9).</summary>
    /// <remarks>
    /// Verified 2026-09-13 against the ROM: the animation table at $01CC (byte-identical to RRH11's
    /// HLKAL/HLKAR/HLKAD/HLKAU) indexes the picture list HLKLP1 at $0CF9, whose nine data pointers read
    /// hulk1, hulk2, hulk3, hulk7, hulk8, hulk9, hulk4, hulk5, hulk6 — so LEFT walks 1,2,1,3, RIGHT
    /// 7,8,7,9, and DOWN and UP both walk 4,5,4,6 (ABAC).
    /// </remarks>
    private static readonly int[] LeftFrames = { 0, 1, 0, 2 };
    private static readonly int[] RightFrames = { 6, 7, 6, 8 };
    private static readonly int[] VerticalFrames = { 3, 4, 3, 5 };

    private static int[] FramesFor(Direction8 direction) => direction switch
    {
        Direction8.Left => LeftFrames,
        Direction8.Right => RightFrames,
        _ => VerticalFrames, // the ROM's DOWN and UP blocks use the same images
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
            // ROM HULKST: the hulk's first HULKND seeks X (its initial
            // picture address is not an X-block), so it starts horizontal.
            _aimed = true;
            _horizontal = true;
            PickDirection(field);
            _currentFrameIndex = FramesFor(_direction)[0]; // HND10: PD4 = 0, OPICT = first frame
            return;
        }

        _stepFifths += 5;
        if (_stepFifths < _stepFifthsPerStep)
        {
            return;
        }

        _stepFifths -= _stepFifthsPerStep;

        // One big step per cycle (ROM HULK00/HULK0: show + move with the
        // current entry's delta — horizontal 3/4 arcade px, vertical 2 —
        // then advance the entry). The frame shown is the entry's frame.
        int[] frames = FramesFor(_direction);
        _currentFrameIndex = frames[_animEntry & 3];
        int stepArcadePx = _horizontal ? ((_animEntry & 1) == 0 ? 3 : 4) : 2;
        _animEntry = (_animEntry + 1) & 3;
        IntVector2 next = _position + _direction.ToIntVector() * ScreenSize.Scaled(stepArcadePx);
        if (field.Wall.Intersects(new Rectangle(next.X, next.Y, CollisionSize.Width, CollisionSize.Height)))
        {
            // ROM CKLIMV out-of-bounds: no move this cycle, take a new direction.
            Reaim(field);
            return;
        }

        _position = next;

        // ROM PD5 velocity timer: a fresh direction every RND(1..31) steps.
        if (--_reaimStepsRemaining <= 0)
        {
            Reaim(field);
        }
    }

    /// <summary>Re-rolls the step timer, switches to the other axis and picks a new direction, resetting the walk to the block's first frame.</summary>
    /// <remarks>
    /// ROM `HULKND`: reset the random step timer, FLIP the axis (previous direction was an X-block
    /// -> seek Y, and vice versa), then aim along the new axis; `HND10` resets the walk to the
    /// block's first frame (CLRA; STA PD4,U; OPICT = HLKLP1 + first image).
    /// </remarks>
    private void Reaim(PlayField field)
    {
        _reaimStepsRemaining = _random.Next(1, 32);
        _horizontal = !_horizontal;
        PickDirection(field);
        // ROM HND10: a new direction resets the walk to the block's first
        // frame (CLRA; STA PD4,U; OPICT = HLKLP1 + first image).
        _animEntry = 0;
        _currentFrameIndex = FramesFor(_direction)[0];
    }

    /// <summary>Aims along the current axis at the target's coordinate plus a random -16..+15 offset.</summary>
    /// <remarks>
    /// ROM `HNDX`/`HNDY`: aim = target coordinate + RND(-16..15) on the current axis; a horizontal
    /// aim out of range clamps to the playfield, a vertical one below range wraps to the far side;
    /// then move toward the adjusted point.
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
            if (ty < bounds.Y) // ROM: target below YMIN>>2 -> seek YMAX instead
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
    /// Spec: "pushed back ... into the WALL but no further".
    ///
    /// ROM RRH11 `HULKIL`: the hulk is pushed at ROM magnitudes — X = ±1 arcade px, doubled to ±2
    /// when SEED's sign bit is clear (50%); Y = ±1, quadrupled to ±4 when LSEED >= $C0 (75%).
    /// </remarks>
    public void ApplyKnockback(IntVector2 direction)
    {
        // ROM HULKIL: LDA LASDIR; TST SEED; BMI (skip) / ASLA -> x2 when the
        // sign bit is clear (50%); LDB LASDIR+1; CMPA LSEED #$C0; BHS (skip)
        // / ASLB X2 -> x4 when LSEED < $C0 (75%).
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
    /// The hulk never shatters (it is indestructible), but it DOES materialise at a wave start — the
    /// appear pass gives every robot — the hulk included — an appear record, so the effect still needs
    /// its art (see <see cref="IArtSource"/> and RRG23's `APPEAR`).
    /// </remarks>
    public Texture2D CurrentFrameArt(SpriteSet sprites) => sprites.HulkFrames[_currentFrameIndex];
}
