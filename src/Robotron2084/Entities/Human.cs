using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
///     A family member the player is trying to rescue: Mikey, Mommy or Daddy. It wanders about until it is rescued,
///     killed or caught by a brain.
/// </summary>
/// <seealso cref="Brain" />
/// <seealso cref="SkullMarker" />
/// <remarks>
///     It acts on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. <see cref="_beatTimer" /> says when it is time for the next <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <see cref="_firstStepTicksRemaining" /> counts the <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> before its first <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <list type="bullet">
///         <item>Original source: <c>RRH11.ASM</c>, routine <c>HUMAN</c> (with the <c>HUMATB</c> walk table)</item>
///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>$02B2</c> (<c>INITIALISE_FAMILY_MEMBERS</c>)</item>
///     </list>
/// </remarks>
public sealed class Human : IEntity, IAnimationFrameSource, IRemovable
{
    /// <summary>How many animation frames are in each of a family member's walk sets.</summary>
    private const int AnimationFramesPerSet = 3;

    /// <summary>How many direction blocks the walk table has, one for each way a family member can walk.</summary>
    private const int PossibleDirections = 8;

    /// <summary>The most steps a family member takes before it picks a new direction.</summary>
    private const int NewDirectionMaxSteps = 128;

    /// <summary>
    ///     The most <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> a family member waits before
    ///     its first step, so that a group of family members does not all start together.
    /// </summary>
    private const int FirstStepMaxTicks = 8;

    /// <summary>
    ///     How long a family member waits between steps. This number is different from the arcade's on purpose. Do not
    ///     change it back.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>HUMAN</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>INITIALISE_FAMILY_MEMBERS</c> (<c>$02B2</c>).</item>
    ///     </list>
    /// </remarks>
    private const int BeatIntervalArcadeFrames = 16;

    /// <summary>How many steps each direction block of the walk table has.</summary>
    private const int SubStepsPerDirection = 4;

    /// <summary>
    ///     Which set of three animation frames each direction block uses. Walking diagonally uses the left set or the
    ///     right set.
    /// </summary>
    private static readonly int[] AnimationFrameGroupByDirection = [0, 1, 2, 3, 0, 1, 1, 0];

    /// <summary>
    ///     The walk table. It has 4 steps for each of the 8 ways a family member can walk. Each step says how far to go
    ///     sideways and how far up or down, in arcade pixels, and which animation frame to show.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRH11.ASM</c> <c>HUMATB</c>. Each step is an animation frame number, with how far
    ///             to go sideways and how far up or down.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>INITIALISE_FAMILY_MEMBERS</c> (<c>$02B2</c>).</item>
    ///     </list>
    ///  </remarks>
    private static readonly (int Dx, int Dy, int Frame)[] Steps =
    {
        // Walking left.
        (-2, 0, 0), (-1, 0, 1), (-2, 0, 0), (-1, 0, 2),
        // Walking right.
        (2, 0, 0), (1, 0, 1), (2, 0, 0), (1, 0, 2),
        // Walking down.
        (0, 1, 0), (0, 1, 1), (0, 1, 0), (0, 1, 2),
        // Walking up.
        (0, -1, 0), (0, -1, 1), (0, -1, 0), (0, -1, 2),
        // Walking up and to the left.
        (-2, -1, 0), (-1, -1, 2), (-2, -1, 0), (-1, -1, 2),
        // Walking up and to the right.
        (2, -1, 0), (1, -1, 1), (2, -1, 0), (1, -1, 2),
        // Walking down and to the right.
        (2, 1, 0), (1, 1, 1), (2, 1, 0), (1, 1, 2),
        // Walking down and to the left.
        (-2, 1, 0), (-1, 1, 1), (-2, 1, 0), (-1, 1, 1)
    };

    private readonly Random _random;

    private readonly SpriteSet _sprites;

    /// <summary>Which walk animation frame is showing, counted through this family member's own animation frames.</summary>
    private int _animationFrameIndex;

    /// <summary>Times the family member's <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see>.</summary>
    private readonly ArcadeFrameTimer _beatTimer = new();

    /// <summary>Which direction block of the walk table (see <see cref="Steps" />) the family member is using.</summary>
    private int _directionBlock;

    private IntVector2 _position;

    /// <summary>
    ///     <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">Ticks</see> left before the family member takes its first step. Each family member gets its own number, so a group
    ///     does not all step together.
    /// </summary>
    private int _firstStepTicksRemaining;

    /// <summary>Steps left before the family member picks a new direction.</summary>
    private int _newDirectionStepsRemaining;

    /// <summary>Which step of the direction block comes next.</summary>
    private int _subStep;

    /// <summary>
    ///     Makes one family member. It picks at random which way it walks first, and how long it waits before its first
    ///     step.
    /// </summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where the family member's top-left corner is.</param>
    /// <param name="kind">Which family member it is. This decides its animation frames and its size.</param>
    /// <param name="random">
    ///     Where its random numbers come from. They pick its direction, how many steps it takes before it
    ///     turns, and its wait before the first step.
    /// </param>
    public Human(SpriteSet sprites, IntVector2 position, HumanKind kind, Random random)
    {
        _sprites = sprites;
        _position = position;
        Kind = kind;
        _random = random;
        _directionBlock = random.Next(PossibleDirections);
        _newDirectionStepsRemaining = 1 + random.Next(NewDirectionMaxSteps);
        _firstStepTicksRemaining = 1 + random.Next(FirstStepMaxTicks);
    }

    /// <summary>
    ///     True while a brain is reprogramming this family member. Until that is over, it cannot walk, be rescued or be
    ///     killed.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>BMUT</c>. The family member is taken off the family list while the
    ///             brain has hold of it.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c>.</item>
    ///     </list>
    /// </remarks>
    public bool IsBeingReprogrammed { get; private set; }

    /// <summary>Which family member this is: Mikey, Mommy or Daddy. This decides its animation frames and its size.</summary>
    public HumanKind Kind { get; }

    /// <summary>This family member's place in the family list.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>HUMAN</c> (ROM <c>$B354</c>).</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>INITIALISE_FAMILY_MEMBERS</c> (<c>$02B2</c>).</item>
    ///     </list>
    ///     A brain chases a place in the list, not a person. That is why every brain on a wave can chase the same
    ///     family member (notes §18.8).
    /// </remarks>
    internal int FamilySlot { get; set; }

    /// <summary>How many steps the family member has taken.</summary>
    internal int StepCount { get; private set; }

    /// <summary>The walk animation frame the family member is showing.</summary>
    public Texture2D GetCurrentAnimationFrame()
    {
        return Kind.GetAnimationFrames(_sprites)[_animationFrameIndex];
    }

    /// <summary>The box the family member takes up on the screen.</summary>
    public Rectangle GetBounds()
    {
        var (w, h) = Kind.GetArcadeCollisionSize();
        return new Rectangle(_position.X, _position.Y, ScreenSize.ToPortPixelsFromArcadePixels(w),
            ScreenSize.ToPortPixelsFromArcadePixels(h));
    }

    /// <summary>Alive until it is killed, rescued or reprogrammed.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the family member's top-left corner is.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>HUMAN</c>, the OBJX/OBJY registers.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>INITIALISE_FAMILY_MEMBERS</c> (<c>$02B2</c>).</item>
    ///     </list>
    /// </remarks>
    public IntVector2 Position => _position;

    /// <summary>
    ///     Draws the walk animation frame. While the family member is being reprogrammed, it is drawn as a flashing
    ///     two-colour shape.
    /// </summary>
    /// <param name="spriteBatch">What the family member is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        var frames = Kind.GetAnimationFrames(_sprites);

        if (IsBeingReprogrammed)
        {
            // A family member being reprogrammed is drawn as a one-colour shape on a one-colour box. Both colours keep changing.
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

    /// <summary>
    ///     Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. On a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> the family member takes the next step of its walk. If the step is blocked, it picks a
    ///     new direction.
    /// </summary>
    /// <param name="gameTime">Not used. The family member counts <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        // Unlike the robots, family members do not wait for the wave to start. They walk about while the robots are still appearing.
        if (!this.IsAlive() || IsBeingReprogrammed) return;

        if (_firstStepTicksRemaining > 0)
        {
            // When this wait is over, the family member has its first beat on this tick.
            if (--_firstStepTicksRemaining > 0) return;
        }
        else if (!_beatTimer.Tick(BeatIntervalArcadeFrames))
        {
            return;
        }

        ActOnBeat(field);
    }

    /// <summary>Kills the family member. It is gone at once, with no death animation.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>DMAOFF</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>INITIALISE_FAMILY_MEMBERS</c> (<c>$02B2</c>).</item>
    ///     </list>
    /// </remarks>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Rescues the family member.</summary>
    public void Rescue()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>
    ///     The longer side of the family member's box, in port pixels. When a family member is put on the field, a square
    ///     this big is kept clear for it.
    /// </summary>
    /// <param name="kind">Which family member.</param>
    /// <returns>The length of the square's side, in port pixels.</returns>
    internal static int GetSpawnSquarePortPixels(HumanKind kind)
    {
        var (width, height) = kind.GetArcadeCollisionSize();
        return ScreenSize.ToPortPixelsFromArcadePixels(Math.Max(width, height));
    }

    /// <summary>Says whether this family member is on the field and free: alive, and not being held by a brain.</summary>
    /// <remarks>
    ///     Original source: a family member that is dead or being reprogrammed is off the family list (<c>RRH11.ASM</c>
    ///     <c>HTAB</c>), so nothing can chase, catch, kill or rescue it.
    /// </remarks>
    internal bool IsGraspable()
    {
        return this.IsAlive() && !IsBeingReprogrammed;
    }

    /// <summary>Starts the reprogramming. The family member stops walking and starts flashing.</summary>
    internal void BeginReprogramming()
    {
        IsBeingReprogrammed = true;
    }

    /// <summary>Finishes the reprogramming. The family member is gone.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>PROGST</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c>.</item>
    ///     </list>
    /// </remarks>
    internal void FinishReprogramming()
    {
        IsBeingReprogrammed = false;
        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Puts the family member somewhere without walking it there.</summary>
    /// <param name="position">Where to put the family member.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BMUT</c>, which lifts the family member up and drops it.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c>.</item>
    ///     </list>
    /// </remarks>
    internal void MoveTo(IntVector2 position)
    {
        _position = position;
    }

    /// <summary>
    ///     What the family member does on its <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. It takes the next step of its walk. If the step is blocked, it picks a
    ///     new direction.
    /// </summary>
    /// <param name="field">The playfield.</param>
    private void ActOnBeat(PlayField field)
    {
        StepCount++;

        var (dx, dy, frame) = Steps[(_directionBlock * SubStepsPerDirection) + _subStep];
        _animationFrameIndex = AnimationFramesPerSet * AnimationFrameGroupByDirection[_directionBlock] + frame;

        // The walk table is in arcade pixels, so the step is changed to port pixels.
        var possibleNextPosition = _position + new IntVector2(dx, dy) * ScreenSize.ToPortPixelsFromArcadePixels(1);
        var nextPosition = GetBounds() with { X = possibleNextPosition.X, Y = possibleNextPosition.Y };
        if (field.HitsWall(nextPosition) || IsOverlappingLivingElectrode(nextPosition, field))
        {
            // The step would go into the wall or a live electrode, so the family member stays where it is and picks a new direction.
            PickNewDirection();
            return;
        }

        _position = possibleNextPosition;
        _subStep = (_subStep + 1) % SubStepsPerDirection;
        if (--_newDirectionStepsRemaining <= 0) PickNewDirection();
    }

    /// <summary>True when the family member's next step would touch an electrode that is still standing.</summary>
    /// <param name="next">The box the family member would take up after the step.</param>
    /// <param name="field">The playfield, which holds the electrodes.</param>
    private static bool IsOverlappingLivingElectrode(Rectangle next, PlayField field)
    {
        foreach (var electrode in field.GetElectrodes())
            if (electrode.IsAlive() && electrode.GetBounds().Intersects(next))
                return true;

        return false;
    }

    /// <summary>Picks a new direction at random, and picks at random how many steps to take before the next change.</summary>
    private void PickNewDirection()
    {
        _directionBlock = _random.Next(PossibleDirections);
        _subStep = 0;
        _animationFrameIndex = AnimationFramesPerSet * AnimationFrameGroupByDirection[_directionBlock];
        _newDirectionStepsRemaining = 1 + _random.Next(NewDirectionMaxSteps);
    }
}
