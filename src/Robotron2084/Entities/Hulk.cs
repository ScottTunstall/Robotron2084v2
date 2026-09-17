using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The indestructible robot — RRH11's HULK process, ported step-for-step:
/// takes ONE step per HLKSPD-tick cycle (horizontal steps alternate 3/4
/// arcade-px in step with its animation, vertical 2 arcade-px); re-aims every
/// RND(1..31) steps or immediately on wall contact (no move that cycle).
/// Every re-aim flips the axis (the ROM alternates X/Y blocks) and aims the
/// new direction at the target's coordinate on that axis plus a random
/// -16..15 offset. Target (R5 $017C): each hulk rolls 50/50 at spawn —
/// a family-list slot (empty at spawn time, since the ROM spawns hulks
/// before the family, so the target is NULL and the hulk hunts the player;
/// the ROM's actual NULL read chases a phantom at $7E01 — a documented
/// bug we skip) or the last family-list slot (the last-spawned human,
/// falling back to the player once that member dies or is rescued).
/// <see cref="LifeState"/> is always <see cref="EntityLifeState.Alive"/>
/// (a laser only knocks it back, spec: "pushed back... into the WALL but no
/// further"). Like the ROM, a hulk walking over an electrode destroys the
/// electrode (handled by the field).
///
/// Animation (decoded 2026-09-13 from the ROM; see <see cref="LeftFrames"/>):
/// each direction block walks an ABAC sequence over the nine verified hulk
/// frames, restarting at the block's first frame on every direction change
/// (ROM HND10: CLRA; STA PD4,U; OPICT = HLKLP1 + first image).
/// </summary>
public sealed class Hulk : IEntity, IArtSource
{
    /// <summary>Collision box = the ROM picture dimensions (14x16 arcade px), top-left anchored at <see cref="Position"/>.</summary>
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

    /// <param name="hulkSpeedRomTicks">ROM HLKSPD for this wave (notes §11.2) — the step period in ROM ticks.</param>
    /// <param name="target">Who this hulk hunts: the player, or a human that falls back to the player when gone (R5 target roll, see class docs).</param>
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

    /// <summary>
    /// Walk frames per direction block, 0-based indices into
    /// <see cref="SpriteSet.HulkFrames"/> (the repo's hulk1..9). Verified
    /// 2026-09-13 against the ROM: the animation table at $01CC (byte-identical
    /// to RRH11's HLKAL/HLKAR/HLKAD/HLKAU) indexes the picture list HLKLP1
    /// at $0CF9, whose nine data pointers read hulk1, hulk2, hulk3, hulk7,
    /// hulk8, hulk9, hulk4, hulk5, hulk6 — so LEFT walks 1,2,1,3, RIGHT
    /// 7,8,7,9, and DOWN and UP both walk 4,5,4,6 (ABAC).
    /// </summary>
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

    public IntVector2 Position
    {
        get => _position;
        internal set => _position = value; // test hook
    }

    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Always Alive — indestructible (the enum's Dying/Dead are simply never used here).</summary>
    public EntityLifeState LifeState => EntityLifeState.Alive;

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

    /// <summary>
    /// ROM HULKND: reset the random step timer, FLIP the axis (previous
    /// direction was an X-block -> seek Y, and vice versa), then aim along
    /// the new axis at target + random offset, clamped/wrapped like the ROM.
    /// </summary>
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

    /// <summary>
    /// ROM HNDX/HNDY: aim = target coordinate + RND(-16..15) on the current
    /// axis; horizontal out-of-range clamps to the playfield, vertical
    /// below-range wraps to the far side; move toward the adjusted point.
    /// </summary>
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
    /// Laser impact — ROM RRH11 HULKIL: the hulk is indestructible and is
    /// pushed along the laser's travel direction, PER AXIS at ROM
    /// magnitudes: X = ±1 arcade px, doubled to ±2 when SEED's sign bit is
    /// clear (50%); Y = ±1, quadrupled to ±4 when LSEED &gt;= $C0 (75%).
    /// A diagonal laser pushes both axes. Clamped at the wall (spec:
    /// "pushed back ... into the WALL but no further").
    /// </summary>
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

    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        // Always solid: no flash, no death animation (indestructible).
        sprites.DrawSprite(spriteBatch, sprites.HulkFrames[_currentFrameIndex], Bounds, Color.White);
    }

    /// <summary>
    /// The hulk never shatters (it is indestructible), but it DOES materialise at
    /// a wave start — RRG23's `APPEAR` creates an appear record for every robot,
    /// and the hulk is on that list — so the engine can still blit its art.
    /// </summary>
    public Texture2D CurrentFrameArt(SpriteSet sprites) => sprites.HulkFrames[_currentFrameIndex];
}
