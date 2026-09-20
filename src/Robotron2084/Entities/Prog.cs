using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A prog — a rescuable human that a <see cref="Brain"/> has "reprogrammed": once the
/// conversion finishes, the human sprite is gone and a prog appears in its place, wearing
/// that same human's look but now hostile — touching the player kills the player, exactly
/// like any other enemy robot. It keeps the art and collision box of the human it became.
///
/// It walks in straight lines only — up, down, left or right, never diagonally — moving 4
/// arcade pixels every time it wakes up (this codebase calls one such wake-up a "beat"; see
/// <see cref="IEntity"/> for that and the "ROM frame"/"fifths" units used below), on
/// whichever single axis it's currently walking.
///
/// It does NOT home in on the player directly. Instead, each prog rolls a persistent random
/// offset from the player's position and walks toward THAT point instead — one prog might
/// aim 20 pixels to the player's left, another 10 pixels above — and only ever chases either
/// the X part of that offset or the Y part, never both at once, so a prog always walks in a
/// straight cardinal line rather than diagonally at its aim point. This offset is re-rolled
/// occasionally (making the prog change its aim over time) and re-picked immediately if a
/// step is blocked; if the aim point would fall off the far edge of the playfield, it wraps
/// to the opposite edge instead, which is what occasionally makes a prog turn and walk AWAY
/// from the player for a while.
///
/// It is never drawn as an ordinary sprite. Instead it leaves a shimmering trail: as it
/// walks, the square it's leaving is briefly painted as a solid colour block with a black
/// silhouette on top, and the square it's entering as a black block with a coloured
/// silhouette — and the last 7 of these "ghost" squares stay frozen on screen at once,
/// fading only when overwritten by a newer one, giving a prog a strobing afterimage trail
/// that makes it visually distinct from every other enemy.
///
/// A laser hit kills it the same way every other robot dies — it bursts into the shared
/// strip explosion — except the burst uses a fixed stand-in picture rather than whatever
/// human art the prog happened to be walking in. A prog is worth 100 points.
/// </summary>
/// <remarks>
/// Ported from the arcade's own prog behaviour (ROM: RRB10.ASM's `PROGST`/`PROG`; notes
/// §18, §46). The ROM measures its horizontal step in 2-pixel-wide "columns" (an artifact
/// of how the original video hardware addresses the screen), so a step of "2 columns" is
/// really 4 pixels — the same distance as the vertical step (notes §55/§87).
///
/// The trail's colour pairs are exact inverses of each other BY DESIGN, not a bug: the
/// "leaving" ghost is a solid colour with a black silhouette, the "entering" ghost is a
/// solid black with a coloured silhouette (notes §53). Don't "fix" this without checking.
///
/// On a laser kill, the arcade wipes the whole ghost trail, swaps in the fixed stand-in
/// picture, nudges the position fully inside the playfield if it's overhanging an edge, and
/// then runs the same shared strip-explosion effect every other robot uses (ROM: `PRGKIL`).
/// </remarks>
public sealed class Prog : IExplodable
{
    /// <summary>How many ROM frames pass between two of the prog's wake-ups (see the class summary
    /// for what a wake-up, called a "beat" in this codebase, is).</summary>
    /// <remarks>The ROM re-runs the prog's step logic every 3 frames.</remarks>
    private const int BeatPeriodRomTicks = 3;

    /// <summary>The wake-up period above, converted to the fixed-point "fifths" clock (see
    /// <see cref="IEntity"/>): 3 ROM frames is 3.6 port ticks, not a whole number.</summary>
    /// <remarks>Notes §52, §65.</remarks>
    private static int BeatPeriod => BeatPeriodRomTicks * 6;

    /// <summary>
    /// The horizontal step size, in the arcade's own "columns" unit: 2 columns, which comes out to
    /// four arcade pixels — the same distance the vertical step below covers.
    /// </summary>
    /// <remarks>
    /// The arcade's own X step table moves 2 columns at a time, and a column is 2 arcade pixels, so
    /// this is really a 4-pixel step, matching the vertical step exactly (notes §55/§87).
    /// </remarks>
    private const int StepXColumns = 2;

    /// <summary>The vertical step: ±4 rows on Y.</summary>
    /// <remarks>The arcade's own Y step table.</remarks>
    private const int StepYRows = 4;

    /// <summary>Arcade pixels in one ROM column (the video buffer is `column*256 + row`).</summary>
    private const int ArcadePixelsPerColumn = 2;

    /// <summary>Half of the X aim-offset's range: (a roll of 1..15 minus this) times 4 columns.</summary>
    /// <remarks>The arcade rolls a random 1..15, subtracts this, and scales by 4 columns, giving ±28
    /// columns of offset in steps of 4 (ROM: `GPOFF`).</remarks>
    private const int OffsetXHalfRange = 8;

    /// <summary>The Y aim-offset's span: a roll of 1..18 gives -16..+18 rows in steps of 2.</summary>
    /// <remarks>The arcade computes this from a random 1..18 roll (ROM: `GPOFF`).</remarks>
    private const int OffsetYSteps = 18;

    /// <summary>The aim-wrap margin past the field's far edge, in columns (X) and rows (Y); beyond it the aim wraps to the opposite edge.</summary>
    /// <remarks>The arcade wraps an aim point that falls this far past the field's far edge back to the
    /// opposite edge (ROM: `GPDIR`).</remarks>
    private const int WrapMarginXColumns = 0x30;
    private const int WrapMarginYRows = 18;

    /// <summary>Roll thresholds out of 256: above the first, the aim offsets are re-rolled;
    /// above the second, the prog re-aims.</summary>
    /// <remarks>Roughly 3% of wake-ups re-roll the offsets and roughly 10% re-aim (ROM: the prog's
    /// step routine).</remarks>
    private const int ReOffsetThreshold256 = 0xF8;
    private const int ReDirectionThreshold256 = 0xE4;

    /// <summary>Collision box = the converted human's picture (per kind).</summary>
    private static (int Width, int Height) ArcadeCollisionSize(HumanKind kind) => kind switch
    {
        HumanKind.Mikey => GameplayConstants.MikeyCollisionSize,
        HumanKind.Mom => GameplayConstants.MomCollisionSize,
        _ => GameplayConstants.DadCollisionSize,
    };

    // The walk animation plays picture 1, then 2, then back to 1, then 3, then repeats
    // (a "first, second, first, third" pattern) through the human's 3 walk pictures.
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    private readonly Random _random;
    private readonly HumanKind _kind;
    private readonly (int Width, int Height) _collisionSize;
    private IntVector2 _position;
    private Direction8 _direction = Direction8.Down; // one of the 4 cardinal directions; set on the first wake-up
    private int _beatTimer; // counts up toward the next wake-up
    private int _frameStep; // which entry of WalkCycle comes next (0-3)
    private int _offsetX;   // this prog's persistent aim-offset on X, re-rolled occasionally
    private int _offsetY;   // this prog's persistent aim-offset on Y

    /// <summary>
    /// One shadow-ring entry: a position this prog vacated, plus the pose it was
    /// drawn in at that moment. A ghost is blitted once and never re-blitted, so it
    /// keeps its creation pose for life.
    /// </summary>
    /// <remarks>The ROM blits a ghost with `HUMON` at `OPICT` and erases it with `PCTOFF`.</remarks>
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
    public Prog(IntVector2 position, HumanKind kind, Random random)
    {
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

    /// <summary>
    /// Kills the prog: it goes straight to Dead and leaves the strip explosion of the
    /// phony burst card for the field to run (see <see cref="CurrentFrameArt"/>). There
    /// is no Dying state.
    /// </summary>
    /// <remarks>
    /// The object is GONE the moment it is hit, and the only thing left is the strip
    /// explosion of the picture it swapped in — the field's, because
    /// <see cref="CurrentFrameArt"/> is now the phony card (ROM: `PRGKIL`; notes §90).
    /// </remarks>
    public void Kill()
    {
        if (LifeState == EntityLifeState.Alive)
        {
            LifeState = EntityLifeState.Dead;
        }
    }

    /// <summary>
    /// The picture the death explosion shatters: the phony burst card, NOT the human
    /// art the prog was walking in.
    /// </summary>
    /// <param name="sprites">The shared sprite set, which holds the phony burst card.</param>
    /// <returns>The phony burst card.</returns>
    /// <remarks>ROM PRGKIL swaps the object's picture to the 12×16 `PGXPIC`.</remarks>
    public Texture2D CurrentFrameArt(SpriteSet sprites) => sprites.ProgBurst;

    /// <summary>
    /// The explosion's rect: the phony burst card's own size at the prog's corner,
    /// not the (smaller) human box it was standing in.
    /// </summary>
    /// <remarks>
    /// The arcade swaps the picture to the 12x16 phony burst card without moving the
    /// object, and the explosion's rectangle is that same top-left corner with the
    /// PICTURE's own width and height. The arcade also clamps that corner to stay just
    /// inside the playfield; a prog is always inside the play area, so the port's
    /// corner already is. (ROM: `PRGKIL`, `EXSTV`.)
    /// </remarks>
    public Rectangle ExplosionBounds => new(
        _position.X,
        _position.Y,
        ScreenSize.Scaled(GameplayConstants.ProgBurstSize.Width),
        ScreenSize.Scaled(GameplayConstants.ProgBurstSize.Height));

    /// <summary>
    /// Runs one wake-up when it's due: advances the walk animation, maybe rolls a fresh aim offset or
    /// a fresh direction, pushes the ghost trail along, then takes one step in its current direction
    /// — or picks a new direction instead if that step would leave the playfield. Held still while
    /// <see cref="PlayField.RobotsFrozen"/>.
    /// </summary>
    /// <param name="gameTime">Unused — the wake-up timer is counted in ROM frames, not real time.</param>
    /// <param name="field">The playfield: the player to aim off and the walls to stay inside.</param>
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

        // Not time to wake up yet. (3 ROM frames is 3.6 port ticks, not a whole number —
        // tracked exactly with the fixed-point "fifths" clock, see IEntity — rounding this
        // down to 3 whole ticks used to make the prog run 20% too fast.)
        _beatTimer += 5;
        if (_beatTimer < BeatPeriod)
        {
            return;
        }

        // Advance the walk animation first — it always advances on a wake-up, even if the
        // step below turns out to be refused.
        _beatTimer -= BeatPeriod;
        _frameStep = (_frameStep + 1) % WalkCycle.Length;

        // Re-roll the aim offset on ~3% of wake-ups, then possibly pick a fresh direction on
        // ~10% — in that order, because picking a direction uses whatever offset is current.
        if (_random.Next(256) > ReOffsetThreshold256)
        {
            RollOffsets();
        }

        if (_random.Next(256) > ReDirectionThreshold256)
        {
            _direction = PickDirection(field);
            _frameStep = 0; // a fresh direction restarts the walk at its first frame
        }

        // Drop a ghost at the position the prog is about to leave, and remember its pose too
        // — this happens every wake-up regardless of whether the step below actually
        // succeeds (a refused step just drops a ghost on top of where the prog already is).
        // The trail keeps the most recent 7 ghosts and never redraws an older one, which is
        // why it looks "frozen" rather than animated. (ROM: `PROG3`.)
        _ghosts.Insert(0, new Ghost(_position, WalkFrameIndex));
        if (_ghosts.Count > GameplayConstants.ProgGhostCount)
        {
            _ghosts.RemoveAt(_ghosts.Count - 1); // drop the oldest ghost once the trail is full
        }

        // Take the step: 2 columns (4px) on X or 4 rows (4px) on Y — always the same 4px
        // distance regardless of axis, matching the class summary above. Only one axis moves,
        // since the direction is always a single cardinal direction.
        int stepX = ScreenSize.Scaled(StepXColumns * ArcadePixelsPerColumn);
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
            // A step that would leave the playfield is dropped entirely, and the prog
            // re-aims instead (ROM: `CKLIM` fails, falling through to `PROGND`/`GPDIR`).
            _direction = PickDirection(field);
            _frameStep = 0;
        }
    }

    /// <summary>
    /// True when the object's picture box is inside the playfield; a failure REJECTS
    /// the whole move (it is not clamped, and the prog never slides along a wall).
    /// </summary>
    /// <remarks>ROM: `CKLIMV`.</remarks>
    private static bool FitsInside(Rectangle bounds, Rectangle box) =>
        box.X >= bounds.X
        && box.Y >= bounds.Y
        && box.Right <= bounds.Right
        && box.Bottom <= bounds.Bottom;

    /// <summary>
    /// Rolls the persistent aim offsets, in the playfield's own units — X in columns
    /// (-28..+28, i.e. -56..+56 arcade px) and Y in rows (-16..+18). These are what stop a
    /// prog from steering straight at you: it walks toward where you were plus its own
    /// standing error.
    /// </summary>
    /// <remarks>ROM: `GPOFF`.</remarks>
    private void RollOffsets()
    {
        _offsetX = (_random.Next(1, 16) - OffsetXHalfRange) * 4;
        _offsetY = ((19 - _random.Next(1, OffsetYSteps + 1)) * 2) - OffsetYSteps;
    }

    /// <summary>
    /// Picks the next cardinal direction: HALF of all re-aims consider X and half
    /// consider Y, so a prog never walks diagonally. The chosen axis aims at your
    /// coordinate plus this prog's offset; an aim point past the field's far edge by
    /// the margin wraps to the opposite edge. Ties walk left/up ("equal" turns
    /// around).
    /// </summary>
    /// <param name="field">The playfield: the player and the bounds to aim and wrap against.</param>
    /// <returns>The direction to walk — always one of left, right, up or down.</returns>
    /// <remarks>The arcade picks the axis with a coin flip and treats an exact match as "arrived", so
    /// ties turn the prog around rather than leave it standing still. (ROM: `GPDIR`.)</remarks>
    private Direction8 PickDirection(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        IntVector2 player = field.Player.Position;

        if (_random.Next(2) == 0)
        {
            // The offset is in COLUMNS, so it becomes pixels first (notes §55).
            int aimX = player.X + ScreenSize.Scaled(_offsetX * ArcadePixelsPerColumn);
            if (aimX > bounds.Right + ScreenSize.Scaled(WrapMarginXColumns * ArcadePixelsPerColumn))
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

    /// <summary>The current walk picture: which of the facing direction's 3 human pictures to show,
    /// following the "first, second, first, third" pattern described on <see cref="WalkCycle"/>.</summary>
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

    /// <summary>
    /// Draws the ghost trail (oldest first, each in the pose it was frozen in) and then the prog
    /// itself, all as the ROM's two-colour remap pairs — a prog is never drawn as a plain sprite.
    /// </summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the human frames and slot colours.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        Texture2D[] frames = _kind switch
        {
            HumanKind.Mikey => sprites.MikeyFrames,
            HumanKind.Mom => sprites.MomFrames,
            _ => sprites.DadFrames,
        };
        Texture2D art = frames[WalkFrameIndex];

        // Oldest ghost first so the newer ones paint over it (a prog that's blocked draws
        // its ghost and itself in the same spot), each shown in whatever walk pose it had
        // the moment it was dropped — a ghost is drawn once and never updated again, so the
        // trail looks like a series of frozen snapshots, not one smoothly animated shape.
        for (int i = _ghosts.Count - 1; i >= 0; i--)
        {
            Ghost ghost = _ghosts[i];
            sprites.DrawSpriteSolidWithBackground(
                spriteBatch,
                frames[ghost.FrameIndex],
                BoundsAt(ghost.Position),
                sprites.SlotColor(GameplayConstants.ProgGhostBackgroundSlot),
                sprites.SlotColor(GameplayConstants.ProgGhostShapeSlot));
        }

        sprites.DrawSpriteSolidWithBackground(
            spriteBatch,
            art,
            Bounds,
            sprites.SlotColor(GameplayConstants.ProgBackgroundSlot),
            sprites.SlotColor(GameplayConstants.ProgShapeSlot));
    }

    /// <summary>This prog's box placed at an arbitrary position (used for the frozen ghosts).</summary>
    private Rectangle BoundsAt(IntVector2 position) =>
        new(position.X, position.Y, _collisionSize.Width, _collisionSize.Height);
}
