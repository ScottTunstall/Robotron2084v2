using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// One member of the human family the player is trying to rescue this wave (Mikey, Mom or Dad — see
/// <see cref="HumanKind"/>). Humans are not enemies and never attack: they are the game's objective.
/// A robot that touches one kills it and leaves a skull behind (a lost rescue), and the player
/// rescues one simply by touching it — the running rescued-this-wave count pays an escalating
/// 1000-5000 point bonus. See <see cref="IEntity"/> for the "beat" / "ROM frame" / "..Timer" /
/// "notes §NN" terminology used throughout this class.
///
/// They wander the field aimlessly in short steps: a random direction for a random number of steps,
/// or a new direction straight away if the next step would leave the play area or land on a standing
/// (not yet destroyed) electrode. Each member has its own walk art, staggered start and direction, so
/// a group of humans doesn't all move in lockstep. Unlike the robots, humans do NOT wait for the wave
/// to start — they set off immediately (see <see cref="Update"/>) — and a <see cref="Brain"/> that
/// touches one may spend a few seconds "reprogramming" it into a <c>Prog</c> (a hostile impostor)
/// instead of killing it outright; see <see cref="IsBeingReprogrammed"/>.
/// </summary>
/// <remarks>
/// Ported from the arcade's own human wander behaviour, routine for routine (ROM: RRH11.ASM, the
/// `HUMAN` process and its `HUMATB` walk table). The walk cycles through 8 direction blocks (4
/// cardinal, 4 diagonal), each made of 4 substeps that alternate 2-then-1 arcade pixels on the major
/// axis (diagonals also add a steady 1px on the minor axis), one substep taken per step period. The
/// arcade's own step period is faster than this port's: the ROM-accurate pace read as "mommies walking
/// too fast" in playtesting, so this port deliberately halves the walk speed (see
/// <see cref="StepPeriodRomTicks"/> — the same kind of deliberate retune applied to the spheroid and
/// enforcer). A new random direction comes every 1-128 steps, or immediately if the next step would
/// leave the playfield or land on a standing electrode. Each human's very first step is delayed by a
/// small random stagger (1-8 ticks) so a group spawned together doesn't all move in lockstep. Animation:
/// 12 frames per family member (4 directions times 3 walk frames, the same layout as the player's own
/// frames); the diagonal directions reuse the left/right frame sets rather than having frames of their own.
/// </remarks>
public sealed class Human : IEntity
{
    /// <summary>
    /// The step period, in ROM frames: how long each step is held. **This is the ONE
    /// deliberate gameplay override in the port** — humans walk at half the arcade rate,
    /// so DO NOT "fix" this to 8 without asking.
    /// </summary>
    /// <remarks>
    /// The arcade's own step period is half this (one step every 8 ROM frames, moving one arcade
    /// pixel each time). This port deliberately slows it to one step every 16 frames — the
    /// ROM-accurate pace read as "the mommies are walking too fast" in playtesting. Do not "fix"
    /// this back to 8 without asking (notes §70).
    /// </remarks>
    private const int StepPeriodRomTicks = 16;

    /// <summary>
    /// The walk table: one "block" of 4 substeps for each of the 8 possible travel directions (4
    /// cardinal + 4 diagonal). Each substep is (delta X, delta Y, walk-frame index within the
    /// direction's 3-frame set) — the human plays through a block's 4 substeps in order, one per
    /// step, looping back to the first once it reaches the end (see <see cref="Update"/>'s
    /// <c>_subStep</c> use). Deltas are in arcade pixels.
    /// Block order: LEFT, RIGHT, DOWN, UP, UP+LEFT, RIGHT+UP, RIGHT+DOWN,
    /// DOWN+LEFT.
    /// </summary>
    /// <remarks>Copied directly from the ROM's own walk table, which lists each substep as a picture
    /// number plus its X/Y delta (ROM: `HUMATB`).</remarks>
    private static readonly (int Dx, int Dy, int Frame)[] Steps =
    {
        // LEFT
        (-2, 0, 0), (-1, 0, 1), (-2, 0, 0), (-1, 0, 2),
        // RIGHT
        (2, 0, 0), (1, 0, 1), (2, 0, 0), (1, 0, 2),
        // DOWN
        (0, 1, 0), (0, 1, 1), (0, 1, 0), (0, 1, 2),
        // UP
        (0, -1, 0), (0, -1, 1), (0, -1, 0), (0, -1, 2),
        // UP+LEFT
        (-2, -1, 0), (-1, -1, 2), (-2, -1, 0), (-1, -1, 2),
        // RIGHT+UP
        (2, -1, 0), (1, -1, 1), (2, -1, 0), (1, -1, 2),
        // RIGHT+DOWN
        (2, 1, 0), (1, 1, 1), (2, 1, 0), (1, 1, 2),
        // DOWN+LEFT
        (-2, 1, 0), (-1, 1, 1), (-2, 1, 0), (-1, 1, 1),
    };

    /// <summary>
    /// Which 3-frame set each block animates from — the diagonals reuse
    /// the cardinal frame sets (UP+LEFT/DOWN+LEFT → left set,
    /// RIGHT+UP/RIGHT+DOWN → right set): [L,R,D,U,L,R,R,L].
    /// </summary>
    private static readonly int[] FrameSet = { 0, 1, 2, 3, 0, 1, 1, 0 };

    /// <summary>The member's collision box in arcade pixels, before scaling.</summary>
    /// <remarks>The ROM picture's dimensions for the member.</remarks>
    private static (int Width, int Height) ArcadeCollisionSize(HumanKind kind) => kind switch
    {
        HumanKind.Mikey => GameplayConstants.MikeyCollisionSize,
        HumanKind.Mom => GameplayConstants.MomCollisionSize,
        _ => GameplayConstants.DadCollisionSize,
    };

    /// <summary>
    /// The largest side of this member's box in port pixels — the square the spawner must keep
    /// clear (the boxes themselves are not square). Keeping a human off the electrodes is
    /// otherwise a trap: the walk refuses a step into a live electrode, so one placed inside
    /// stands there for the rest of the wave.
    /// </summary>
    /// <param name="kind">The member whose box is measured.</param>
    /// <returns>The square's side, in port pixels.</returns>
    /// <remarks>Notes §77, §88.</remarks>
    internal static int SpawnSquarePortPixels(HumanKind kind)
    {
        (int width, int height) = ArcadeCollisionSize(kind);
        return ScreenSize.Scaled(Math.Max(width, height));
    }

    private readonly Random _random;
    private readonly HumanKind _kind;
    private IntVector2 _position;
    private int _directionBlock; // Which of the 8 direction blocks (see Steps) the human is currently walking.
    private int _subStep;        // Which of the 4 substeps within that block comes next (0-3).
    private int _stepTimer;     // Counts up to the next step, in fixed-point fifths of a port tick — see IEntity.
    private int _reDirStepsRemaining; // Steps left before the human rolls a fresh direction.
    private int _startStaggerTicks;   // Ticks left before this human's very first step (staggers group spawns).
    private int _frame;          // Current walk picture index, 0-11 into this family member's 12 frames.

    /// <summary>Creates one family member with its own stagger and starting direction.</summary>
    /// <param name="position">Top-left of the human.</param>
    /// <param name="kind">Which member — it decides the art and the collision box.</param>
    /// <param name="random">The random source for the direction, the step count and the stagger.</param>
    public Human(IntVector2 position, HumanKind kind, Random random)
    {
        _position = position;
        _kind = kind;
        _random = random;
        _directionBlock = random.Next(8);
        _reDirStepsRemaining = 1 + random.Next(128); // A fresh direction comes every 1-128 steps.
        _startStaggerTicks = 1 + random.Next(8);     // Wait 1-8 ticks before the very first step.
        _stepTimer = 0;                             // The stagger's last tick doubles as the first step.
    }

    /// <summary>Which member this is (Mikey, Mum or Dad) — it decides the art and the box.</summary>
    public HumanKind Kind => _kind;

    /// <summary>Top-left of the human.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>This member's own picture box at <see cref="Position"/>.</summary>
    public Rectangle Bounds
    {
        get
        {
            (int w, int h) = ArcadeCollisionSize(_kind);
            return new(_position.X, _position.Y, ScreenSize.Scaled(w), ScreenSize.Scaled(h));
        }
    }

    /// <summary>Alive until a robot kills it, the player rescues it, or a brain finishes reprogramming it.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Steps taken so far — test hook for the step cadence.</summary>
    /// <remarks>Notes §65, §70.</remarks>
    internal int StepCount { get; private set; }

    /// <summary>Killed by a robot: gone at once, with no death animation. The skull is the field's.</summary>
    /// <remarks>The human's picture is simply switched off, with no death animation (ROM: `DMAOFF`).</remarks>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>Rescued by the player: gone at once, and the field awards the bonus.</summary>
    public void Rescue() => LifeState = EntityLifeState.Dead;

    /// <summary>
    /// Walks the current direction block one substep at a time: waits out the start stagger, then takes the
    /// next step on the step clock — re-aiming instead of moving when the next step would leave the
    /// playfield or land on a standing electrode.
    /// </summary>
    /// <param name="gameTime">Unused — the step period is counted in ROM frames.</param>
    /// <param name="field">The playfield: the wall and the electrodes the walk refuses to enter.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        // Deliberately no "robots frozen" gate here: every robot in the original waits for a
        // wave-start flag before it's allowed to move, but the human family is created before
        // that flag is set, so in the arcade humans start wandering immediately — while the
        // robots are still assembling and the player still can't move (notes §88).
        if (LifeState != EntityLifeState.Alive || IsBeingReprogrammed)
        {
            return;
        }

        if (_startStaggerTicks > 0)
        {
            _startStaggerTicks--;
            if (_startStaggerTicks > 0)
            {
                return;
            }

            // The stagger's very last tick doubles as the human's first step — it wakes up and
            // walks on the same tick the stagger runs out, rather than waiting one extra tick
            // after. That keeps every later step falling on the same clean, evenly-spaced
            // schedule the arcade uses (notes §88).
        }
        else
        {
            // A step's length in port ticks isn't a whole number (19.2, not 19), so it's tracked
            // with the fixed-point fifths trick rather than rounded down — rounding down walked
            // a human 1% too fast, drifting one tick further ahead every 5 steps (notes §52/§65).
            _stepTimer += 5;
            if (_stepTimer < StepPeriodRomTicks * 6)
            {
                return;
            }

            _stepTimer -= StepPeriodRomTicks * 6;
        }

        StepCount++;

        (int dx, int dy, int frame) = Steps[_directionBlock * 4 + _subStep];
        _frame = 3 * FrameSet[_directionBlock] + frame;

        // Each unit in the walk table is one arcade pixel.
        IntVector2 candidate = _position + new IntVector2(dx, dy) * ScreenSize.SpecScale;
        Rectangle next = Bounds with { X = candidate.X, Y = candidate.Y };
        if (field.Wall.Intersects(next) || OverlapsLivingElectrode(next, field))
        {
            // Blocked by the wall or a standing electrode: pick a fresh direction instead of
            // taking this step.
            PickNewDirection();
            return;
        }

        _position = candidate;
        _subStep = (_subStep + 1) % 4;
        if (--_reDirStepsRemaining <= 0)
        {
            PickNewDirection();
        }
    }

    /// <summary>True when the human's next step would overlap an electrode that is still standing.</summary>
    private static bool OverlapsLivingElectrode(Rectangle next, PlayField field)
    {
        foreach (Electrode electrode in field.Electrodes)
        {
            if (electrode.LifeState == EntityLifeState.Alive && electrode.Bounds.Overlaps(next))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Picks a fresh direction and re-rolls how many steps to take before the next change.</summary>
    /// <remarks>Rolls a random 1-128 steps before the next change, and a random one of the 8 blocks
    /// in <see cref="Steps"/> to walk.</remarks>
    private void PickNewDirection()
    {
        _directionBlock = _random.Next(8);
        _subStep = 0;
        _frame = 3 * FrameSet[_directionBlock];
        _reDirStepsRemaining = 1 + _random.Next(128);
    }

    /// <summary>Draws the walk frame — or, while a brain is working on this human, the flashing two-colour shape.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the family frames.</param>
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

        if (IsBeingReprogrammed)
        {
            // While being reprogrammed, the human is drawn as a solid silhouette of its own walk
            // picture in one colour, over a solid background rectangle in a second colour — and
            // both colours are cycling palette slots, which is what produces the flashing effect
            // (ROM: `BMUT`'s `HUMON`, notes §47).
            sprites.DrawSpriteSolidWithBackground(
                spriteBatch,
                frames[_frame],
                Bounds,
                sprites.SlotColor(GameplayConstants.ReprogramBackgroundSlot),
                sprites.SlotColor(GameplayConstants.ReprogramShapeSlot));
            return;
        }

        sprites.DrawSprite(spriteBatch, frames[_frame], Bounds, Color.White);
    }

    /// <summary>
    /// True while a brain is working on this human: it does not walk, cannot be rescued or killed, and is
    /// drawn as the flashing two-colour shape. It becomes a prog when the animation finishes — never a skull.
    /// </summary>
    /// <remarks>ROM `BMUT`: the human comes off the human list while the brain drives it.</remarks>
    public bool IsBeingReprogrammed { get; private set; }

    /// <summary>Starts being reprogrammed: the human stops walking and starts flashing.</summary>
    /// <remarks>ROM `BMUT` entry.</remarks>
    internal void BeginReprogramming() => IsBeingReprogrammed = true;

    /// <summary>Finishes reprogramming: the human is gone, and the field puts a prog in its place.</summary>
    /// <remarks>ROM `BMUT` completion — `PROGST` makes the prog where the human was.</remarks>
    internal void FinishReprogramming()
    {
        IsBeingReprogrammed = false;
        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Test-only positioning hook (InternalsVisibleTo the test assembly).</summary>
    /// <param name="position">The position to move the human to.</param>
    internal void TeleportTo(IntVector2 position) => _position = position;
}
