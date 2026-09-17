using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// One of the human family the player rescues (ROM RRH11; PHASE D). NOT an
/// enemy: a robot kill (in the R5 ROM the hulk is the only one) leaves a
/// skull &amp; crossbones; the player touches one to rescue it — the running
/// save count pays the 1000-5000 bonus (ScoreValues.RescueBonus).
///
/// Walk (ROM HUMAN process + HUMATB, decoded): 8 direction blocks, each 4
/// substeps of 2/1 alternating arcade-px steps (cardinals: 2,1 on the major
/// axis; diagonals: 2,1 on X plus 1 on Y); one step per ROM step period —
/// the ROM's is NAP 8, the playtest-doubled port value is 16 (round 8: the
/// ROM-accurate pace read "mommies walking too fast"; same pattern as the
/// spheroid/enforcer speed retunes); a new random direction every 1..128
/// steps (LSEED&amp;$7F+1) or whenever the
/// next step would leave the play area or land on a post/electrode
/// (obstacle → no move, re-aim);
/// staggered start 1..8 ticks (SEED&amp;7+1). Animation: 12 frames per member
/// = 4 directions × 3 walk frames (same layout as the player frames); the
/// diagonals reuse the left/right frame sets, exactly as the ROM's image
/// numbers do. <b>Not</b> frozen by <see cref="PlayField.RobotsFrozen"/>: the
/// ROM's HUMAN process is the one robot routine with no STATUS check, so the
/// family walks while the robots are held for the wave-start appear and while
/// the player is in the start grace (notes §88).
/// </summary>
public sealed class Human : IEntity
{
    /// <summary>
    /// Step period in ROM frames. **This is the ONE deliberate gameplay override in
    /// the port** (author, playtest round 8: "the mommies are walking too fast") —
    /// the Gospel is explicit that the human's process is `NAP 8` AND that it stores
    /// its new position on every pass (`HUM2 ... STD OX16,X`), i.e. one arcade pixel
    /// every 8 frames. The port walks one pixel every 16 frames = half the arcade
    /// rate, on the author's instruction, so DO NOT "fix" this to 8 without asking
    /// (notes §70).
    /// </summary>
    private const int StepPeriodRomTicks = 16;

    /// <summary>
    /// ROM HUMATB verbatim: 8 direction blocks × 4 substeps of
    /// (delta X, delta Y, walk-frame index within the direction's 3-frame
    /// set). Deltas are in arcade pixels (the ROM's "IMAGE #,DELTA X,DELTA Y"
    /// table; the $FF "start over" entries are the end-of-block markers).
    /// Block order: LEFT, RIGHT, DOWN, UP, UP+LEFT, RIGHT+UP, RIGHT+DOWN,
    /// DOWN+LEFT.
    /// </summary>
    private static readonly (int Dx, int Dy, int Frame)[] Steps =
    {
        // LEFT (ROM images 0,4,0,8)
        (-2, 0, 0), (-1, 0, 1), (-2, 0, 0), (-1, 0, 2),
        // RIGHT (12,16,12,20)
        (2, 0, 0), (1, 0, 1), (2, 0, 0), (1, 0, 2),
        // DOWN (24,28,24,32)
        (0, 1, 0), (0, 1, 1), (0, 1, 0), (0, 1, 2),
        // UP (36,40,36,44)
        (0, -1, 0), (0, -1, 1), (0, -1, 0), (0, -1, 2),
        // UP+LEFT (0,8,0,8)
        (-2, -1, 0), (-1, -1, 2), (-2, -1, 0), (-1, -1, 2),
        // RIGHT+UP (12,16,12,20)
        (2, -1, 0), (1, -1, 1), (2, -1, 0), (1, -1, 2),
        // RIGHT+DOWN (12,16,12,20)
        (2, 1, 0), (1, 1, 1), (2, 1, 0), (1, 1, 2),
        // DOWN+LEFT (0,4,0,4)
        (-2, 1, 0), (-1, 1, 1), (-2, 1, 0), (-1, 1, 1),
    };

    /// <summary>
    /// Which 3-frame set each block animates from — the ROM's diagonals reuse
    /// the cardinal frame sets (UP+LEFT/DOWN+LEFT → left set,
    /// RIGHT+UP/RIGHT+DOWN → right set): [L,R,D,U,L,R,R,L].
    /// </summary>
    private static readonly int[] FrameSet = { 0, 1, 2, 3, 0, 1, 1, 0 };

    /// <summary>Collision box per member = the ROM picture dimensions (arcade px), scaled at use.</summary>
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
    /// stands there for the rest of the wave (notes §77, §88).
    /// </summary>
    internal static int SpawnSquarePortPixels(HumanKind kind)
    {
        (int width, int height) = ArcadeCollisionSize(kind);
        return ScreenSize.Scaled(Math.Max(width, height));
    }

    private readonly Random _random;
    private readonly HumanKind _kind;
    private IntVector2 _position;
    private int _directionBlock; // 0..7, ROM table order (see Steps)
    private int _subStep;        // 0..3 within the block
    private int _stepFifths;     // the step period in exact 6ths (notes §52/§65)
    private int _reDirStepsRemaining;
    private int _startStaggerTicks;
    private int _frame;          // 0..11 into the member's 12 frames

    public Human(IntVector2 position, HumanKind kind, Random random)
    {
        _position = position;
        _kind = kind;
        _random = random;
        _directionBlock = random.Next(8);
        _reDirStepsRemaining = 1 + random.Next(128); // ROM: LSEED&$7F+1
        _startStaggerTicks = 1 + random.Next(8);     // ROM: SEED&7+1
        _stepFifths = 0;                             // the stagger's last tick IS the first step
    }

    public HumanKind Kind => _kind;

    public IntVector2 Position => _position;

    public Rectangle Bounds
    {
        get
        {
            (int w, int h) = ArcadeCollisionSize(_kind);
            return new(_position.X, _position.Y, ScreenSize.Scaled(w), ScreenSize.Scaled(h));
        }
    }

    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Steps taken so far — test hook for the step cadence (notes §65/§70).</summary>
    internal int StepCount { get; private set; }

    /// <summary>Killed by a robot (hulk): instant off, no animation (ROM DMAOFF).</summary>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>Rescued by the player (touch): instant off; the field awards the bonus.</summary>
    public void Rescue() => LifeState = EntityLifeState.Dead;

    public void Update(GameTime gameTime, PlayField field)
    {
        // NOTE: no `RobotsFrozen` gate. Every ROBOT routine checks the ROM's STATUS flag
        // (`ROBOT LDA STATUS`, `HULK LDA STATUS WAIT FOR STATUS TO GO`, `TST STATUS DONT START
        // EARLY GUYS`) — but the HUMAN process does not, and `HUMSTV` creates the family
        // BEFORE the appear sequence sets STATUS, so in the arcade they start walking while
        // the robots are still assembling and while the player cannot move. Holding them with
        // the robots is what made the family look asleep at the start of a wave (notes §88).
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

            // The stagger's LAST tick is the first step: `HUMSTV` stores the stagger straight
            // into the process's `PTIME`, so the wake-up — and therefore the walk body — runs
            // then. Falling through here (with the step clock still at zero) leaves the
            // following steps on the clean grid, which is what the ROM's uniform `NAP 8`
            // cadence gives (notes §88).
        }
        else
        {
            // 16 ROM frames = 19.2 port ticks, so the step runs on the exact-6ths
            // accumulator rather than the truncated PortTicks(16) = 19 that walked a
            // human 1% fast, one tick further ahead every 5 steps (notes §52/§65).
            _stepFifths += 5;
            if (_stepFifths < StepPeriodRomTicks * 6)
            {
                return;
            }

            _stepFifths -= StepPeriodRomTicks * 6;
        }

        StepCount++;

        (int dx, int dy, int frame) = Steps[_directionBlock * 4 + _subStep];
        _frame = 3 * FrameSet[_directionBlock] + frame;

        // One arcade pixel per table step = SpecScale internal pixels.
        IntVector2 candidate = _position + new IntVector2(dx, dy) * ScreenSize.SpecScale;
        Rectangle next = Bounds with { X = candidate.X, Y = candidate.Y };
        if (field.Wall.Intersects(next) || OverlapsLivingElectrode(next, field))
        {
            // ROM: obstacle (bounds CKLIM / a post via CKOBS) -> new direction,
            // no move this step.
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

    private void PickNewDirection()
    {
        _directionBlock = _random.Next(8);
        _subStep = 0;
        _frame = 3 * FrameSet[_directionBlock];
        _reDirStepsRemaining = 1 + _random.Next(128);
    }

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
            // ROM BMUT's HUMON with D = $AABB: the blitter fills the frame
            // rectangle with colour 1 ($AA = slot 10) and then draws the
            // human's OWN picture as a solid shape in colour 2 ($BB = slot 11)
            // (op $12 then op $1A — the disassembly's "solid colour with solid
            // background" wrapper, notes §47). BOTH slots cycle, which is the
            // "rapidly cycling background colour" its comment describes.
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
    /// True while a brain is reprogramming this human (ROM BMUT): it is off the
    /// human list — no longer a brain target, no longer rescuable, no longer
    /// killable by a hulk — its walk is frozen, and the BRAIN drives its
    /// position. It becomes a PROG when the animation finishes (never a skull).
    /// </summary>
    public bool IsBeingReprogrammed { get; private set; }

    /// <summary>ROM BMUT entry: the human stops walking and is rendered as the flashing two-colour shape.</summary>
    internal void BeginReprogramming() => IsBeingReprogrammed = true;

    /// <summary>ROM BMUT completion: the human's body is gone (PROGST makes the prog in its place).</summary>
    internal void FinishReprogramming()
    {
        IsBeingReprogrammed = false;
        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Test-only positioning hook (InternalsVisibleTo the test assembly).</summary>
    internal void TeleportTo(IntVector2 position) => _position = position;
}
