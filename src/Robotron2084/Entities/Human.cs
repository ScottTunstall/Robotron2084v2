using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>One of the family members the player is trying to rescue: Mikey, Mummy or Daddy.</summary>
/// <seealso cref="Brain"/>
/// <seealso cref="SkullMarker"/>
/// <remarks>ROM: RRH11.ASM's <c>HUMAN</c> process and its <c>HUMATB</c>
/// walk table. It walks one of 8 direction blocks (4 cardinal, 4 diagonal) of 4 substeps — 2-then-1
/// arcade px on the major axis, plus a steady 1px on the minor axis for diagonals — taking a new
/// random direction every 1-128 steps, or at once if the next step would leave the field or land on
/// a standing electrode. Its 12 frames are 4 directions x 3 walk frames, the diagonals reusing the
/// cardinal sets. Humans set off before the robots' wave-start flag. Timers count 5 per tick and 6
/// per arcade frame, so an interval of N frames is due at 6 x N.</remarks>
public sealed class Human : IEntity, IArtSource
{
    /// <summary>The step period in ROM frames. The ONE deliberate gameplay override — do not "fix" it.</summary>
    /// <remarks>The arcade steps every 8 frames and moves one arcade pixel; the port deliberately
    /// slows this to 16, because the ROM-accurate pace read as "the mommies are walking too fast" in
    /// playtesting. Do not change it back to 8 without asking (notes §70).</remarks>
    private const int StepPeriodRomTicks = 16;

    /// <summary>The walk table: 4 substeps for each of the 8 travel directions, in arcade pixels.</summary>
    /// <remarks>Copied from the ROM's <c>HUMATB</c> — each substep is a picture number with an X/Y
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

    /// <summary>Which 3-frame set each block animates from: [L,R,D,U,L,R,R,L].</summary>
    private static readonly int[] FrameSet = { 0, 1, 2, 3, 0, 1, 1, 0 };

    /// <summary>The member's collision box in arcade pixels, before scaling.</summary>
    private static (int Width, int Height) ArcadeCollisionSize(HumanKind kind) => kind switch
    {
        HumanKind.Mikey => GameplayConstants.MikeyCollisionSize,
        HumanKind.Mom => GameplayConstants.MomCollisionSize,
        _ => GameplayConstants.DadCollisionSize,
    };

    /// <summary>The largest side of this member's box in port pixels — the square the spawner keeps clear.</summary>
    /// <param name="kind">The member whose box is measured.</param>
    /// <returns>The square's side, in port pixels.</returns>
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
    private int _stepTimer;     // Counts up to the next step.
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

    /// <summary>Alive until killed, rescued or reprogrammed.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Steps taken so far — test hook for the step cadence.</summary>
    internal int StepCount { get; private set; }

    /// <summary>Killed: gone at once, with no death animation.</summary>
    /// <remarks>ROM: <c>DMAOFF</c>.</remarks>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>Rescued: gone at once.</summary>
    public void Rescue() => LifeState = EntityLifeState.Dead;

    /// <summary>Walks the current direction block one substep at a time, on the step clock.</summary>
    /// <param name="gameTime">Unused — the step period is counted in ticks.</param>
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
            // Counts up to the next step: 5 per tick, 6 per arcade frame.
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
            // Blocked by the wall or a standing electrode: pick a fresh direction instead.
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
    private void PickNewDirection()
    {
        _directionBlock = _random.Next(8);
        _subStep = 0;
        _frame = 3 * FrameSet[_directionBlock];
        _reDirStepsRemaining = 1 + _random.Next(128);
    }

    /// <summary>Draws the walk frame, or — while being reprogrammed — the flashing two-colour shape.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        Texture2D[] frames = FramesOf(sprites);

        if (IsBeingReprogrammed)
        {
            // Reprogrammed: a solid silhouette over a solid background, both cycling slots.
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

    /// <summary>The walk frame this human is showing — the art pixel-perfect collision compares.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    public Texture2D CurrentFrameArt(SpriteSet sprites) => FramesOf(sprites)[_frame];

    /// <summary>The three walk pictures this human's kind is drawn with (notes §49).</summary>
    private Texture2D[] FramesOf(SpriteSet sprites) => _kind switch
    {
        HumanKind.Mikey => sprites.MikeyFrames,
        HumanKind.Mom => sprites.MomFrames,
        _ => sprites.DadFrames,
    };

    /// <summary>True while this human is being reprogrammed: it cannot walk, be rescued or be killed.</summary>
    /// <remarks>ROM: <c>BMUT</c> — the human comes off the human list while the brain drives it.</remarks>
    public bool IsBeingReprogrammed { get; private set; }

    /// <summary>Starts being reprogrammed: the human stops walking and starts flashing.</summary>
    internal void BeginReprogramming() => IsBeingReprogrammed = true;

    /// <summary>Finishes reprogramming: the human is gone.</summary>
    /// <remarks>ROM: <c>PROGST</c>.</remarks>
    internal void FinishReprogramming()
    {
        IsBeingReprogrammed = false;
        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Test-only positioning hook (InternalsVisibleTo the test assembly).</summary>
    /// <param name="position">The position to move the human to.</param>
    internal void TeleportTo(IntVector2 position) => _position = position;
}
