using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A hulk — the big, indestructible robot that slowly stalks the player or a human.</summary>
/// <seealso cref="Player"/>
/// <seealso cref="Human"/>
/// <remarks>ROM: RRH11.ASM's <c>HULK</c> process — <c>HULKND</c> re-aims, <c>HNDX</c>/<c>HNDY</c>
/// pick the direction, <c>CKLIMV</c> rejects a step that would leave the field, <c>HULKIL</c> is the
/// laser knockback, <c>HLKSPD</c> the step period. Nothing kills it: a hit only shoves it back,
/// clamped at the wall, and it destroys electrodes rather than being destroyed by them. It hunts the
/// player, or a human that falls back to the player once gone, decided at creation. It takes one step
/// per speed cycle — 3 or 4 arcade px sideways, alternating, or a flat 2 up/down — on one axis only,
/// and re-aims when its random step timer runs out or the wall blocks it. A step's walk frame follows
/// a 4-step A-B-A-C pattern (see <see cref="LeftFrames"/>). Timers count 5 per tick and 6 per arcade
/// frame, so an interval of N frames is due at 6 x N.</remarks>
public sealed class Hulk : IEntity, IAnimationFrameSource
{
    private readonly SpriteSet _sprites;
    /// <summary>The hulk picture's own 14x16 arcade px box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.HulkCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.HulkCollisionSize.Height));

    private readonly Random _random;
    private readonly int _stepPeriod; // how long one step takes — the ROM's HLKSPD frame count
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
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the hulk.</param>
    /// <param name="random">The random source, for the re-aim timer and the aim offsets.</param>
    /// <param name="hulkSpeedRomTicks">How many ROM frames between steps — this wave's hulk speed.</param>
    /// <param name="target">Returns who this hulk hunts right now: the player, or a human that falls back to the player once it is gone.</param>
    /// <remarks>The step period (ROM: <c>HLKSPD</c>) is 5-8 ROM frames.</remarks>
    public Hulk(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int hulkSpeedRomTicks,
        Func<IntVector2> target)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _stepPeriod = ArcadeClock.Units(hulkSpeedRomTicks);
        _target = target;
        _reaimStepsRemaining = random.Next(1, 32); // a fresh direction comes every 1-31 steps, chosen at random
        _direction = Direction8.Up; // placeholder — the first Update() call picks the real starting direction
        _currentFrameIndex = VerticalFrames[0];
    }

    /// <summary>The walk frames for each direction, as indices into <see cref="SpriteSet.HulkFrames"/>.</summary>
    /// <remarks>Each direction plays 4 frames in an A-B-A-C pattern, reusing 3 of the 9 pictures:
    /// LEFT hulk1/2/1/3, RIGHT hulk7/8/7/9, UP and DOWN hulk4/5/4/6 (ROM: <c>HLKAL</c>/<c>HLKAR</c>/
    /// <c>HLKAD</c>/<c>HLKAU</c>, pictures <c>HLKLP1</c>).</remarks>
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

    /// <summary>One hulk cycle: aims on the first call, then steps, or re-aims when the wall blocks it.</summary>
    /// <param name="gameTime">Unused — the steps are counted in ROM frames.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        _playfieldBounds = field.Wall.PlayfieldBounds;
        if (field.RobotsFrozen)
        {
            return;
        }

        if (!_aimed)
        {
            // The first move is always sideways, never up/down.
            _aimed = true;
            _horizontal = true;
            PickDirection(field);
            _currentFrameIndex = FramesFor(_direction)[0]; // start the walk animation from its first frame
            return;
        }

        _stepTimer += ArcadeClock.UnitsPerPortTick;
        if (_stepTimer < _stepPeriod)
        {
            return;
        }

        _stepTimer -= _stepPeriod;

        // Show this cycle's walk frame, then move; sideways steps alternate 3px and 4px.
        int[] frames = FramesFor(_direction);
        _currentFrameIndex = frames[_animEntry & 3];
        int stepArcadePx = _horizontal ? ((_animEntry & 1) == 0 ? 3 : 4) : 2;
        _animEntry = (_animEntry + 1) & 3;
        IntVector2 next = _position + _direction.ToIntVector() * ScreenSize.Scaled(stepArcadePx);
        if (field.Wall.Intersects(new Rectangle(next.X, next.Y, CollisionSize.Width, CollisionSize.Height)))
        {
            // That step would cross the wall — stay put and re-aim.
            Reaim(field);
            return;
        }

        _position = next;

        if (--_reaimStepsRemaining <= 0)
        {
            Reaim(field);
        }
    }

    /// <summary>Re-rolls the step timer, switches axis and picks a new direction.</summary>
    /// <remarks>ROM: <c>HULKND</c>/<c>HND10</c> — the walk animation restarts at its first frame.</remarks>
    private void Reaim(PlayField field)
    {
        _reaimStepsRemaining = _random.Next(1, 32);
        _horizontal = !_horizontal;
        PickDirection(field);
        _animEntry = 0;
        _currentFrameIndex = FramesFor(_direction)[0];
    }

    /// <summary>Aims along the current axis at the target's coordinate plus a -16..+15 offset.</summary>
    /// <remarks>ROM: <c>HNDX</c>/<c>HNDY</c> — an aim outside the wall is pulled back in; an aim
    /// above the top wall is redirected to the bottom wall.</remarks>
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

    /// <summary>Pushes the hulk along the laser's travel direction, stopping at the wall.</summary>
    /// <param name="direction">The laser's travel direction, per axis (-1, 0 or +1).</param>
    /// <remarks>ROM: <c>HULKIL</c> — sideways the shove is 1 arcade px, doubling to 2 about half the
    /// time; up/down it is 1, quadrupling to 4 about three-quarters of the time.</remarks>
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

    /// <summary>Draws the current walk picture solid.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        _sprites.DrawSprite(spriteBatch, _sprites.HulkFrames[_currentFrameIndex], Bounds, Color.White);
    }

    /// <summary>This hulk's current walk picture, for the appear effect.</summary>
    /// <remarks>It never shatters, but it still materialises at the start of a wave.</remarks>
    public Texture2D CurrentAnimationFrame => _sprites.HulkFrames[_currentFrameIndex];
}
