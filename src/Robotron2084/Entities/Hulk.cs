using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A hulk is a huge, tough robot that can't be killed by shooting it — it just gets knocked back. It slowly stomps after you or a human.</summary>
/// <seealso cref="Player"/>
/// <seealso cref="Human"/>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRH11.ASM</c>, routine <c>HULK</c> (with <c>HULKND</c>, <c>HULKIL</c> sub-blocks)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>ANIMATE_HULK</c> (<c>$003E</c>), with movement in <c>HULK_MOVE_HORIZONTALLY</c>/<c>MAKE_HULK_MOVE_VERTICALLY</c> and direction changes in <c>HULK_CHANGE_DIRECTION</c></item>
/// </list>
/// </remarks>
public sealed class Hulk : IEntity, IAnimationFrameSource
{
    /// <summary>ROM <c>HNDX</c>/<c>HNDY</c>: ...and less than this many (exclusive bound of the random roll).</summary>
    private const int AimOffsetMaxExclusiveArcadePixels = 16;

    /// <summary>ROM <c>HNDX</c>/<c>HNDY</c>: the aim is the target's coordinate plus at least this many arcade px.</summary>
    private const int AimOffsetMinArcadePixels = -16;

    /// <summary>One more than the most steps a hulk may take before it aims again. The number of steps is picked at random, from <see cref="ReaimStepsMin"/> up to one less than this, and counted down in <see cref="_stepsUntilReaim"/>.</summary>
    private const int ReaimStepsMaxExclusive = 32;

    /// <summary>The fewest steps a hulk takes before it aims again. The number of steps is picked at random, from this up to one less than <see cref="ReaimStepsMaxExclusive"/>, and counted down in <see cref="_stepsUntilReaim"/>.</summary>
    private const int ReaimStepsMin = 1;

    /// <summary>ROM <c>HULKIL</c>: how much a doubled sideways shove is multiplied.</summary>
    private const int ShoveSidewaysDoubleFactor = 2;

    /// <summary>ROM <c>HULKIL</c>: a sideways shove is doubled when a roll of this many sides comes up 0 (half the time).</summary>
    private const int ShoveSidewaysDoubleRollSides = 2;

    /// <summary>ROM <c>HULKIL</c>: how much a quadrupled up/down shove is multiplied.</summary>
    private const int ShoveVerticalQuadrupleFactor = 4;

    /// <summary>ROM <c>HULKIL</c>: the roll must come up below this for the up/down shove to be quadrupled (three quarters of the time).</summary>
    private const int ShoveVerticalQuadrupleRollBelow = 3;

    /// <summary>ROM <c>HULKIL</c>: an up/down shove is quadrupled when a roll of this many sides comes up below the threshold.</summary>
    private const int ShoveVerticalQuadrupleRollSides = 4;

    /// <summary>The longer sideways step, taken on the odd entries of the walk pattern.</summary>
    private const int SidewaysLongStepArcadePixels = 4;

    /// <summary>The shorter sideways step, taken on the even entries of the walk pattern (ROM horizontal animation table).</summary>
    private const int SidewaysShortStepArcadePixels = 3;

    /// <summary>The flat up/down step.</summary>
    private const int VerticalStepArcadePixels = 2;

    /// <summary>How many steps make up the walk pattern. <see cref="_walkCycleStep"/> counts up to this and then goes back to the first step.</summary>
    private const int WalkPatternLength = 4;

    /// <summary>The hulk sprite's own 14x16 arcade px box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.HulkCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.HulkCollisionSize.Height));

    /// <summary>The walk frames for each direction, as indices into <see cref="SpriteSet.HulkAnimationFrames"/>.</summary>
    /// <remarks>Each direction plays 4 frames in an A-B-A-C pattern, reusing 3 of the 9 animation frames:
    /// LEFT hulk1/2/1/3, RIGHT hulk7/8/7/9, UP and DOWN hulk4/5/4/6 (ROM: <c>HLKAL</c>/<c>HLKAR</c>/
    /// <c>HLKAD</c>/<c>HLKAU</c>, animation frames <c>HLKLP1</c>).</remarks>
    private static readonly int[] LeftAnimationFrames = { 0, 1, 0, 2 };

    private static readonly int[] RightAnimationFrames = { 6, 7, 6, 8 };
    private static readonly int[] VerticalAnimationFrames = { 3, 4, 3, 5 };

    private readonly Random _random;
    private readonly SpriteSet _sprites;
    private readonly int _beatIntervalClockUnits; // how long one step takes — the ROM's HLKSPD frame count, in clock units
    private readonly Func<IntVector2> _target;
    private bool _aimed;
    private int _animationFrameIndex;
    private Direction8 _direction;
    private bool _horizontal;
    private Rectangle? _playfieldBounds;
    private IntVector2 _position;

    // 0-based index into SpriteSet.HulkAnimationFrames
    private int _reaimStepsRemaining;

    private int _beatTimer;
    private int _walkCycleStep; // which of the 4 frames in the current walk pattern comes up next (0-3)
                                // cached from the last Update; used by ApplyKnockback

    /// <summary>Creates a hulk; it takes its first aim on its first update.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the hulk.</param>
    /// <param name="random">The random source, for the re-aim timer and the aim offsets.</param>
    /// <param name="beatIntervalRomFrames">How many ROM frames between steps (ROM <c>HLKSPD</c>): a bigger number is a SLOWER hulk.</param>
    /// <param name="target">Returns who this hulk hunts right now: the player, or a human that falls back to the player once it is gone.</param>
    /// <remarks>The interval between beats (ROM: <c>HLKSPD</c>) is 5-8 ROM frames.</remarks>
    public Hulk(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int beatIntervalRomFrames,
        Func<IntVector2> target)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _beatIntervalClockUnits = ArcadeClock.ToClockUnits(beatIntervalRomFrames);
        _target = target;
        _reaimStepsRemaining = RollReaimSteps();
        _direction = Direction8.Up; // placeholder — the first Update() call picks the real starting direction
        _animationFrameIndex = VerticalAnimationFrames[0];
    }

    /// <summary>The hulk sprite's own 14x16 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>This hulk's current walk animation frame, for the appear effect.</summary>
    /// <remarks>It never shatters, but it still materialises at the start of a wave.</remarks>
    public Texture2D GetCurrentAnimationFrame() => _sprites.HulkAnimationFrames[_animationFrameIndex];

    /// <summary>Always Alive — indestructible (the enum's Dying/Dead are simply never used here).</summary>
    public EntityLifeState LifeState => EntityLifeState.Alive;

    /// <summary>Top-left of the hulk.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>Current walk frame, 0-based index into <see cref="SpriteSet.HulkAnimationFrames"/> (test hook).</summary>
    internal int AnimationFrameIndex => _animationFrameIndex;

    /// <summary>Current travel direction (test hook).</summary>
    internal Direction8 Direction => _direction;

    /// <summary>Pushes the hulk along the laser's travel direction, stopping at the wall.</summary>
    /// <param name="direction">The laser's travel direction, per axis (-1, 0 or +1).</param>
    /// <remarks>ROM: <c>HULKIL</c> — sideways the shove is 1 arcade px, doubling about half the
    /// time; up/down it is 1, quadrupling about three-quarters of the time.</remarks>
    public void ApplyKnockback(IntVector2 direction)
    {
        int dx = direction.X != 0 && _random.Next(ShoveSidewaysDoubleRollSides) == 0
            ? direction.X * ShoveSidewaysDoubleFactor
            : direction.X;
        int dy = direction.Y != 0 && _random.Next(ShoveVerticalQuadrupleRollSides) < ShoveVerticalQuadrupleRollBelow
            ? direction.Y * ShoveVerticalQuadrupleFactor
            : direction.Y;
        _position += new IntVector2(ScreenSize.ToPortPixels(dx), ScreenSize.ToPortPixels(dy));
        if (_playfieldBounds is { } bounds)
        {
            int x = Math.Clamp(_position.X, bounds.X, bounds.Right - CollisionSize.Width);
            int y = Math.Clamp(_position.Y, bounds.Top, bounds.Bottom - CollisionSize.Height);
            _position = new IntVector2(x, y);
        }
    }

    /// <summary>Draws the current walk animation frame solid.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), Bounds, Color.White);
    }

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
            _animationFrameIndex = GetFrames(_direction)[0]; // start the walk animation from its first frame
            return;
        }

        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < _beatIntervalClockUnits)
        {
            return;
        }

        _beatTimer -= _beatIntervalClockUnits;

        // Show this cycle's walk frame, then move; sideways steps alternate short and long.
        _animationFrameIndex = GetFrames(_direction)[_walkCycleStep];
        int stepArcadePx = _horizontal
            ? (_walkCycleStep % 2 == 0 ? SidewaysShortStepArcadePixels : SidewaysLongStepArcadePixels)
            : VerticalStepArcadePixels;
        _walkCycleStep = (_walkCycleStep + 1) % WalkPatternLength;
        IntVector2 next = _position + _direction.ToIntVector() * ScreenSize.ToPortPixels(stepArcadePx);
        if (field.HitsWall(new Rectangle(next.X, next.Y, CollisionSize.Width, CollisionSize.Height)))
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

    /// <summary>Puts the hulk at <paramref name="position"/> (test hook).</summary>
    /// <param name="position">The new top-left.</param>
    internal void TeleportTo(IntVector2 position) => _position = position;

    private static int[] GetFrames(Direction8 direction) => direction switch
    {
        Direction8.Left => LeftAnimationFrames,
        Direction8.Right => RightAnimationFrames,
        _ => VerticalAnimationFrames, // the ROM draws DOWN and UP with the same set of animation frames
    };

    /// <summary>Aims along the current axis at the target's coordinate plus a random offset.</summary>
    /// <remarks>ROM: <c>HNDX</c>/<c>HNDY</c> — an aim outside the wall is pulled back in; an aim
    /// above the top wall is redirected to the bottom wall.</remarks>
    private void PickDirection(PlayField field)
    {
        IntVector2 target = _target();
        int offset = _random.Next(AimOffsetMinArcadePixels, AimOffsetMaxExclusiveArcadePixels);
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

    /// <summary>Re-rolls the step timer, switches axis and picks a new direction.</summary>
    /// <remarks>ROM: <c>HULKND</c>/<c>HND10</c> — the walk animation restarts at its first frame.</remarks>
    private void Reaim(PlayField field)
    {
        _reaimStepsRemaining = RollReaimSteps();
        _horizontal = !_horizontal;
        PickDirection(field);
        _walkCycleStep = 0;
        _animationFrameIndex = GetFrames(_direction)[0];
    }

    /// <summary>The steps until the next fresh direction: a random count (ROM <c>HULKND</c>).</summary>
    private int RollReaimSteps() => _random.Next(ReaimStepsMin, ReaimStepsMaxExclusive);
}
