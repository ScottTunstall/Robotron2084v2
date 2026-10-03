using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A human is one of the family members the player is trying to rescue: Mikey, Mommy or Daddy. It wanders about until it is saved, killed or captured.</summary>
/// <seealso cref="Brain"/>
/// <seealso cref="SkullMarker"/>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRH11.ASM</c>, routine <c>HUMAN</c> (with the <c>HUMATB</c> walk table)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$02B2</c> (<c>INITIALISE_FAMILY_MEMBERS</c>)</item>
/// </list>
/// </remarks>
public sealed class Human : IEntity, IAnimationFrameSource, IRemovable
{
    /// <summary>Animation frames in each of a family member's walk sets.</summary>
    private const int AnimationFramesPerSet = 3;

    /// <summary>Direction blocks in the walk table: the 8 travel directions.</summary>
    private const int DirectionBlockCount = 8;

    /// <summary>A fresh direction comes after a random 1..this many steps.</summary>
    private const int NewDirectionStepsMax = 128;

    /// <summary>The very first step waits a random 1..this many ticks, which staggers a group's start.</summary>
    private const int StartStaggerTicksMax = 8;

    /// <summary>The interval between beats, in ROM frames. The ONE deliberate gameplay override — do not "fix" it.</summary>
    /// <remarks>The arcade steps every 8 frames and moves one arcade pixel; the port deliberately
    /// slows this to 16, because the ROM-accurate pace reads as too fast (notes §70).</remarks>
    private const int BeatIntervalRomFrames = 16;

    /// <summary>Substeps in each direction block of the walk table.</summary>
    private const int SubStepsPerBlock = 4;

    /// <summary>Which 3-frame set each block animates from: [L,R,D,U,L,R,R,L].</summary>
    private static readonly int[] AnimationFrameGroupByDirectionBlock = { 0, 1, 2, 3, 0, 1, 1, 0 };

    /// <summary>The walk table: 4 substeps for each of the 8 travel directions, in arcade pixels.</summary>
    /// <remarks>Copied from the ROM's <c>HUMATB</c> — each substep is an animation frame number with an X/Y
    /// delta. Block order: LEFT, RIGHT, DOWN, UP, UP+LEFT, RIGHT+UP, RIGHT+DOWN, DOWN+LEFT.</remarks>
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

    private readonly HumanKind _kind;

    private readonly Random _random;

    private readonly SpriteSet _sprites;

    private int _animationFrameIndex;

    private int _directionBlock;

    private IntVector2 _position;

    private int _startStaggerTicks;

    private int _stepsUntilNewDirection;

    private int _beatTimer;

    // Which of the 8 direction blocks (see Steps) the human is currently walking.
    private int _subStep;

    /// <summary>Creates one family member with its own stagger and starting direction.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the human.</param>
    /// <param name="kind">Which member — it decides the animation frames and the collision box.</param>
    /// <param name="random">The random source for the direction, the step count and the stagger.</param>
    public Human(SpriteSet sprites, IntVector2 position, HumanKind kind, Random random)
    {
        _sprites = sprites;
        _position = position;
        _kind = kind;
        _random = random;
        _directionBlock = random.Next(DirectionBlockCount);
        _stepsUntilNewDirection = 1 + random.Next(NewDirectionStepsMax);
        _startStaggerTicks = 1 + random.Next(StartStaggerTicksMax);
        _beatTimer = 0;                             // The stagger's last tick doubles as the first step.
    }

    /// <summary>This member's own sprite box at <see cref="Position"/>.</summary>
    public Rectangle Bounds
    {
        get
        {
            (int w, int h) = _kind.GetArcadeCollisionSize();
            return new(_position.X, _position.Y, ScreenSize.ToPortPixels(w), ScreenSize.ToPortPixels(h));
        }
    }

    /// <summary>The walk frame this human is showing — the sprite pixel-perfect collision compares.</summary>
    public Texture2D GetCurrentAnimationFrame() => _kind.GetAnimationFrames(_sprites)[_animationFrameIndex];

    /// <summary>True while this human is being reprogrammed: it cannot walk, be rescued or be killed.</summary>
    /// <remarks>ROM: <c>BMUT</c> — the human comes off the human list while the brain drives it.</remarks>
    public bool IsBeingReprogrammed { get; private set; }

    // Which of the 4 substeps within that block comes next (0-3).
    // Counts up to the next step.
    // Steps left before the human rolls a fresh direction.
    // Ticks left before this human's very first step (staggers group spawns).
    // Current animation frame, 0-11 into this family member's 12 animation frames.
    /// <summary>Which member this is (Mikey, Mommy or Daddy) — it decides the animation frames and the box.</summary>
    public HumanKind Kind => _kind;

    /// <summary>Alive until killed, rescued or reprogrammed.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Top-left of the human.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>
    /// This member's slot in the family list (the ROM's <c>$B354</c>), handed out in spawn order by
    /// <see cref="PlayField"/> — so the first Mikey holds slot 0.
    /// </summary>
    /// <remarks>A brain's target is a SLOT rather than a person, which is why every brain on a wave can
    /// chase the same member (notes §18.8).</remarks>
    internal int FamilySlot { get; set; }

    /// <summary>Steps taken so far — test hook for the step cadence.</summary>
    internal int StepCount { get; private set; }

    /// <summary>Draws the walk frame, or — while being reprogrammed — the flashing two-colour shape.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        Texture2D[] frames = _kind.GetAnimationFrames(_sprites);

        if (IsBeingReprogrammed)
        {
            // Reprogrammed: a solid silhouette over a solid background, both cycling slots.
            _sprites.Blitter.DrawSpriteSolidWithBackground(
                spriteBatch,
                frames[_animationFrameIndex],
                Bounds,
                _sprites.Blitter.GetSlotColour(ReprogramTuning.BackgroundSlot),
                _sprites.Blitter.GetSlotColour(ReprogramTuning.ShapeSlot));
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, frames[_animationFrameIndex], Bounds, Color.White);
    }

    /// <summary>Killed: gone at once, with no death animation.</summary>
    /// <remarks>ROM: <c>DMAOFF</c>.</remarks>
    public void Kill()
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Rescued: gone at once.</summary>
    public void Rescue()
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Walks the current direction block one substep at a time, on the step clock.</summary>
    /// <param name="gameTime">Unused — the beat interval is counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        // No "robots frozen" gate: humans wander while the field is still assembling.
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

            // The last stagger tick doubles as the first step, so later steps stay on schedule.
        }
        else
        {
            _beatTimer += ArcadeClock.UnitsPerPortTick;
            if (_beatTimer < ArcadeClock.ToClockUnits(BeatIntervalRomFrames))
            {
                return;
            }

            _beatTimer -= ArcadeClock.ToClockUnits(BeatIntervalRomFrames);
        }

        StepCount++;

        (int dx, int dy, int frame) = Steps[_directionBlock * SubStepsPerBlock + _subStep];
        _animationFrameIndex = AnimationFramesPerSet * AnimationFrameGroupByDirectionBlock[_directionBlock] + frame;

        // Each unit in the walk table is one arcade pixel.
        IntVector2 candidate = _position + new IntVector2(dx, dy) * ScreenSize.ToPortPixels(1);
        Rectangle next = Bounds with { X = candidate.X, Y = candidate.Y };
        if (field.Wall.Intersects(next) || OverlapsLivingElectrode(next, field))
        {
            // Blocked by the wall or a standing electrode: pick a fresh direction instead.
            PickNewDirection();
            return;
        }

        _position = candidate;
        _subStep = (_subStep + 1) % SubStepsPerBlock;
        if (--_stepsUntilNewDirection <= 0)
        {
            PickNewDirection();
        }
    }

    /// <summary>The largest side of this member's box in port pixels — the square the spawner keeps clear.</summary>
    /// <param name="kind">The member whose box is measured.</param>
    /// <returns>The square's side, in port pixels.</returns>
    internal static int SpawnSquarePortPixels(HumanKind kind)
    {
        (int width, int height) = kind.GetArcadeCollisionSize();
        return ScreenSize.ToPortPixels(Math.Max(width, height));
    }

    /// <summary>Says whether this human is standing on the field and free: alive, and not in a brain's hold.</summary>
    /// <remarks>Original source: a human that is dead or being reprogrammed is off the family list (<c>RRH11.ASM</c> <c>HTAB</c>), so nothing can target, catch, kill or rescue it.</remarks>
    internal bool IsGraspable() => LifeState == EntityLifeState.Alive && !IsBeingReprogrammed;

    /// <summary>Starts being reprogrammed: the human stops walking and starts flashing.</summary>
    internal void BeginReprogramming() => IsBeingReprogrammed = true;

    /// <summary>Finishes reprogramming: the human is gone.</summary>
    /// <remarks>ROM: <c>PROGST</c>.</remarks>
    internal void FinishReprogramming()
    {
        IsBeingReprogrammed = false;
        LifeState = EntityLifeState.Dead;
    }

    /// <summary>The brain's hold on its victim: moves the human directly, without walking it.</summary>
    /// <param name="position">Where the brain puts the human.</param>
    /// <remarks>ROM: <c>BMUT</c>, the reprogramming lift and drop. A test can also use it to place one.</remarks>
    internal void MoveTo(IntVector2 position) => _position = position;

    /// <summary>True when the human's next step would overlap an electrode that is still standing.</summary>
    private static bool OverlapsLivingElectrode(Rectangle next, PlayField field)
    {
        foreach (Electrode electrode in field.Electrodes)
        {
            if (electrode.LifeState == EntityLifeState.Alive && electrode.Bounds.Intersects(next))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Picks a fresh direction and re-rolls how many steps to take before the next change.</summary>
    private void PickNewDirection()
    {
        _directionBlock = _random.Next(DirectionBlockCount);
        _subStep = 0;
        _animationFrameIndex = AnimationFramesPerSet * AnimationFrameGroupByDirectionBlock[_directionBlock];
        _stepsUntilNewDirection = 1 + _random.Next(NewDirectionStepsMax);
    }
}
