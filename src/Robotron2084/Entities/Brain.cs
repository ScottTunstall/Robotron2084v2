using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A brain is a floating robot that chases a family member and turns them into a prog. It also shoots cruise missiles at you.</summary>
/// <seealso cref="PlayField"/>
/// <seealso cref="Human"/>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRB10.ASM</c>: <c>BRNSTV</c> (start the brains), <c>BRAIN</c>/<c>BRNL</c>/<c>BRNL1</c> (each beat), <c>BMUT</c> (reprogram a human), <c>BRNSHT</c> (fire a cruise missile), <c>BRNKIL</c> (shot by a laser)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c>: <c>INITIALISE_ALL_BRAINS</c> (<c>$1AF6</c>), <c>BRAIN_AI</c> (<c>$1BEE</c>), <c>ANIMATE_BRAIN</c> (<c>$1C11</c>), <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c> (<c>$1CC2</c>), <c>CREATE_CRUISE_MISSILE</c> (<c>$2006</c>), <c>BRAIN_COLLISION_HANDLER</c> (<c>$1DD6</c>)</item>
/// </list>
/// Sideways distances here are in columns (two arcade pixels each) and up-and-down distances in rows (one arcade pixel each), as in the ROM; see docs/glossary.md.
/// </remarks>
public sealed class Brain : IEntity, IExplodable, IRemovable
{
    /// <summary>The ROM frame a brain spends on the beat itself, added to the wave's wait to get the interval between beats.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNSLP</c>, the sleep of <c>BRNSPD</c> frames at the end of each beat. Disassembly: <c>BRAIN_AI</c> (<c>$1BEE</c>), the wait loaded from <c>$BE63</c> at <c>$1C9C</c>.</remarks>
    private const int BeatExecutionRomFrames = 1;

    /// <summary>How far to the right of the brain's top-left corner a new cruise missile appears.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNSHT</c>, <c>ADDD #$0304</c>, the 3 columns. Disassembly: <c>CREATE_CRUISE_MISSILE</c> (<c>$2006</c>).</remarks>
    private const int MissileMuzzleXColumns = 3;

    /// <summary>How far below the brain's top-left corner a new cruise missile appears.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNSHT</c>, <c>ADDD #$0304</c>, the 4 rows. Disassembly: <c>CREATE_CRUISE_MISSILE</c> (<c>$2006</c>).</remarks>
    private const int MissileMuzzleYRows = 4;

    /// <summary>How far below the brain's top-left corner the victim stands while being reprogrammed.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BMUT1</c>, <c>ADDA #2</c> on Y, 2 rows. Disassembly: <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c> (<c>$1CC2</c>) at <c>$1CEB</c>.</remarks>
    private const int VictimDropRows = 2;

    /// <summary>The gap between the victim and the brain's left side when the victim stands on the left.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BMUT00</c>, <c>SUBA #1</c> on X, 1 column. Disassembly: <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c> (<c>$1CC2</c>) at <c>$1CCD</c>.</remarks>
    private const int VictimGapColumns = 1;

    /// <summary>How far right of the brain's top-left corner the victim stands when there is no room on the left.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BMUT10</c>, <c>ADDA #8</c> on X, 8 columns (16 arcade pixels). Disassembly: <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c> (<c>$1CC2</c>) at <c>$1CDC</c>.</remarks>
    private const int VictimRightOffsetColumns = 8;

    /// <summary>How close to the right wall the victim may stand before the brain tries the left side again.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BMUT10</c>, <c>CMPA #XMAX-4</c> on X, 4 columns. Disassembly: <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c> (<c>$1CC2</c>) at <c>$1CDE</c>.</remarks>
    private const int VictimRightWallMarginColumns = 4;

    /// <summary>How close sideways the brain gets to its target before it stops moving sideways. Up and down has no such gap.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNL1</c>, <c>ADDA #2 / CMPA #4 / BLS</c> on X, 2 columns. Disassembly: <c>ANIMATE_BRAIN</c> (<c>$1C11</c>) at <c>$1C11</c>.</remarks>
    private static readonly int ApproachDeadZonePixels = ScreenSize.ToPortPixelsFromColumns(2);

    /// <summary>How close sideways the brain's and the human's top-left corners must be for a catch, in port pixels.</summary>
    private static readonly int CatchReachX = ScreenSize.ToPortPixelsFromColumns(ReprogramTuning.CatchReachColumns);

    /// <summary>How close up and down the brain's and the human's top-left corners must be for a catch, in port pixels.</summary>
    private static readonly int CatchReachY = ScreenSize.ToPortPixels(ReprogramTuning.CatchReachRows);

    /// <summary>The size of the brain's sprite, which is also the size of its hit box.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRDP1</c>, 7 bytes by 16 rows (14 by 16 arcade pixels). Disassembly: the brain standing still, <c>$2159</c>.</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.BrainCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.BrainCollisionSize.Height));

    /// <summary>How far the brain moves sideways on each beat.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNL1</c>, a step of 1 column. Disassembly: <c>ANIMATE_BRAIN</c> (<c>$1C11</c>).</remarks>
    private static readonly int StepXPixels = ScreenSize.ToPortPixelsFromColumns(1);

    /// <summary>How far the brain moves up or down on each beat.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNL1</c>, a step of 1 row. Disassembly: <c>ANIMATE_BRAIN</c> (<c>$1C11</c>).</remarks>
    private static readonly int StepYPixels = ScreenSize.ToPortPixels(1);

    /// <summary>The order the brain's three walking animation frames are shown in: first, second, first, third.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNAL</c>, <c>BRNAR</c>, <c>BRNAD</c> and <c>BRNAU</c>, each listing its frames as 1, 2, 1, 3. Disassembly: <c>BRAIN_ANIMATION_TABLES</c> (<c>$1CA2</c>).</remarks>
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    /// <summary>How long between beats, in clock units.</summary>
    private readonly int _beatIntervalClockUnits;

    /// <summary>The longest wait, in beats, between cruise missiles. Each wait is a random number from 1 up to this.</summary>
    private readonly int _fireIntervalBeats;

    private readonly Random _random;
    private readonly SpriteSet _sprites;

    /// <summary>Builds up, a tick at a time, until it is time for the next beat.</summary>
    private int _beatTimer;

    /// <summary>Which way the brain is facing. A new brain faces down.</summary>
    private WalkFacing _facing = WalkFacing.Down;

    /// <summary>Beats left before the brain fires its next cruise missile.</summary>
    private int _fireBeatsRemaining;

    private IntVector2 _position;

    /// <summary>True when the next reprogramming step moves the human down, false when it moves them back up.</summary>
    private bool _reprogramMovingDown;

    /// <summary>Where the human stands, up and down, while being reprogrammed. Each shake is measured from here.</summary>
    private int _victimRestingY;

    /// <summary>Steps left before the reprogramming is finished.</summary>
    private int _reprogramRedrawsRemaining;

    /// <summary>Builds up, a tick at a time, until it is time for the next reprogramming step.</summary>
    private int _reprogramTimer;

    /// <summary>Which place in the family list the brain is chasing.</summary>
    private int _targetSlot;

    /// <summary>The human being reprogrammed right now, or null.</summary>
    private Human? _victim;

    /// <summary>Where the brain is in its four-step walking pattern, from 0 to 3.</summary>
    private int _walkCycleStep;

    /// <summary>Makes a brain. It takes its first step on its first beat.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the brain.</param>
    /// <param name="random">Where the missile waits and the reprogramming wobble come from.</param>
    /// <param name="beatWaitRomFrames">How many ROM frames this wave's brains wait after each beat. A bigger number is a slower brain. The interval between beats is this plus the frame the beat itself takes.</param>
    /// <param name="fireIntervalBeats">The longest this wave's brains wait between cruise missiles, in beats.</param>
    /// <param name="targetFamilySlot">Which place in the family list the brain chases at the start.</param>
    /// <remarks>
    /// Original source: <c>RRB10.ASM</c> <c>BRNSTV</c> (the wait is <c>BSHTIM</c> through <c>RMAX</c>, the target comes from <c>GETHTG</c>). Disassembly: <c>INITIALISE_ALL_BRAINS</c> (<c>$1AF6</c>) and <c>FIND_NEAREST_FAMILY_MEMBER_TO_PROG</c> (<c>$1B95</c>).
    /// The playfield passes the nearest slot as the brain is made, before the family exists. That is the ROM's own order, and it is why every brain in the arcade chases Mikey (notes §18.8).
    /// </remarks>
    public Brain(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int beatWaitRomFrames,
        int fireIntervalBeats,
        int targetFamilySlot)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _fireIntervalBeats = fireIntervalBeats;
        _targetSlot = targetFamilySlot;
        _beatIntervalClockUnits = ArcadeClock.ToClockUnits(BeatExecutionRomFrames + beatWaitRomFrames);
        _fireBeatsRemaining = 1 + random.Next(fireIntervalBeats);
    }

    /// <summary>The box around the brain, from its top-left corner.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>The animation frame the brain is showing now. The explosion copies this when the brain is shot.</summary>
    public Texture2D GetCurrentAnimationFrame() => _sprites.BrainAnimationFrames[WalkAnimationFrameIndex];

    /// <summary>True while this brain is turning a human into a prog.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BMUT</c> to <c>BMUT4</c>. Disassembly: the "progging" flag at <c>$9895</c> (<c>brain_progging_flag</c>) and <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c> (<c>$1CC2</c>).</remarks>
    public bool IsReprogramming => _victim is not null;

    /// <summary>Alive until a laser hits it, then dead at once. The brain never plays a dying animation of its own.</summary>
    /// <remarks>The explosion that follows is made by the playfield, not by the brain (<see cref="IExplodable"/>).</remarks>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The top-left corner of the brain.</summary>
    /// <remarks>Original source: <c>OBJX</c> and <c>OBJY</c>. Disassembly: the blitter destination at offsets <c>$04</c> and <c>$05</c> of the object.</remarks>
    public IntVector2 Position => _position;

    /// <summary>The family member this brain is chasing, or null when it is chasing the player.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNL0</c>, the object the brain's <c>PD2</c> pointer resolves to. It is null until the first beat, because the ROM works the target out inside the beat (notes §18.8). Disassembly: <c>BRAIN_AI</c> (<c>$1BEE</c>).</remarks>
    internal Human? Target { get; private set; }

    /// <summary>Which place in the family list this brain is chasing.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>PD2</c>, the brain's target. Disassembly: the brain's target field at offset <c>$09</c> of its process (<c>$1BEE</c>).</remarks>
    internal int TargetFamilySlot => _targetSlot;

    /// <summary>Which of the brain's animation frames is showing, counting from 0 in <see cref="SpriteSet.BrainAnimationFrames"/>.</summary>
    internal int WalkAnimationFrameIndex => (int)_facing * 3 + WalkCycle[_walkCycleStep];

    /// <summary>Draws the brain. While it is reprogramming, it is drawn on top of a solid block of colour.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNON</c>. Disassembly: <c>DRAW_BRAIN_IN_PROGGING_STATE</c> (<c>$1DAF</c>).</remarks>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        if (IsReprogramming)
        {
            _sprites.Blitter.DrawSolidRectangle(spriteBatch, Bounds, _sprites.Blitter.GetSlotColour(ReprogramTuning.ShapeSlot));
        }

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), Bounds, Color.White);
    }

    /// <summary>Takes the brain off the field at once. The playfield makes the explosion and gives the points.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>BRNKIL</c> (the explosion is <c>HVEXST</c>, the score <c>$0150</c> and the sound <c>BKSND</c>)</item>
    /// <item>Disassembly: <c>BRAIN_COLLISION_HANDLER</c> (<c>$1DD6</c>)</item>
    /// </list>
    /// A brain shot while it is reprogramming loses its victim, which is done by <see cref="PlayField"/> (<c>ReleaseVictimsOfDeadBrains</c>).
    /// </remarks>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Runs one tick of the brain. When a beat is due it chases its target, takes a step, changes its animation frame and counts down to its next missile.</summary>
    /// <param name="gameTime">Not used. The brain counts in ticks, not in seconds.</param>
    /// <param name="field">The playfield the brain is on.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>BRAIN</c>, <c>BRNL</c> and <c>BRNL1</c></item>
    /// <item>Disassembly: <c>BRAIN_AI</c> (<c>$1BEE</c>) and <c>ANIMATE_BRAIN</c> (<c>$1C11</c>)</item>
    /// </list>
    /// </remarks>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive())
        {
            return;
        }

        if (field.RobotsFrozen)
        {
            return;
        }

        if (_victim is { } victim)
        {
            AdvanceReprogramming(field, victim);
            return;
        }

        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < _beatIntervalClockUnits)
        {
            return;
        }

        _beatTimer -= _beatIntervalClockUnits;

        Target = ResolveTarget(field);
        AdvanceWalkAnimation(StepTowardTarget(field, Target?.Position ?? field.Player.Position));

        if (--_fireBeatsRemaining <= 0)
        {
            FireIfPossible(field);
        }
    }

    /// <summary>Starts turning a human into a prog. The human is moved next to the brain and the reprogramming animation begins.</summary>
    /// <param name="human">The human being reprogrammed. They are out of play until the animation ends.</param>
    /// <param name="playfieldBounds">The edges of the playfield, so the human is placed inside them.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>BMUT</c>, <c>BMUT00</c>, <c>BMUT10</c> and <c>BMUT1</c>; the test that the brain is close enough is the end of <c>BRNL1</c></item>
    /// <item>Disassembly: <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c> (<c>$1CC2</c>)</item>
    /// </list>
    /// </remarks>
    internal void BeginReprogramming(Human human, Rectangle playfieldBounds)
    {
        _victim = human;
        human.BeginReprogramming();
        _reprogramRedrawsRemaining = ReprogramTuning.Iterations * ReprogramTuning.RedrawsPerIteration;
        _reprogramMovingDown = true;
        _reprogramTimer = ArcadeClock.ToClockUnits(ReprogramTuning.StepRomFrames);

        int humanWidth = human.Bounds.Width;
        int x = _position.X - humanWidth - ScreenSize.ToPortPixelsFromColumns(VictimGapColumns);
        WalkFacing facing = WalkFacing.Left;
        if (x < playfieldBounds.X)
        {
            x = _position.X + ScreenSize.ToPortPixelsFromColumns(VictimRightOffsetColumns);
            facing = WalkFacing.Right;
            if (x >= playfieldBounds.Right - ScreenSize.ToPortPixelsFromColumns(VictimRightWallMarginColumns))
            {
                x = _position.X - humanWidth - ScreenSize.ToPortPixelsFromColumns(VictimGapColumns);
                facing = WalkFacing.Left;
            }
        }

        _facing = facing;
        _walkCycleStep = 0;

        _victimRestingY = _position.Y + ScreenSize.ToPortPixels(VictimDropRows);
        human.MoveTo(new IntVector2(x, _victimRestingY));
    }

    /// <summary>Finds the family member the brain could catch right now: the one it is chasing, if it is close enough.</summary>
    /// <returns>The family member, or null when the brain cannot catch anyone.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c>, the end of <c>BRNL1</c> (<c>ADDB #3 / CMPB #$6 / BHI</c> then <c>ADDA #3 / CMPA #6 / BLS BMUT</c>)</item>
    /// <item>Disassembly: <c>ANIMATE_BRAIN</c> (<c>$1C11</c>) from <c>$1C48</c> to <c>$1C56</c></item>
    /// </list>
    /// The test compares the two top-left corners, not the sprites, and only the brain's own target can be caught. This only
    /// asks: starting the reprogramming is for the field to decide (<see cref="BeginReprogramming"/>).
    /// </remarks>
    internal Human? GetCatchableTarget()
    {
        if (!this.IsAlive() || IsReprogramming)
        {
            return null;
        }

        if (Target is not { } human || !human.IsGraspable())
        {
            return null;
        }

        bool inReach = Math.Abs(_position.X - human.Position.X) <= CatchReachX
            && Math.Abs(_position.Y - human.Position.Y) <= CatchReachY;
        return inReach ? human : null;
    }

    /// <summary>Lets go of the human being reprogrammed, if there is one, who is then lost. Used when the brain is shot part way through.</summary>
    /// <returns>The human the brain was reprogramming, or null if it was not reprogramming anyone.</returns>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNKIL</c>, the <c>BRNK2</c> branch for a brain shot while reprogramming. Disassembly: <c>BRAIN_COLLISION_HANDLER</c> (<c>$1DD6</c>).</remarks>
    internal Human? ReleaseVictim()
    {
        Human? victim = _victim;
        _victim = null;
        victim?.FinishReprogramming();
        return victim;
    }

    /// <summary>Says whether the brain, put at this sideways position, would still be inside the playfield.</summary>
    private static bool FitsInsideX(Rectangle bounds, int x) =>
        x >= bounds.X && x + CollisionSize.Width <= bounds.Right;

    /// <summary>Says whether the brain, put at this up-and-down position, would still be inside the playfield.</summary>
    private static bool FitsInsideY(Rectangle bounds, int y) =>
        y >= bounds.Y && y + CollisionSize.Height <= bounds.Bottom;

    /// <summary>Does one step of the reprogramming. The human is shaken a little way down, then a little way back up, until the steps run out and a prog appears.</summary>
    /// <param name="field">The playfield the brain is on.</param>
    /// <param name="victim">The human being reprogrammed.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>BMUTL</c> to <c>BMUT4</c> (20 rounds of a move down then a move up, a random 0 to 7 rows each; <c>PROGST</c> at the end)</item>
    /// <item>Disassembly: <c>DRAW_PROG_STARTING_TO_SHAKE</c> (<c>$1D37</c>) and <c>CREATE_PROG</c> (<c>$1E19</c>)</item>
    /// </list>
    /// Each shake is measured from the human's resting place, as in the ROM, so the shakes never drift.
    /// </remarks>
    private void AdvanceReprogramming(PlayField field, Human victim)
    {
        _reprogramTimer += ArcadeClock.UnitsPerPortTick;
        if (_reprogramTimer < ArcadeClock.ToClockUnits(ReprogramTuning.StepRomFrames))
        {
            return;
        }

        _reprogramTimer -= ArcadeClock.ToClockUnits(ReprogramTuning.StepRomFrames);

        Rectangle bounds = field.Wall.PlayfieldBounds;
        int jitter = _random.Next(ReprogramTuning.JitterPixels);
        int height = victim.Bounds.Height;
        int y = _reprogramMovingDown
            ? Math.Min(_victimRestingY + jitter, bounds.Bottom - height)
            : Math.Max(_victimRestingY - jitter, bounds.Y);
        victim.MoveTo(new IntVector2(victim.Position.X, y));

        if (_reprogramMovingDown)
        {
            field.PlaySoundFrom(SoundTables.Programming, victim.Bounds);
        }

        if (--_reprogramRedrawsRemaining > 0)
        {
            _reprogramMovingDown = !_reprogramMovingDown;
            return;
        }

        field.PlaySoundFrom(SoundTables.HumanProgFinalConversion, victim.Bounds);
        victim.FinishReprogramming();
        field.SpawnProg(victim.Position, victim.Kind);
        _victim = null;
    }

    /// <summary>Moves the brain on to its next walking animation frame. If it has turned to face a new way, the walking starts again from the first frame.</summary>
    /// <param name="step">The step the brain tried to take this beat.</param>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRN5</c> to <c>BRNSD1</c> (<c>BRNDIR</c> and <c>BRNSD</c>). Disassembly: <c>ANIMATE_BRAIN</c> (<c>$1C11</c>), from <c>$1C58</c> to <c>$1C8F</c>.</remarks>
    private void AdvanceWalkAnimation(IntVector2 step)
    {
        WalkFacing nextFacing = step.X != 0
            ? (step.X > 0 ? WalkFacing.Right : WalkFacing.Left)
            : (step.Y < 0 ? WalkFacing.Up : WalkFacing.Down);

        if (nextFacing == _facing)
        {
            _walkCycleStep = (_walkCycleStep + 1) % WalkCycle.Length;
        }
        else
        {
            _facing = nextFacing;
            _walkCycleStep = 0;
        }
    }

    /// <summary>Fires a cruise missile if there is room for one, then picks how long to wait for the next.</summary>
    /// <param name="field">The playfield, which says whether there is room and makes the missile.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>BRNSHT</c> (at most 8 missiles; the wait is picked first, even when no missile is made)</item>
    /// <item>Disassembly: <c>CREATE_CRUISE_MISSILE</c> (<c>$2006</c>)</item>
    /// </list>
    /// </remarks>
    private void FireIfPossible(PlayField field)
    {
        if (field.CanFireCruiseMissile())
        {
            field.SpawnCruiseMissile(_position + new IntVector2(
                ScreenSize.ToPortPixelsFromColumns(MissileMuzzleXColumns),
                ScreenSize.ToPortPixels(MissileMuzzleYRows)));
        }

        _fireBeatsRemaining = 1 + _random.Next(_fireIntervalBeats);
    }

    /// <summary>Works out who the brain should chase: the family member it was chasing, a new one if that one is gone, or the player if the whole family is gone.</summary>
    /// <param name="field">The playfield, which keeps the family list.</param>
    /// <returns>The family member to chase, or null if the brain should chase the player.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>BRNL0</c> and <c>GETHTG</c></item>
    /// <item>Disassembly: <c>BRAIN_AI</c> (<c>$1BEE</c>) and <c>FIND_NEAREST_FAMILY_MEMBER_TO_PROG</c> (<c>$1B95</c>)</item>
    /// </list>
    /// An empty slot sends the brain to the player, unless a family member is still about, in which case it picks the nearest one straight away.
    /// </remarks>
    private Human? ResolveTarget(PlayField field)
    {
        if (field.GetFamilyMemberInSlot(_targetSlot) is { } chased)
        {
            return chased;
        }

        if (!field.AnyFamilyMemberAvailable())
        {
            return null;
        }

        _targetSlot = field.GetNearestFamilySlot(_position);
        return field.GetFamilyMemberInSlot(_targetSlot);
    }

    /// <summary>Takes a small step towards the target, as long as the step stays inside the playfield.</summary>
    /// <param name="field">The playfield, whose walls the brain must stay inside.</param>
    /// <param name="target">The place the brain is heading for: its family member, or the player.</param>
    /// <returns>The step the brain tried to take, even if a wall stopped it. The brain turns to face the step it tried.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>BRNL1</c> to <c>BRN40</c></item>
    /// <item>Disassembly: <c>ANIMATE_BRAIN</c> (<c>$1C11</c>)</item>
    /// </list>
    /// Sideways, the brain stops moving once it is close enough. Up and down, it always moves, and goes down when the target is level with it.
    /// The ROM undoes both steps if either would leave the playfield, which pins a brain against a wall for good. This port deliberately
    /// checks each step on its own, so a brain against a wall slides along it (the author's decision).
    /// </remarks>
    private IntVector2 StepTowardTarget(PlayField field, IntVector2 target)
    {
        int dx = 0;
        int targetDx = target.X - _position.X;
        if (Math.Abs(targetDx) > ApproachDeadZonePixels)
        {
            dx = Math.Sign(targetDx) * StepXPixels;
        }

        int dy = target.Y >= _position.Y ? StepYPixels : -StepYPixels;

        Rectangle bounds = field.Wall.PlayfieldBounds;
        if (dx != 0 && FitsInsideX(bounds, _position.X + dx))
        {
            _position = _position with { X = _position.X + dx };
        }

        if (FitsInsideY(bounds, _position.Y + dy))
        {
            _position = _position with { Y = _position.Y + dy };
        }

        return new IntVector2(dx, dy);
    }
}
