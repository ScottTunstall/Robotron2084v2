using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
///     A brain is a robot that chases a family member and turns them into a <see cref="Prog" />>. It also shoots
///     cruise missiles at you.
/// </summary>
/// <seealso cref="PlayField" />
/// <seealso cref="FamilyMember" />
/// <remarks>
///     It acts on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <see cref="_beatArcadeFrameTimer" /> says when it is time for the next <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <see cref="_reprogramArcadeFrameTimer" /> times the steps of reprogramming a family member.
///     <list type="bullet">
///         <item>
///             Original source: <c>RRB10.ASM</c>: <c>BRNSTV</c> (start the brains),
///             <c>BRAIN</c>/<c>BRNL</c>/<c>BRNL1</c> (each <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>), <c>BMUT</c> (reprogram a family member), <c>BRNSHT</c>
///             (fire a cruise missile), <c>BRNKIL</c> (shot by a laser)
///         </item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c>: <c>INITIALISE_ALL_BRAINS</c> (<c>$1AF4</c>), <c>BRAIN_AI</c>
///             (<c>$1BEE</c>), <c>ANIMATE_BRAIN</c> (<c>$1C0F</c>), <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c>
///             (<c>$1CC2</c>), <c>CREATE_CRUISE_MISSILE</c> (<c>$2006</c>), <c>BRAIN_COLLISION_HANDLER</c>
///             (<c>$1DD6</c>)
///         </item>
///     </list>
///     Sideways distances here are in columns
///     (two arcade pixels each) and up-and-down distances in rows (one arcade pixel each), as in the ROM; see
///     docs/glossary.md.
/// </remarks>
public sealed class Brain : IExplodable, IRemovable, IWaveStartRobot
{
    /// <summary>How long one <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> takes to run. The time from one <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> to the next.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>BRNSLP</c>, the sleep of <c>BRNSPD</c> frames at the end
    ///             of each <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
    ///         </item>
    ///         <item>
    ///             Disassembly: <c>BRAIN_AI</c> (<c>$1BEE</c>), the wait loaded from <c>$BE63</c> at
    ///             <c>$1C9C</c>.
    ///         </item>
    ///     </list>
    /// </remarks>
    private const int BeatExecutionArcadeFrames = 1;

    /// <summary>How long the arcade's routine sleeps after the look that finds the game live, before the first <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BRN0A</c>, <c>NAP 12,BRNL</c>.</item>
    ///         <item>Disassembly: <c>$1BD8</c> onwards.</item>
    ///     </list>
    /// </remarks>
    private const int FirstBeatNapArcadeFrames = 12;

    /// <summary>How long the arcade's routine sleeps between one look at whether the game is live and the next.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BRAIN</c>, <c>BITA #$7F / BEQ BRN0A / NAP 4,BRAIN</c>.</item>
    ///         <item>Disassembly: <c>$1BD8</c>.</item>
    ///     </list>
    /// </remarks>
    private const int LivePollArcadeFrames = 4;

    /// <summary>How far to the right of the brain's top-left corner a new cruise missile appears, in columns.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BRNSHT</c>, <c>ADDD #$0304</c>, the 3 columns.</item>
    ///         <item>Disassembly: <c>CREATE_CRUISE_MISSILE</c> (<c>$2006</c>).</item>
    ///     </list>
    /// </remarks>
    private const int MissileMuzzleXColumns = 3;

    /// <summary>How far below the brain's top-left corner a new cruise missile appears, in rows.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BRNSHT</c>, <c>ADDD #$0304</c>, the 4 rows.</item>
    ///         <item>Disassembly: <c>CREATE_CRUISE_MISSILE</c> (<c>$2006</c>).</item>
    ///     </list>
    /// </remarks>
    private const int MissileMuzzleYRows = 4;

    /// <summary>How many rows below the brain's top-left corner the victim stands while being reprogrammed.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BMUT1</c>, <c>ADDA #2</c> on the up-and-down position, 2 rows.</item>
    ///         <item>Disassembly: <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c> (<c>$1CC2</c>) at <c>$1CEB</c>.</item>
    ///     </list>
    /// </remarks>
    private const int VictimDropRows = 2;

    /// <summary>The gap between the victim and the brain's left side when the victim stands on the brain's left, in columns.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BMUT00</c>, <c>SUBA #1</c> on the sideways position, 1 column.</item>
    ///         <item>Disassembly: <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c> (<c>$1CC2</c>) at <c>$1CCD</c>.</item>
    ///     </list>
    /// </remarks>
    private const int VictimGapColumns = 1;

    /// <summary>
    ///     How far to the right of the brain's top-left corner the victim stands when there is no room on the left, in
    ///     columns.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>BMUT10</c>, <c>ADDA #8</c> on the sideways position, 8 columns (16
    ///             arcade
    ///             pixels).
    ///         </item>
    ///         <item>Disassembly: <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c> (<c>$1CC2</c>) at <c>$1CDC</c>.</item>
    ///     </list>
    /// </remarks>
    private const int VictimRightOffsetColumns = 8;

    /// <summary>How close to the right wall the victim may stand, in columns.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BMUT10</c>, <c>CMPA #XMAX-4</c> on the sideways position, 4 columns.</item>
    ///         <item>Disassembly: <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c> (<c>$1CC2</c>) at <c>$1CDE</c>.</item>
    ///     </list>
    /// </remarks>
    private const int VictimRightWallMarginColumns = 4;

    /// <summary>
    ///     How close sideways the brain gets to its target before it stops moving sideways, in port pixels. Up and down
    ///     has no such gap.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>BRNL1</c>, <c>ADDA #2 / CMPA #4 / BLS</c> on the sideways position, 2
    ///             columns.
    ///         </item>
    ///         <item>Disassembly: <c>ANIMATE_BRAIN</c> (<c>$1C0F</c>) at <c>$1C11</c>.</item>
    ///     </list>
    /// </remarks>
    private static readonly int ApproachDeadZonePixels = ScreenSize.ToPortPixelsFromColumns(2);

    /// <summary>How close sideways the brain's and the family member's top-left corners must be for a catch, in port pixels.</summary>
    private static readonly int CatchReachX = ScreenSize.ToPortPixelsFromColumns(ReprogramTuning.CatchReachColumns);

    /// <summary>
    ///     How close up and down the brain's and the family member's top-left corners must be for a catch, in port
    ///     pixels.
    /// </summary>
    private static readonly int CatchReachY = ScreenSize.ToPortPixelsFromArcadePixels(ReprogramTuning.CatchReachRows);

    /// <summary>
    ///     How big the brain is, in port pixels. It is the size of the brain's sprite.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BRDP1</c>, 7 bytes by 16 rows (14 by 16 arcade pixels).</item>
    ///         <item>Disassembly: the brain standing still, <c>$2159</c>.</item>
    ///     </list>
    /// </remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.BrainCollisionSize.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.BrainCollisionSize.Height));

    /// <summary>How far the brain moves sideways on each <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>, in port pixels.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BRNL1</c>, a step of 1 column.</item>
    ///         <item>Disassembly: <c>ANIMATE_BRAIN</c> (<c>$1C0F</c>).</item>
    ///     </list>
    /// </remarks>
    private static readonly int StepXPixels = ScreenSize.ToPortPixelsFromColumns(1);

    /// <summary>How far the brain moves up or down on each <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>, in port pixels.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BRNL1</c>, a step of 1 row.</item>
    ///         <item>Disassembly: <c>ANIMATE_BRAIN</c> (<c>$1C0F</c>).</item>
    ///     </list>
    /// </remarks>
    private static readonly int StepYPixels = ScreenSize.ToPortPixelsFromArcadePixels(1);

    /// <summary>The order the brain's three walking animation frames are shown in: first, second, first, third.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>BRNAL</c>, <c>BRNAR</c>, <c>BRNAD</c> and <c>BRNAU</c>,
    ///             each listing its animation frames as 1, 2, 1, 3.
    ///         </item>
    ///         <item>Disassembly: <c>BRAIN_ANIMATION_TABLES</c> (<c>$1CA2</c>).</item>
    ///     </list>
    /// </remarks>
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    /// <summary>The time from one <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> to the next.</summary>
    private readonly int _beatIntervalArcadeFrames;

    /// <summary>
    ///     The longest wait, in <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see>, between cruise missiles.
    /// </summary>
    private readonly int _fireIntervalBeats;

    private readonly Random _random;
    private readonly TextureSet _textureSet;

    private ArcadeFrameTimer _beatArcadeFrameTimer = new();

    /// <summary><see cref="Robotron2084.Core.ArcadeFrameTimer">Beats</see> left before the brain fires its next cruise missile.</summary>
    private int _fireBeatsRemaining;

    private IntVector2 _position;

    /// <summary>True when the next reprogramming step moves the family member down, false when it moves them back up.</summary>
    private bool _reprogramMovingDown;

    /// <summary>Steps left before the reprogramming is finished.</summary>
    private int _reprogramRedrawsRemaining;

    /// <summary>Says when it is time for the next reprogramming step.</summary>
    private ArcadeFrameTimer _reprogramArcadeFrameTimer = new();

    /// <summary>Which place in the family list the brain is chasing.</summary>
    private int _targetSlot;

    /// <summary>The family member being reprogrammed right now, or null.</summary>
    private FamilyMember? _victimFamilyMember;

    /// <summary>Where the family member stands, up and down, while being reprogrammed.</summary>
    private int _victimRestingY;

    /// <summary>Where the brain is in its four-step walking pattern, from 0 to 3.</summary>
    private int _walkCycleIndex;

    /// <summary>Which walk the brain is showing. A new brain shows the one for walking down.</summary>
    private WalkSequence _walkSequence = WalkSequence.Down;

    /// <summary>Makes a brain. It takes its first step on its first <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.</summary>
    /// <param name="textureSet">The shared texture set.</param>
    /// <param name="position">Where the brain's top-left corner is.</param>
    /// <param name="random">
    ///     Where its random numbers come from. They pick the waits between missiles and the shaking during
    ///     reprogramming.
    /// </param>
    /// <param name="beatWaitArcadeFrames">
    ///     How long this wave's brains wait after each <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. A bigger number makes a slower brain.
    ///     The time from one <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> to the next is this plus <see cref="BeatExecutionArcadeFrames" />.
    /// </param>
    /// <param name="fireIntervalBeats">The longest this wave's brains wait between cruise missiles, in <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see>.</param>
    /// <param name="targetFamilySlot">Which place in the family list the brain chases at the start.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>BRNSTV</c> (the wait is <c>BSHTIM</c> through
    ///             <c>RMAX</c>, the target comes from <c>GETHTG</c>).
    ///         </item>
    ///         <item>
    ///             Disassembly: <c>INITIALISE_ALL_BRAINS</c> (<c>$1AF4</c>) and
    ///             <c>FIND_NEAREST_FAMILY_MEMBER_TO_PROG</c> (<c>$1B95</c>). The ROM picks the nearest place in the family
    ///             list as each brain is made, before the family is on the field, which is why every brain in the arcade
    ///             chases Mikey (notes §18.8).
    ///         </item>
    ///     </list>
    /// </remarks>
    public Brain(
        TextureSet textureSet,
        IntVector2 position,
        Random random,
        int beatWaitArcadeFrames,
        int fireIntervalBeats,
        int targetFamilySlot)
    {
        _textureSet = textureSet;
        _position = position;
        _random = random;
        _fireIntervalBeats = fireIntervalBeats;
        _targetSlot = targetFamilySlot;
        _beatIntervalArcadeFrames = BeatExecutionArcadeFrames + beatWaitArcadeFrames;
        _fireBeatsRemaining = ArcadeRandom.PickUpTo(random, fireIntervalBeats);
    }

    /// <summary>Alive until it is killed, then dead at once. The brain has no death animation of its own.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the brain's top-left corner is.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>OBJX</c> and <c>OBJY</c>.</item>
    ///         <item>Disassembly: no address in the disassembly (names of the original source only); the object's blitter destination is at offsets <c>$04</c> and <c>$05</c> of the object.</item>
    ///     </list>
    /// </remarks>
    public IntVector2 Position => _position;

    /// <summary>The family member this brain is chasing, or null when it is chasing the player.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>BRNL0</c>, the object the brain's <c>PD2</c> pointer
    ///             leads to. It is null until the first <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>, because the ROM works the target out inside the
    ///             <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> (notes §18.8).
    ///         </item>
    ///         <item>Disassembly: <c>BRAIN_AI</c> (<c>$1BEE</c>).</item>
    ///     </list>
    /// </remarks>
    internal FamilyMember? TargetFamilyMember { get; private set; }

    /// <summary>Which place in the family list this brain is chasing.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>PD2</c>, the brain's target.</item>
    ///         <item>Disassembly: the brain's target field at offset <c>$09</c> of its process (<c>$1BEE</c>).</item>
    ///     </list>
    /// </remarks>
    internal int TargetFamilySlot => _targetSlot;

    /// <summary>
    ///     Sets the time of the brain's first <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>, on the <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see> the game goes live. The <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> timer is set so that it
    ///     comes due when that time has gone by.
    /// </summary>
    /// <param name="playField">The playfield, which works out how long the wait is.</param>
    public void BeginPlay(PlayField playField)
    {
        _beatArcadeFrameTimer = playField.CreateFirstBeatTimer(LivePollArcadeFrames, FirstBeatNapArcadeFrames, _beatIntervalArcadeFrames);
    }

    /// <summary>Draws the brain. While it is reprogramming, it is drawn on top of a solid block of colour.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BRNON</c>.</item>
    ///         <item>Disassembly: <c>DRAW_BRAIN_IN_PROGGING_STATE</c> (<c>$1DAF</c>).</item>
    ///     </list>
    /// </remarks>
    /// <param name="spriteBatch">What the brain is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        if (IsReprogramming())
            _textureSet.BlitterDraw.DrawSolidRectangle(spriteBatch, GetBounds(),
                _textureSet.BlitterDraw.GetPaletteIndexColour(ReprogramTuning.ShapePaletteIndex));

        _textureSet.BlitterDraw.DrawSprite(spriteBatch, GetCurrentAnimationFrameTexture(), GetBounds(), Color.White);
    }

    /// <summary>The box the brain takes up on the screen.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);
    }

    /// <summary>The animation frame the brain is showing now. The explosion copies this when the brain is shot.</summary>
    public Texture2D GetCurrentAnimationFrameTexture()
    {
        return _textureSet.BrainAnimationFrameTextures[GetWalkAnimationFrameIndex()];
    }

    /// <inheritdoc />
    /// <remarks>It is the animation frame showing now (<see cref="GetCurrentAnimationFrameTexture" />).</remarks>
    public Texture2D GetExplosionAnimationFrameTexture()
    {
        return GetCurrentAnimationFrameTexture();
    }

    /// <inheritdoc />
    /// <remarks>It is the same box as <see cref="GetBounds" />.</remarks>
    public Rectangle GetExplosionBounds()
    {
        return GetBounds();
    }

    /// <summary>True while this brain is turning a family member into a prog.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BMUT</c> to <c>BMUT4</c>.</item>
    ///         <item>
    ///             Disassembly: the "progging" flag at <c>$9895</c> (<c>brain_progging_flag</c>) and
    ///             <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c> (<c>$1CC2</c>).
    ///         </item>
    ///     </list>
    /// </remarks>
    public bool IsReprogramming()
    {
        return _victimFamilyMember is not null;
    }

    /// <summary>Takes the brain off the field at once.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>BRNKIL</c> (the explosion is <c>HVEXST</c>, the score
    ///             <c>$0150</c> and the sound <c>BKSND</c>)
    ///         </item>
    ///         <item>Disassembly: <c>BRAIN_COLLISION_HANDLER</c> (<c>$1DD6</c>)</item>
    ///     </list>
    /// </remarks>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>
    ///     Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see> of the brain. When a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> is due it chases its target, takes a step, changes its animation frame
    ///     and counts down to its next missile.
    /// </summary>
    /// <param name="gameTime">Not used. The brain counts in <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>, not in seconds.</param>
    /// <param name="playField">The playfield the brain is on.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BRAIN</c>, <c>BRNL</c> and <c>BRNL1</c></item>
    ///         <item>Disassembly: <c>BRAIN_AI</c> (<c>$1BEE</c>) and <c>ANIMATE_BRAIN</c> (<c>$1C0F</c>)</item>
    ///     </list>
    /// </remarks>
    public void Update(GameTime gameTime, PlayField playField)
    {
        if (!this.IsAlive()) return;

        if (playField.RobotsFrozen()) return;

        if (_victimFamilyMember is { } victimFamilyMember)
        {
            if (_reprogramArcadeFrameTimer.Tick(ReprogramTuning.StepIntervalArcadeFrames)) TakeReprogramStep(playField, victimFamilyMember);
            return;
        }

        if (_beatArcadeFrameTimer.Tick(_beatIntervalArcadeFrames)) ActOnBeat(playField);
    }

    /// <summary>
    ///     Starts turning a family member into a prog. The family member is moved next to the brain and the reprogramming
    ///     animation begins.
    /// </summary>
    /// <param name="familyMember">The family member being reprogrammed. They are out of play until the animation ends.</param>
    /// <param name="playfieldBounds">The inside of the playfield wall, so that the family member is put inside it.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>BMUT</c>, <c>BMUT00</c>, <c>BMUT10</c> and <c>BMUT1</c>;
    ///             the test that the brain is close enough is the end of <c>BRNL1</c>
    ///         </item>
    ///         <item>Disassembly: <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c> (<c>$1CC2</c>)</item>
    ///     </list>
    /// </remarks>
    internal void BeginReprogramming(FamilyMember familyMember, Rectangle playfieldBounds)
    {
        _victimFamilyMember = familyMember;
        familyMember.BeginReprogramming();
        _reprogramRedrawsRemaining = ReprogramTuning.Iterations * ReprogramTuning.RedrawsPerIteration;
        _reprogramMovingDown = true;
        _reprogramArcadeFrameTimer = ArcadeFrameTimer.CreateDue(ReprogramTuning.StepIntervalArcadeFrames);

        var familyMemberWidth = familyMember.GetBounds().Width;
        var x = _position.X - familyMemberWidth - ScreenSize.ToPortPixelsFromColumns(VictimGapColumns);
        var walkSequence = WalkSequence.Left;
        if (x < playfieldBounds.X)
        {
            x = _position.X + ScreenSize.ToPortPixelsFromColumns(VictimRightOffsetColumns);
            walkSequence = WalkSequence.Right;
            if (x >= playfieldBounds.Right - ScreenSize.ToPortPixelsFromColumns(VictimRightWallMarginColumns))
            {
                x = _position.X - familyMemberWidth - ScreenSize.ToPortPixelsFromColumns(VictimGapColumns);
                walkSequence = WalkSequence.Left;
            }
        }

        _walkSequence = walkSequence;
        _walkCycleIndex = 0;

        _victimRestingY = _position.Y + ScreenSize.ToPortPixelsFromArcadePixels(VictimDropRows);
        familyMember.MoveTo(new IntVector2(x, _victimRestingY));
    }

    /// <summary>Finds the family member the brain could catch right now: the one it is chasing, if it is close enough.</summary>
    /// <returns>The family member, or null when the brain cannot catch anyone.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c>, the end of <c>BRNL1</c> (<c>ADDB #3 / CMPB #$6 / BHI</c>
    ///             then <c>ADDA #3 / CMPA #6 / BLS BMUT</c>)
    ///         </item>
    ///         <item>Disassembly: <c>ANIMATE_BRAIN</c> (<c>$1C0F</c>) from <c>$1C48</c> to <c>$1C56</c></item>
    ///     </list>
    ///     The check compares the two top-left corners, not the sprites. Only the family member the brain is chasing
    ///     can be caught. This method only answers the question. It does not start the reprogramming (see
    ///     <see cref="BeginReprogramming" />).
    /// </remarks>
    internal FamilyMember? GetCatchableTarget()
    {
        if (!this.IsAlive() || IsReprogramming()) return null;

        if (TargetFamilyMember is not { } familyMember || !familyMember.IsGraspable()) return null;

        var inReach = Math.Abs(_position.X - familyMember.Position.X) <= CatchReachX
                      && Math.Abs(_position.Y - familyMember.Position.Y) <= CatchReachY;
        return inReach ? familyMember : null;
    }

    /// <summary>The family member being reprogrammed, or null if the brain is not reprogramming anyone.</summary>
    internal FamilyMember? GetVictim()
    {
        return _victimFamilyMember;
    }

    /// <summary>
    ///     Which of the brain's animation frames is showing, counting from 0 in
    ///     <see cref="TextureSet.BrainAnimationFrameTextures" />.
    /// </summary>
    internal int GetWalkAnimationFrameIndex()
    {
        return (int)_walkSequence * 3 + WalkCycle[_walkCycleIndex];
    }

    /// <summary>
    ///     Lets go of the family member being reprogrammed, if there is one. That family member is then lost.
    /// </summary>
    /// <remarks>
    ///     Use <see cref="GetVictim" /> first if you need to know who it was.
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>BRNKIL</c>, the <c>BRNK2</c> branch for a brain shot
    ///             while reprogramming.
    ///         </item>
    ///         <item>Disassembly: <c>BRAIN_COLLISION_HANDLER</c> (<c>$1DD6</c>).</item>
    ///     </list>
    /// </remarks>
    internal void ReleaseVictim()
    {
        var victimFamilyMember = _victimFamilyMember;
        _victimFamilyMember = null;
        victimFamilyMember?.FinishReprogramming();
    }

    /// <summary>Gives the brain a new place in the family list to chase.</summary>
    /// <param name="targetFamilySlot">The place to chase from now on.</param>
    /// <remarks>
    ///     This is not in the arcade game. It lets the brain pick its target again once the family is on the field.
    /// </remarks>
    internal void Retarget(int targetFamilySlot)
    {
        _targetSlot = targetFamilySlot;
    }

    /// <summary>Gets the walk that matches a step. Left and right win over up and down.</summary>
    /// <param name="step">The step the brain tried to take.</param>
    /// <returns>The walk for the way the step goes.</returns>
    private static WalkSequence GetWalkSequence(IntVector2 step)
    {
        var isMovingSideways = step.X != 0;
        if (isMovingSideways)
        {
            var isMovingRight = step.X > 0;
            if (isMovingRight) return WalkSequence.Right;

            return WalkSequence.Left;
        }

        var isMovingUp = step.Y < 0;
        if (isMovingUp) return WalkSequence.Up;

        return WalkSequence.Down;
    }

    /// <summary>Says whether the brain would still be inside the playfield if its left edge were at <paramref name="x" />.</summary>
    /// <param name="bounds">The inside of the playfield wall.</param>
    /// <param name="x">Where the brain's left edge would be.</param>
    private static bool IsInsideX(Rectangle bounds, int x)
    {
        return x >= bounds.X && x + CollisionSize.Width <= bounds.Right;
    }

    /// <summary>Says whether the brain would still be inside the playfield if its top edge were at <paramref name="y" />.</summary>
    /// <param name="bounds">The inside of the playfield wall.</param>
    /// <param name="y">Where the brain's top edge would be.</param>
    private static bool IsInsideY(Rectangle bounds, int y)
    {
        return y >= bounds.Y && y + CollisionSize.Height <= bounds.Bottom;
    }

    /// <summary>
    ///     What the brain does on its <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. It picks who to chase, takes a step towards them, and fires a cruise missile
    ///     if it is time to.
    /// </summary>
    /// <param name="playField">The playfield.</param>
    private void ActOnBeat(PlayField playField)
    {
        UpdateTargetSlot(playField);
        TargetFamilyMember = GetTarget(playField);
        var step = GetStepTowardTarget(TargetFamilyMember?.Position ?? playField.Player.Position);
        TakeStep(playField, step);
        AdvanceWalkAnimation(step);

        if (--_fireBeatsRemaining <= 0) FireIfPossible(playField);
    }

    /// <summary>
    ///     Moves the brain on to its next walking animation frame. If it has turned to face a new way, the walking starts
    ///     again from the first animation frame.
    /// </summary>
    /// <param name="step">The step the brain tried to take this <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>BRN5</c> to <c>BRNSD1</c> (<c>BRNDIR</c> and
    ///             <c>BRNSD</c>).
    ///         </item>
    ///         <item>Disassembly: <c>ANIMATE_BRAIN</c> (<c>$1C0F</c>), from <c>$1C58</c> to <c>$1C8F</c>.</item>
    ///     </list>
    /// </remarks>
    private void AdvanceWalkAnimation(IntVector2 step)
    {
        var nextWalkSequence = GetWalkSequence(step);

        if (nextWalkSequence == _walkSequence)
        {
            _walkCycleIndex = (_walkCycleIndex + 1) % WalkCycle.Length;
        }
        else
        {
            _walkSequence = nextWalkSequence;
            _walkCycleIndex = 0;
        }
    }

    /// <summary>Fires a cruise missile if there is room for one, then picks how long to wait for the next.</summary>
    /// <param name="playField">The playfield, which says whether there is room and makes the missile.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>BRNSHT</c> (at most 8 missiles; the wait is picked first,
    ///             even when no missile is made)
    ///         </item>
    ///         <item>Disassembly: <c>CREATE_CRUISE_MISSILE</c> (<c>$2006</c>)</item>
    ///     </list>
    /// </remarks>
    private void FireIfPossible(PlayField playField)
    {
        if (playField.CanFireCruiseMissile())
            playField.SpawnCruiseMissile(_position + new IntVector2(
                ScreenSize.ToPortPixelsFromColumns(MissileMuzzleXColumns),
                ScreenSize.ToPortPixelsFromArcadePixels(MissileMuzzleYRows)));

        _fireBeatsRemaining = ArcadeRandom.PickUpTo(_random, _fireIntervalBeats);
    }

    /// <summary>Works out the small step the brain takes towards the target.</summary>
    /// <param name="target">The place the brain is heading for: its family member, or the player.</param>
    /// <returns>The step the brain tries to take, even if a wall would stop it. The brain turns to face this step.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BRNL1</c> to <c>BRN40</c></item>
    ///         <item>Disassembly: <c>ANIMATE_BRAIN</c> (<c>$1C0F</c>)</item>
    ///     </list>
    ///     Sideways, the brain stops moving once it is close enough. Up and down, it always
    ///     moves, and goes down when the target is level with it.
    /// </remarks>
    private IntVector2 GetStepTowardTarget(IntVector2 target)
    {
        var dx = 0;
        var targetDeltaX = target.X - _position.X;
        if (Math.Abs(targetDeltaX) > ApproachDeadZonePixels) dx = Math.Sign(targetDeltaX) * StepXPixels;

        var dy = -StepYPixels;
        var isTargetBelowOrLevel = target.Y >= _position.Y;
        if (isTargetBelowOrLevel) dy = StepYPixels;

        return new IntVector2(dx, dy);
    }

    /// <summary>The family member the brain is chasing, or null if the brain is chasing the player.</summary>
    /// <param name="playField">The playfield, which keeps the family list.</param>
    private FamilyMember? GetTarget(PlayField playField)
    {
        return playField.GetFamilyMemberInSlot(_targetSlot);
    }

    /// <summary>
    ///     Does one step of the reprogramming. The family member is shaken a little way down, then a little way back up,
    ///     until the steps run out and a prog appears.
    /// </summary>
    /// <param name="playField">The playfield the brain is on.</param>
    /// <param name="victimFamilyMember">The family member being reprogrammed.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>BMUTL</c> to <c>BMUT4</c> (20 rounds of a move down then
    ///             a move up, a random 0 to 7 rows each; <c>PROGST</c> at the end)
    ///         </item>
    ///         <item>
    ///             Disassembly: <c>DRAW_PROG_STARTING_TO_SHAKE</c> (<c>$1D37</c>) and <c>CREATE_PROG</c>
    ///             (<c>$1E19</c>)
    ///         </item>
    ///     </list>
    ///     Each shake is measured from the family member's resting place, as in the ROM, so the shakes never drift.
    /// </remarks>
    private void TakeReprogramStep(PlayField playField, FamilyMember victimFamilyMember)
    {
        var bounds = playField.PlayfieldWall.PlayfieldBounds;
        var jitter = _random.Next(ReprogramTuning.JitterPixels);
        var height = victimFamilyMember.GetBounds().Height;
        var y = Math.Max(_victimRestingY - jitter, bounds.Y);
        if (_reprogramMovingDown) y = Math.Min(_victimRestingY + jitter, bounds.Bottom - height);

        victimFamilyMember.MoveTo(new IntVector2(victimFamilyMember.Position.X, y));

        if (_reprogramMovingDown) playField.PlaySoundFrom(SoundTables.Programming, victimFamilyMember.GetBounds());

        if (--_reprogramRedrawsRemaining > 0)
        {
            _reprogramMovingDown = !_reprogramMovingDown;
            return;
        }

        playField.PlaySoundFrom(SoundTables.FamilyMemberProgFinalConversion, victimFamilyMember.GetBounds());
        victimFamilyMember.FinishReprogramming();
        playField.SpawnProg(victimFamilyMember.Position, victimFamilyMember.Kind);
        _victimFamilyMember = null;
    }

    /// <summary>Takes a step, as long as the step stays inside the playfield.</summary>
    /// <param name="playField">The playfield, whose walls the brain must stay inside.</param>
    /// <param name="step">The step to take (see <see cref="GetStepTowardTarget" />).</param>
    /// <remarks>
    ///     The ROM undoes both steps if either would leave the playfield, which pins a brain against a wall for good.
    ///     This game checks each step by itself on purpose, so a brain against a wall slides along it. That is the
    ///     author's choice.
    /// </remarks>
    private void TakeStep(PlayField playField, IntVector2 step)
    {
        var bounds = playField.PlayfieldWall.PlayfieldBounds;
        if (step.X != 0 && IsInsideX(bounds, _position.X + step.X)) _position = _position with { X = _position.X + step.X };

        if (IsInsideY(bounds, _position.Y + step.Y)) _position = _position with { Y = _position.Y + step.Y };
    }

    /// <summary>
    ///     Moves the brain on to the nearest family member to chase, if the family member it was chasing is gone and
    ///     another is still on the field.
    /// </summary>
    /// <param name="playField">The playfield, which keeps the family list.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>BRNL0</c> and <c>GETHTG</c></item>
    ///         <item>
    ///             Disassembly: <c>BRAIN_AI</c> (<c>$1BEE</c>) and <c>FIND_NEAREST_FAMILY_MEMBER_TO_PROG</c>
    ///             (<c>$1B95</c>)
    ///         </item>
    ///     </list>
    ///     If the place the brain was chasing is empty, the brain chases the player. But if any family member is still
    ///     on the field, it picks the nearest one at once.
    /// </remarks>
    private void UpdateTargetSlot(PlayField playField)
    {
        if (playField.GetFamilyMemberInSlot(_targetSlot) is not null) return;

        if (!playField.AnyFamilyMemberAvailable()) return;

        _targetSlot = playField.GetNearestFamilySlot(_position);
    }
}
