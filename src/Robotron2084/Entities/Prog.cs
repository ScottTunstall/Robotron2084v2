using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A prog — a reprogrammed human: it wears the human's art and hunts the player.</summary>
/// <seealso cref="Human"/>
/// <seealso cref="Explosion"/>
/// <remarks>ROM: RRB10.ASM's <c>PROGST</c>/<c>PROG</c>/<c>GPOFF</c>/<c>GPDIR</c>/<c>PRGKIL</c>
/// (notes §18). It keeps its victim's art and box, walks one cardinal direction at a time (never
/// diagonally) 4 arcade px a beat, and does not home on the player: it rolls a persistent random aim
/// offset and walks toward the player's position plus that offset, one axis at a time, re-rolling the
/// offset occasionally and the direction on a blocked step. An aim point past the field's far edge
/// wraps to the opposite edge, which is why a prog sometimes walks away. Each beat drops a ghost at
/// the square it is leaving — the newest 7 are kept, each frozen in the pose it was dropped in — giving
/// it a strobing afterimage trail. The leaving ghost is a coloured silhouette on black, the entering one
/// black on colour: exact inverses by design, not a bug. A kill wipes the trail and swaps in the
/// 12x16 <c>PGXPIC</c> card for the shared strip explosion. Timers count 5 per tick and 6 per arcade
/// frame, so an interval of N frames is due at 6 x N.</remarks>
public sealed class Prog : IExplodable, IRemovable
{
    private readonly SpriteSet _sprites;    /// <summary>How many ROM frames pass between beats.</summary>
    /// <remarks>The ROM re-runs the prog's step logic every 3 frames.</remarks>
    private const int BeatPeriodRomTicks = 3;

    /// <summary>The beat in timer units (a tick adds 5; an arcade frame is 6 units).</summary>
    private static int BeatPeriod => BeatPeriodRomTicks * 6;

    /// <summary>The horizontal step: 2 columns = 4 arcade px, the same distance as the vertical step.</summary>
    /// <remarks>ROM: the X step table moves 2 columns at a time, and a column is 2 arcade px.</remarks>
    private const int StepXColumns = 2;

    /// <summary>The vertical step: ±4 rows on Y.</summary>
    /// <remarks>ROM: the Y step table.</remarks>
    private const int StepYRows = 4;

    /// <summary>Half of the X aim-offset's range: (a roll of 1..15 minus this) times 4 columns.</summary>
    /// <remarks>ROM: <c>GPOFF</c> gives ±28 columns of offset in steps of 4.</remarks>
    private const int OffsetXHalfRange = 8;

    /// <summary>The Y aim-offset's span: a roll of 1..18 gives -16..+18 rows in steps of 2.</summary>
    /// <remarks>ROM: <c>GPOFF</c> computes this from a random 1..18 roll.</remarks>
    private const int OffsetYSteps = 18;

    /// <summary>The aim-wrap margin past the field's far edge, in columns (X) and rows (Y).</summary>
    /// <remarks>ROM: <c>GPDIR</c> wraps an aim past this margin to the opposite edge.</remarks>
    private const int WrapMarginXColumns = 0x30;
    private const int WrapMarginYRows = 18;

    /// <summary>Roll thresholds out of 256: above the first the offsets re-roll, above the second it re-aims.</summary>
    private const int ReOffsetThreshold256 = 0xF8;
    private const int ReDirectionThreshold256 = 0xE4;

    /// <summary>Collision box = the converted human's picture (per kind).</summary>
    private static (int Width, int Height) ArcadeCollisionSize(HumanKind kind) => kind switch
    {
        HumanKind.Mikey => GameplayConstants.MikeyCollisionSize,
        HumanKind.Mom => GameplayConstants.MomCollisionSize,
        _ => GameplayConstants.DadCollisionSize,
    };

    // The walk cycle: picture 1, 2, 1, 3 — the same A-B-A-C pattern the humans use.
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    private readonly Random _random;
    private readonly HumanKind _kind;
    private readonly (int Width, int Height) _collisionSize;
    private IntVector2 _position;
    private Direction8 _direction = Direction8.Down; // set on the first beat
    private int _beatTimer; // counts up toward the next beat
    private int _frameStep; // which entry of WalkCycle comes next (0-3)
    private int _offsetX;   // this prog's persistent aim-offset on X, re-rolled occasionally
    private int _offsetY;   // this prog's persistent aim-offset on Y

    /// <summary>One shadow-ring entry: a position the prog vacated and the pose it was drawn in.</summary>
    /// <remarks>A ghost is blitted once and never re-blitted, so it keeps its creation pose for life.</remarks>
    private readonly record struct Ghost(IntVector2 Position, int FrameIndex);

    /// <summary>The shadow ring, NEWEST FIRST (see <see cref="Ghost"/>).</summary>
    private readonly List<Ghost> _ghosts = new();

    /// <summary>Test hook: the ghost trail's positions, newest first.</summary>
    internal IReadOnlyList<IntVector2> GhostTrail
    {
        get
        {
            var positions = new List<IntVector2>(_ghosts.Count);
            foreach (Ghost ghost in _ghosts)
            {
                positions.Add(ghost.Position);
            }

            return positions;
        }
    }

    /// <summary>Test hook: the pose each ghost was frozen in, newest first.</summary>
    internal IReadOnlyList<int> GhostFrames
    {
        get
        {
            var frames = new List<int>(_ghosts.Count);
            foreach (Ghost ghost in _ghosts)
            {
                frames.Add(ghost.FrameIndex);
            }

            return frames;
        }
    }

    /// <summary>Makes a prog where the human was, carrying that human's art and box.</summary>
    /// <param name="position">Top-left of the prog.</param>
    /// <param name="kind">Which human it became; this picks the art and the collision box.</param>
    /// <param name="random">The random source: the aim offsets and the re-aim rolls.</param>
    public Prog(SpriteSet sprites, IntVector2 position, HumanKind kind, Random random)
    {
        _sprites = sprites;
        _position = position;
        _kind = kind;
        _random = random;
        _collisionSize = (
            ScreenSize.Scaled(ArcadeCollisionSize(kind).Width),
            ScreenSize.Scaled(ArcadeCollisionSize(kind).Height));
        RollOffsets(); // ROM PROGST calls GPOFF at creation
    }

    /// <summary>Which human's art/box this prog carries (it became that human).</summary>
    public HumanKind Kind => _kind;

    /// <summary>Top-left of the prog (the ROM's OBJX/OBJY).</summary>
    public IntVector2 Position => _position;

    /// <summary>The converted human's own box at <see cref="Position"/> (a prog keeps its victim's size).</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, _collisionSize.Width, _collisionSize.Height);

    /// <summary>Alive until shot; never Dying (it dies by exploding, see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Kills the prog outright; the strip explosion is the whole visual.</summary>
    /// <remarks>ROM: <c>PRGKIL</c> — the object is gone immediately, leaving only the strip explosion
    /// of the picture it swapped in.</remarks>
    public void Kill()
    {
        if (LifeState == EntityLifeState.Alive)
        {
            LifeState = EntityLifeState.Dead;
        }
    }

    /// <summary>The picture the death explosion shatters: the phony burst card, not the human art.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <returns>The phony burst card.</returns>
    /// <remarks>ROM: <c>PRGKIL</c> swaps the picture to the 12x16 <c>PGXPIC</c>.</remarks>
    public Texture2D CurrentAnimationFrame => _sprites.ProgBurst;

    /// <summary>The explosion's rect: the burst card's size at the prog's corner.</summary>
    /// <remarks>ROM: <c>PRGKIL</c>/<c>EXSTV</c> swap the picture without moving the object, and the
    /// explosion uses that corner with the PICTURE's own size. The arcade clamps the corner inside the
    /// field; a prog is always inside it already.</remarks>
    public Rectangle ExplosionBounds => new(
        _position.X,
        _position.Y,
        ScreenSize.Scaled(GameplayConstants.ProgBurstSize.Width),
        ScreenSize.Scaled(GameplayConstants.ProgBurstSize.Height));

    /// <summary>Runs one beat: animate, maybe re-roll, drop a ghost, then step.</summary>
    /// <param name="gameTime">Unused — the beat timer is counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        if (field.RobotsFrozen)
        {
            return;
        }

        _beatTimer += 5;
        if (_beatTimer < BeatPeriod)
        {
            return;
        }

        // The animation advances on every beat, even if the step below is refused.
        _beatTimer -= BeatPeriod;
        _frameStep = (_frameStep + 1) % WalkCycle.Length;

        // Re-roll the offsets first: picking a direction uses whatever offset is current.
        if (_random.Next(256) > ReOffsetThreshold256)
        {
            RollOffsets();
        }

        if (_random.Next(256) > ReDirectionThreshold256)
        {
            _direction = PickDirection(field);
            _frameStep = 0;
        }

        // Drop a ghost at the square being left, remembering its pose. Refused steps drop one too;
        // the trail keeps 7 and never redraws an older one (ROM: PROG3).
        _ghosts.Insert(0, new Ghost(_position, WalkFrameIndex));
        if (_ghosts.Count > GameplayConstants.ProgGhostCount)
        {
            _ghosts.RemoveAt(_ghosts.Count - 1);
        }

        // 2 columns (4px) on X or 4 rows (4px) on Y, on one axis only.
        int stepX = ScreenSize.Scaled(StepXColumns * GameplayConstants.ArcadePixelsPerColumn);
        int stepY = ScreenSize.Scaled(StepYRows);
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
            _frameStep = 0;
        }
    }

    /// <summary>True when the box is inside the playfield; a failure rejects the whole move.</summary>
    /// <remarks>ROM: <c>CKLIMV</c>.</remarks>
    private static bool FitsInside(Rectangle bounds, Rectangle box) =>
        box.X >= bounds.X
        && box.Y >= bounds.Y
        && box.Right <= bounds.Right
        && box.Bottom <= bounds.Bottom;

    /// <summary>Rolls the persistent aim offsets: X in columns (-28..+28), Y in rows (-16..+18).</summary>
    /// <remarks>ROM: <c>GPOFF</c> — the offsets are the prog's standing error against the player.</remarks>
    private void RollOffsets()
    {
        _offsetX = (_random.Next(1, 16) - OffsetXHalfRange) * 4;
        _offsetY = ((19 - _random.Next(1, OffsetYSteps + 1)) * 2) - OffsetYSteps;
    }

    /// <summary>Picks the next cardinal direction: half the re-aims consider X, half Y, so never diagonal.</summary>
    /// <param name="field">The playfield: the player and the bounds to aim and wrap against.</param>
    /// <returns>The direction to walk — always one of left, right, up or down.</returns>
    /// <remarks>ROM: <c>GPDIR</c> — an exact match counts as "arrived", so ties turn the prog around.</remarks>
    private Direction8 PickDirection(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        IntVector2 player = field.Player.Position;

        if (_random.Next(2) == 0)
        {
            // The offset is in columns, so convert to pixels first.
            int aimX = player.X + ScreenSize.Scaled(_offsetX * GameplayConstants.ArcadePixelsPerColumn);
            if (aimX > bounds.Right + ScreenSize.Scaled(WrapMarginXColumns * GameplayConstants.ArcadePixelsPerColumn))
            {
                aimX = bounds.Left;
            }

            return aimX <= _position.X ? Direction8.Left : Direction8.Right;
        }

        int aimY = player.Y + ScreenSize.Scaled(_offsetY);
        if (aimY > bounds.Bottom + ScreenSize.Scaled(WrapMarginYRows))
        {
            aimY = bounds.Top;
        }

        return aimY <= _position.Y ? Direction8.Up : Direction8.Down;
    }

    /// <summary>The current walk picture: the facing direction's set, following <see cref="WalkCycle"/>.</summary>
    internal int WalkFrameIndex
    {
        get
        {
            int set = _direction switch
            {
                Direction8.Left => 0,
                Direction8.Right => 1,
                Direction8.Down => 2,
                _ => 3,
            };
            return set * 3 + WalkCycle[_frameStep];
        }
    }

    /// <summary>Draws the ghost trail (oldest first) and then the prog, all as two-colour remap pairs.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        Texture2D[] frames = _kind switch
        {
            HumanKind.Mikey => _sprites.MikeyFrames,
            HumanKind.Mom => _sprites.MomFrames,
            _ => _sprites.DadFrames,
        };
        Texture2D picture = frames[WalkFrameIndex];

        // Oldest ghost first so newer ones paint over it; each is drawn once, in the pose it
        // had when dropped, so the trail is frozen snapshots rather than an animation.
        for (int i = _ghosts.Count - 1; i >= 0; i--)
        {
            Ghost ghost = _ghosts[i];
            _sprites.DrawSpriteSolidWithBackground(
                spriteBatch,
                frames[ghost.FrameIndex],
                BoundsAt(ghost.Position),
                _sprites.SlotColor(GameplayConstants.ProgGhostBackgroundSlot),
                _sprites.SlotColor(GameplayConstants.ProgGhostShapeSlot));
        }

        _sprites.DrawSpriteSolidWithBackground(
            spriteBatch,
            picture,
            Bounds,
            _sprites.SlotColor(GameplayConstants.ProgBackgroundSlot),
            _sprites.SlotColor(GameplayConstants.ProgShapeSlot));
    }

    /// <summary>This prog's box placed at an arbitrary position (used for the frozen ghosts).</summary>
    private Rectangle BoundsAt(IntVector2 position) =>
        new(position.X, position.Y, _collisionSize.Width, _collisionSize.Height);
}