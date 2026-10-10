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
/// It acts on a beat. The <see cref="PlayField"/> calls <see cref="Update"/> on nearly every tick, through
/// <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. <see cref="_beatTimer"/> gathers the ticks
/// until it is time for the next beat (see <see cref="ArcadeClock"/>). <see cref="_startStaggerTicks"/> counts the
/// ticks before its first step.
///
/// <list type="bullet">
/// <item>Original source: <c>RRH11.ASM</c>, routine <c>HUMAN</c> (with the <c>HUMATB</c> walk table)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$02B2</c> (<c>INITIALISE_FAMILY_MEMBERS</c>)</item>
/// </list>
/// </remarks>
public sealed class Human : IEntity, IAnimationFrameSource, IRemovable
{
    /// <summary>How many animation frames are in each of a family member's walk sets. It is multiplied by the set a direction uses to find the first animation frame, which is stored in <see cref="_animationFrameIndex"/>.</summary>
    private const int AnimationFramesPerSet = 3;

    /// <summary>How many direction blocks the walk table has, one for each way a human can walk. <see cref="_directionBlock"/> is set to a random one of them.</summary>
    private const int DirectionBlockCount = 8;

    /// <summary>The most steps a human takes before choosing a new direction. <see cref="_stepsUntilNewDirection"/> is set to a random number of steps from one up to this.</summary>
    private const int NewDirectionStepsMax = 128;

    /// <summary>The most ticks a human waits before its first step. <see cref="_startStaggerTicks"/> is set to a random number of ticks from one up to this, so a group of humans does not all start together.</summary>
    private const int StartStaggerTicksMax = 8;

    /// <summary>The interval between beats, in ROM frames. The ONE deliberate gameplay override — do not "fix" it.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HUMAN</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>INITIALISE_FAMILY_MEMBERS</c> (<c>$02B2</c>).</item>
    /// </list>
    ///
    /// The arcade steps every 8 frames and moves one
    /// arcade pixel; the port deliberately slows this to 16, because the ROM-accurate pace reads as too
    /// fast (notes §70).
    /// </remarks>
    private const int BeatIntervalRomFrames = 16;

    /// <summary>How many substeps each direction block of the walk table has. <see cref="_subStep"/> counts up to this and then goes back to the first.</summary>
    private const int SubStepsPerBlock = 4;

    /// <summary>Which 3-frame set each block animates from: [L,R,D,U,L,R,R,L].</summary>
    private static readonly int[] AnimationFrameGroupByDirectionBlock = { 0, 1, 2, 3, 0, 1, 1, 0 };

    /// <summary>The walk table: 4 substeps for each of the 8 travel directions, in arcade pixels.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HUMATB</c> — each substep is an animation frame number
    /// with an X/Y delta.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>INITIALISE_FAMILY_MEMBERS</c> (<c>$02B2</c>).</item>
    /// </list>
    ///
    /// Block order: LEFT, RIGHT, DOWN, UP, UP+LEFT, RIGHT+UP, RIGHT+DOWN, DOWN+LEFT.
    /// </remarks>
    private static readonly (int Dx, int Dy, int Frame)[] Steps =
    {
        // Four steps moving left, through walk frames 0, 1, 0 and 2 of the set.
        (-2, 0, 0), (-1, 0, 1), (-2, 0, 0), (-1, 0, 2),
        // Four steps moving right, through walk frames 0, 1, 0 and 2 of the set.
        (2, 0, 0), (1, 0, 1), (2, 0, 0), (1, 0, 2),
        // Four steps moving down, through walk frames 0, 1, 0 and 2 of the set.
        (0, 1, 0), (0, 1, 1), (0, 1, 0), (0, 1, 2),
        // Four steps moving up, through walk frames 0, 1, 0 and 2 of the set.
        (0, -1, 0), (0, -1, 1), (0, -1, 0), (0, -1, 2),
        // Four steps moving diagonally up and to the left.
        (-2, -1, 0), (-1, -1, 2), (-2, -1, 0), (-1, -1, 2),
        // Four steps moving diagonally up and to the right.
        (2, -1, 0), (1, -1, 1), (2, -1, 0), (1, -1, 2),
        // Four steps moving diagonally down and to the right.
        (2, 1, 0), (1, 1, 1), (2, 1, 0), (1, 1, 2),
        // Four steps moving diagonally down and to the left.
        (-2, 1, 0), (-1, 1, 1), (-2, 1, 0), (-1, 1, 1),
    };

    private readonly HumanKind _kind;

    private readonly Random _random;

    private readonly SpriteSet _sprites;

    /// <summary>Which walk animation frame is showing, counted through this family member's own animation frames.</summary>
    private int _animationFrameIndex;

    /// <summary>Counts up to the next beat.</summary>
    private int _beatTimer;

    /// <summary>Which direction block of the walk table (see <see cref="Steps"/>) the human is walking.</summary>
    private int _directionBlock;

    private IntVector2 _position;

    /// <summary>Ticks left before this human takes its first step, so a group does not all step together.</summary>
    private int _startStaggerTicks;

    /// <summary>Steps left before the human picks a new direction.</summary>
    private int _stepsUntilNewDirection;

    /// <summary>Which substep of the direction block comes next.</summary>
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
        _beatTimer = 0;                             // The beat timer starts at zero. The last start-up tick counts as the first step (see Update).
    }

    /// <summary>This member's own sprite box at <see cref="Position"/>.</summary>
    public Rectangle GetBounds()
    {
        (int w, int h) = _kind.GetArcadeCollisionSize();
        return new(_position.X, _position.Y, ScreenSize.ToPortPixels(w), ScreenSize.ToPortPixels(h));
    }

    /// <summary>The walk frame this human is showing — the sprite pixel-perfect collision compares.</summary>
    public Texture2D GetCurrentAnimationFrame() => _kind.GetAnimationFrames(_sprites)[_animationFrameIndex];

    /// <summary>True while this human is being reprogrammed: it cannot walk, be rescued or be killed.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>BMUT</c> — the human comes off the human list while the
    /// brain drives it.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c>.</item>
    /// </list>
    /// </remarks>
    public bool IsBeingReprogrammed { get; private set; }

    /// <summary>Which member this is (Mikey, Mommy or Daddy) — it decides the animation frames and the box.</summary>
    public HumanKind Kind => _kind;

    /// <summary>Alive until killed, rescued or reprogrammed.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Top-left of the human.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HUMAN</c> — the OBJX/OBJY registers.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>INITIALISE_FAMILY_MEMBERS</c> (<c>$02B2</c>).</item>
    /// </list>
    /// </remarks>
    public IntVector2 Position => _position;

    /// <summary>
    /// This member's slot in the family list, handed out in spawn order by
    /// <see cref="PlayField"/> — so the first Mikey holds slot 0.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HUMAN</c> (ROM <c>$B354</c>).</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>INITIALISE_FAMILY_MEMBERS</c> (<c>$02B2</c>).</item>
    /// </list>
    ///
    /// A brain's target is a SLOT
    /// rather than a person, which is why every brain on a wave can chase the same member (notes §18.8).
    /// </remarks>
    internal int FamilySlot { get; set; }

    /// <summary>Steps taken so far — test hook for the step cadence.</summary>
    internal int StepCount { get; private set; }

    /// <summary>Draws the walk frame, or — while being reprogrammed — the flashing two-colour shape.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        Texture2D[] frames = _kind.GetAnimationFrames(_sprites);

        if (IsBeingReprogrammed)
        {
            // While it is being reprogrammed, the human is drawn as a solid silhouette on a solid background, both in colours that cycle.
            _sprites.Blitter.DrawSpriteSolidWithBackground(
                spriteBatch,
                frames[_animationFrameIndex],
                GetBounds(),
                _sprites.Blitter.GetSlotColour(ReprogramTuning.BackgroundSlot),
                _sprites.Blitter.GetSlotColour(ReprogramTuning.ShapeSlot));
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, frames[_animationFrameIndex], GetBounds(), Color.White);
    }

    /// <summary>Killed: gone at once, with no death animation.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>DMAOFF</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>INITIALISE_FAMILY_MEMBERS</c> (<c>$02B2</c>).</item>
    /// </list>
    /// </remarks>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Rescued: gone at once.</summary>
    public void Rescue()
    {
        if (!this.IsAlive())
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
        // Unlike robots, humans do not wait for the field to go live. They wander while it is still being put together.
        if (!this.IsAlive() || IsBeingReprogrammed)
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

            // The last start-up tick (see ArcadeClock) also counts as the first step, so the steps that follow keep to the same schedule.
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

        // Each step in the table moves the human by one arcade pixel, scaled up to port pixels.
        IntVector2 candidate = _position + new IntVector2(dx, dy) * ScreenSize.ToPortPixels(1);
        Rectangle next = GetBounds() with { X = candidate.X, Y = candidate.Y };
        if (field.HitsWall(next) || OverlapsLivingElectrode(next, field))
        {
            // A step into a wall or a live Electrode is refused, and the human picks a new direction instead. (see Electrode)
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
    internal bool IsGraspable() => this.IsAlive() && !IsBeingReprogrammed;

    /// <summary>Starts being reprogrammed: the human stops walking and starts flashing.</summary>
    internal void BeginReprogramming() => IsBeingReprogrammed = true;

    /// <summary>Finishes reprogramming: the human is gone.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>PROGST</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c>.</item>
    /// </list>
    /// </remarks>
    internal void FinishReprogramming()
    {
        IsBeingReprogrammed = false;
        LifeState = EntityLifeState.Dead;
    }

    /// <summary>The brain's hold on its victim: moves the human directly, without walking it.</summary>
    /// <param name="position">Where the brain puts the human.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>BMUT</c> — the reprogramming lift and drop.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c>.</item>
    /// </list>
    ///
    /// A test can also use it to place one.
    /// </remarks>
    internal void MoveTo(IntVector2 position) => _position = position;

    /// <summary>True when the human's next step would overlap an electrode that is still standing.</summary>
    private static bool OverlapsLivingElectrode(Rectangle next, PlayField field)
    {
        foreach (Electrode electrode in field.GetElectrodes())
        {
            if (electrode.IsAlive() && electrode.GetBounds().Intersects(next))
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
