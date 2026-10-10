using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A prog is a family member the brain has captured and turned into an enemy. It looks like the human it used to be, but now hunts the player instead of running from danger.</summary>
/// <seealso cref="Human"/>
/// <seealso cref="StripEffect"/>
/// <remarks>
/// It acts on a beat. The <see cref="PlayField"/> calls <see cref="Update"/> on nearly every tick, through
/// <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. <see cref="_beatTimer"/> gathers the ticks
/// until it is time for the next beat (see <see cref="ArcadeClock"/>).
///
/// <list type="bullet">
/// <item>Original source: <c>RRB10.ASM</c>, routine <c>PROG</c> (with
/// <c>PROGST</c>/<c>GPOFF</c>/<c>GPDIR</c>/<c>PRGKIL</c>)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$1EAB</c> (<c>ANIMATE_PROG</c>)</item>
/// </list>
/// </remarks>
public sealed class Prog : IExplodable, IRemovable
{
    /// <summary>Sides of the coin flip that picks whether a re-aim considers X or Y.</summary>
    private const int AxisFlipSides = 2;

    /// <summary>How many ROM frames pass between beats.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>PROG</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    /// </list>
    ///
    /// The arcade re-runs the prog's step logic every 3 frames.
    /// </remarks>
    private const int BeatIntervalRomFrames = 3;

    /// <summary>Half of the range of the sideways aim offset. A random roll has this subtracted from it, and the result is multiplied by <see cref="OffsetXStepColumns"/> to give <see cref="_offsetX"/>.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>GPOFF</c> gives ±28 columns of offset in steps of 4.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    /// </list>
    /// </remarks>
    private const int OffsetXHalfRange = 8;

    /// <summary>The biggest number the sideways aim offset's random roll can come up with. The roll starts at one. It is used to work out <see cref="_offsetX"/>.</summary>
    private const int OffsetXRollMax = 15;

    /// <summary>How many columns each step of the sideways aim offset is. The rolled number of steps is multiplied by this to give <see cref="_offsetX"/>.</summary>
    private const int OffsetXStepColumns = 4;

    /// <summary>The Y offset is (this minus a roll of 1..<see cref="OffsetYSteps"/>) times the step, less <see cref="OffsetYSteps"/>.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>GPOFF</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    /// </list>
    /// </remarks>
    private const int OffsetYCentre = 19;

    /// <summary>How many rows each step of the up-and-down aim offset is. The rolled number of steps is multiplied by this to give <see cref="_offsetY"/>.</summary>
    private const int OffsetYStepRows = 2;

    /// <summary>The span of the up-and-down aim offset, in steps. A random roll from one up to this is used to work out <see cref="_offsetY"/>.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>GPOFF</c> computes this from a random 1..18 roll.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    /// </list>
    /// </remarks>
    private const int OffsetYSteps = 18;

    private const int ReDirectionThreshold256 = 0xE4;

    /// <summary>Roll thresholds out of 256: above the first the offsets re-roll, above the second it re-aims.</summary>
    private const int ReOffsetThreshold256 = 0xF8;

    /// <summary>The horizontal step: 2 columns = 4 arcade px, the same distance as the vertical step.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>GPOFF</c> — the X step table moves 2 columns at a time,
    /// and a column is 2 arcade px.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    /// </list>
    /// </remarks>
    private const int StepXColumns = 2;

    /// <summary>The vertical step: ±4 rows on Y.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>GPOFF</c> — the Y step table.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    /// </list>
    /// </remarks>
    private const int StepYRows = 4;

    /// <summary>Sides of the ROM rolls the re-offset and re-aim thresholds are compared against.</summary>
    private const int ThresholdRollSides = 256;

    /// <summary>The aim-wrap margin past the field's far edge, in columns (X) and rows (Y).</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>GPDIR</c> wraps an aim past this margin to the opposite
    /// edge.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    /// </list>
    /// </remarks>
    private const int WrapMarginXColumns = 0x30;

    private const int WrapMarginYRows = 18;

    // The walk cycle: animation frame 1, 2, 1, 3 — the same A-B-A-C pattern the humans use.
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    private readonly (int Width, int Height) _collisionSize;

    /// <summary>The shadow ring, NEWEST FIRST (see <see cref="Ghost"/>).</summary>
    private readonly List<Ghost> _ghosts = new();

    private readonly HumanKind _kind;
    private readonly Random _random;
    private readonly SpriteSet _sprites;
    private int _beatTimer;

    private Direction8 _direction = Direction8.Down;

    private int _offsetX;

    // this prog's persistent aim-offset on X, re-rolled occasionally
    private int _offsetY;

    private IntVector2 _position;

    // set on the first beat
    // counts up toward the next beat
    private int _walkCycleStep;

    /// <summary>Makes a prog where the human was, carrying that human's animation frames and box.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the prog.</param>
    /// <param name="kind">Which human it became; this picks the animation frames and the collision box.</param>
    /// <param name="random">The random source: the aim offsets and the re-aim rolls.</param>
    public Prog(SpriteSet sprites, IntVector2 position, HumanKind kind, Random random)
    {
        _sprites = sprites;
        _position = position;
        _kind = kind;
        _random = random;
        _collisionSize = (
            ScreenSize.ToPortPixels(kind.GetArcadeCollisionSize().Width),
            ScreenSize.ToPortPixels(kind.GetArcadeCollisionSize().Height));
        RollOffsets(); // ROM PROGST calls GPOFF at creation
    }

    /// <summary>The converted human's own box at <see cref="Position"/> (a prog keeps its victim's size).</summary>
    public Rectangle GetBounds() => new(_position.X, _position.Y, _collisionSize.Width, _collisionSize.Height);

    /// <summary>The animation frame the death explosion shatters: the phony burst card, not the human's animation frames.</summary>
    /// <returns>The phony burst card.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>PRGKIL</c> swaps the sprite to the 12x16 <c>PGXPIC</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    /// </list>
    /// </remarks>
    public Texture2D GetCurrentAnimationFrame() => _sprites.ProgBurstSprite;

    /// <summary>The explosion's rect: the burst card's size at the prog's corner.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>PRGKIL</c>/<c>EXSTV</c> swap the sprite without moving
    /// the object.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    /// </list>
    ///
    /// The explosion uses
    /// that corner with the sprite's own size. The arcade clamps the corner inside the field; a prog is
    /// always inside it already.
    /// </remarks>
    public Rectangle GetExplosionBounds() => new(
        _position.X,
        _position.Y,
        ScreenSize.ToPortPixels(CollisionSizes.ProgBurstSize.Width),
        ScreenSize.ToPortPixels(CollisionSizes.ProgBurstSize.Height));

    /// <summary>Which human's animation frames and box this prog carries (it became that human).</summary>
    public HumanKind Kind => _kind;

    /// <summary>Alive until shot; never Dying (it dies by exploding, see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Top-left of the prog.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> the OBJX/OBJY registers.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    /// </list>
    /// </remarks>
    public IntVector2 Position => _position;

    /// <summary>Test hook: the pose each ghost was frozen in, newest first.</summary>
    internal IReadOnlyList<int> GetGhostFrames()
    {
        var frames = new List<int>(_ghosts.Count);
        foreach (Ghost ghost in _ghosts)
        {
            frames.Add(ghost.AnimationFrameIndex);
        }

        return frames;
    }

    /// <summary>Test hook: the ghost trail's positions, newest first.</summary>
    internal IReadOnlyList<IntVector2> GetGhostTrail()
    {
        var positions = new List<IntVector2>(_ghosts.Count);
        foreach (Ghost ghost in _ghosts)
        {
            positions.Add(ghost.Position);
        }

        return positions;
    }

    /// <summary>The current walk animation frame: the facing direction's set, following <see cref="WalkCycle"/>.</summary>
    internal int GetWalkAnimationFrameIndex()
    {
        WalkSequence walkSequence = _direction switch
        {
            Direction8.Left => WalkSequence.Left,
            Direction8.Right => WalkSequence.Right,
            Direction8.Down => WalkSequence.Down,
            _ => WalkSequence.Up,
        };
        return (int)walkSequence * 3 + WalkCycle[_walkCycleStep];
    }

    /// <summary>How many clock units pass between one beat and the next. A prog's <see cref="_beatTimer"/> goes up by one port tick's worth of clock units each tick, and when it reaches this, a beat happens and this is subtracted from it.</summary>
    private static readonly int BeatIntervalClockUnits = ArcadeClock.ToClockUnits(BeatIntervalRomFrames);

    // which entry of WalkCycle comes next (0-3)
    // this prog's persistent aim-offset on Y

    /// <summary>One shadow-ring entry: a position the prog vacated and the pose it was drawn in.</summary>
    /// <remarks>A ghost is blitted once and never re-blitted, so it keeps its creation pose for life.</remarks>
    private readonly record struct Ghost(IntVector2 Position, int AnimationFrameIndex);

    /// <summary>Draws the ghost trail (oldest first) and then the prog, all as two-colour remap pairs.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        Texture2D[] frames = _kind.GetAnimationFrames(_sprites);
        Texture2D animationFrame = frames[GetWalkAnimationFrameIndex()];

        // Oldest ghost first so newer ones paint over it; each is drawn once, in the pose it
        // had when dropped, so the trail is frozen snapshots rather than an animation.
        for (int i = _ghosts.Count - 1; i >= 0; i--)
        {
            Ghost ghost = _ghosts[i];
            _sprites.Blitter.DrawSpriteSolidWithBackground(
                spriteBatch,
                frames[ghost.AnimationFrameIndex],
                GetBoundsAt(ghost.Position),
                _sprites.Blitter.GetSlotColour(ProgTuning.GhostBackgroundSlot),
                _sprites.Blitter.GetSlotColour(ProgTuning.GhostShapeSlot));
        }

        _sprites.Blitter.DrawSpriteSolidWithBackground(
            spriteBatch,
            animationFrame,
            GetBounds(),
            _sprites.Blitter.GetSlotColour(ProgTuning.BackgroundSlot),
            _sprites.Blitter.GetSlotColour(ProgTuning.ShapeSlot));
    }

    /// <summary>Kills the prog outright; the strip explosion is the whole visual.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>PRGKIL</c> — the object is gone immediately, leaving only
    /// the strip explosion.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    /// </list>
    ///
    /// of the
    /// sprite it swapped in.
    /// </remarks>
    public void Kill()
    {
        if (this.IsAlive())
        {
            LifeState = EntityLifeState.Dead;
        }
    }

    /// <summary>Runs one beat: animate, maybe re-roll, drop a ghost, then step.</summary>
    /// <param name="gameTime">Unused — the beat timer is counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive())
        {
            return;
        }

        if (field.RobotsFrozen())
        {
            return;
        }

        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < BeatIntervalClockUnits)
        {
            return;
        }

        // The animation advances on every beat, even if the step below is refused.
        _beatTimer -= BeatIntervalClockUnits;
        _walkCycleStep = (_walkCycleStep + 1) % WalkCycle.Length;

        // Re-roll the offsets first: picking a direction uses whatever offset is current.
        if (_random.Next(ThresholdRollSides) > ReOffsetThreshold256)
        {
            RollOffsets();
        }

        if (_random.Next(ThresholdRollSides) > ReDirectionThreshold256)
        {
            _direction = PickDirection(field);
            _walkCycleStep = 0;
        }

        // Drop a ghost at the square being left, remembering its pose. Refused steps drop one too;
        // the trail keeps 7 and never redraws an older one (ROM: PROG3).
        _ghosts.Insert(0, new Ghost(_position, GetWalkAnimationFrameIndex()));
        if (_ghosts.Count > ProgTuning.GhostCount)
        {
            _ghosts.RemoveAt(_ghosts.Count - 1);
        }

        // 2 columns (4px) on X or 4 rows (4px) on Y, on one axis only.
        int stepX = ScreenSize.ToPortPixelsFromColumns(StepXColumns);
        int stepY = ScreenSize.ToPortPixels(StepYRows);
        IntVector2 step = _direction switch
        {
            Direction8.Left => new IntVector2(-stepX, 0),
            Direction8.Right => new IntVector2(stepX, 0),
            Direction8.Up => new IntVector2(0, -stepY),
            _ => new IntVector2(0, stepY),
        };

        IntVector2 candidate = _position + step;
        Rectangle next = new(candidate.X, candidate.Y, _collisionSize.Width, _collisionSize.Height);
        if (FitsInside(field.Wall.PlayfieldBounds, next))
        {
            _position = candidate;
        }
        else
        {
            // A refused step is dropped entirely and the prog re-aims (ROM: CKLIM fails → GPDIR).
            _direction = PickDirection(field);
            _walkCycleStep = 0;
        }
    }

    /// <summary>True when the box is inside the playfield; a failure rejects the whole move.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>CKLIMV</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    /// </list>
    /// </remarks>
    private static bool FitsInside(Rectangle bounds, Rectangle box) =>
        box.X >= bounds.X
        && box.Y >= bounds.Y
        && box.Right <= bounds.Right
        && box.Bottom <= bounds.Bottom;

    /// <summary>This prog's box placed at an arbitrary position (used for the frozen ghosts).</summary>
    private Rectangle GetBoundsAt(IntVector2 position) =>
        new(position.X, position.Y, _collisionSize.Width, _collisionSize.Height);

    /// <summary>Picks the next cardinal direction: half the re-aims consider X, half Y, so never diagonal.</summary>
    /// <param name="field">The playfield: the player and the bounds to aim and wrap against.</param>
    /// <returns>The direction to walk — always one of left, right, up or down.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>GPDIR</c> — an exact match counts as "arrived", so ties
    /// turn the prog around.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    /// </list>
    /// </remarks>
    private Direction8 PickDirection(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        IntVector2 player = field.Player.Position;

        if (_random.Next(AxisFlipSides) == 0)
        {
            // The offset is in columns, so convert to pixels first.
            int aimX = player.X + ScreenSize.ToPortPixelsFromColumns(_offsetX);
            if (aimX > bounds.Right + ScreenSize.ToPortPixelsFromColumns(WrapMarginXColumns))
            {
                aimX = bounds.Left;
            }

            return aimX <= _position.X ? Direction8.Left : Direction8.Right;
        }

        int aimY = player.Y + ScreenSize.ToPortPixels(_offsetY);
        if (aimY > bounds.Bottom + ScreenSize.ToPortPixels(WrapMarginYRows))
        {
            aimY = bounds.Top;
        }

        return aimY <= _position.Y ? Direction8.Up : Direction8.Down;
    }

    /// <summary>Rolls the persistent aim offsets: X in columns (-28..+28), Y in rows (-16..+18).</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>GPOFF</c> — the offsets are the prog's standing error
    /// against the player.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    /// </list>
    /// </remarks>
    private void RollOffsets()
    {
        _offsetX = (_random.Next(1, OffsetXRollMax + 1) - OffsetXHalfRange) * OffsetXStepColumns;
        _offsetY = ((OffsetYCentre - _random.Next(1, OffsetYSteps + 1)) * OffsetYStepRows) - OffsetYSteps;
    }
}
