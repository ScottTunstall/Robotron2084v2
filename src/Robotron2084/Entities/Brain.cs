using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A brain is a hovering robot that catches humans and turns them into progs. It also fires homing missiles at you.</summary>
/// <seealso cref="PlayField"/>
/// <seealso cref="Human"/>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRB10.ASM</c>, routine <c>BRAIN</c> (with <c>BRNKIL</c> sub-block)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>BRAIN_AI</c> (<c>$2523</c>) and <c>ANIMATE_BRAIN</c> (<c>$2539</c>)</item>
/// </list>
/// </remarks>
public sealed class Brain : IEntity, IExplodable, IRemovable
{
    /// <summary>Extra ROM frames added to this wave's brain speed to get the beat.</summary>
    private const int BeatExecutionRomFrames = 1;

    /// <summary>The cruise missile leaves the brain this many arcade px right of its corner...</summary>
    private const int MissileMuzzleXArcadePixels = 3;

    /// <summary>...and this many below it.</summary>
    private const int MissileMuzzleYArcadePixels = 4;

    /// <summary>ROM <c>BMUT00</c>: the victim is set this many arcade px below the brain's corner.</summary>
    private const int VictimDropArcadePixels = 2;

    /// <summary>ROM <c>BMUT00</c>: the victim is set this many arcade px clear of the brain's left side...</summary>
    private const int VictimGapArcadePixels = 1;

    /// <summary>ROM <c>BMUT10</c>: ...or, at the left wall, this many px right of the brain's corner...</summary>
    private const int VictimRightOffsetArcadePixels = 8;

    /// <summary>ROM <c>BMUT10</c>: ...unless that would come within this many px of the right wall.</summary>
    private const int VictimRightWallMarginArcadePixels = 4;

    /// <summary>X approach dead zone, in port pixels; there is no equivalent zone on Y.</summary>
    private static readonly int ApproachDeadZonePixels = ScreenSize.ToPortPixels(2);

    /// <summary>The brain sprite's own 14x16 arcade px box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.BrainCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.BrainCollisionSize.Height));

    /// <summary>How far the brain moves on each axis per step: one arcade px.</summary>
    private static readonly int StepPixels = ScreenSize.ToPortPixels(1);

    /// <summary>The walk pattern's frame order: animation frame 1, 2, 1, 3 (an A-B-A-C cycle).</summary>
    /// <remarks>ROM: each direction's animation table (<c>BRNAL</c>/<c>BRNAR</c>/<c>BRNAD</c>/
    /// <c>BRNAU</c>) plays its 3 animation frames in this order.</remarks>
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    private readonly int _beatPeriod;

    // how long between beats
    private readonly int _fireIntervalBeats;

    private readonly Random _random;
    private readonly SpriteSet _sprites;
    private int _beatTimer;

    // counts up toward the next beat
    private WalkFacing _facing = WalkFacing.Down;

    private int _fireBeatsRemaining;
    private IntVector2 _position;
    private bool _reprogramLifting;

    private int _reprogramRedrawsRemaining;

    private int _reprogramTimer;

    // beats left before the next missile
    private int _targetSlot;

    // the family-list slot this brain chases (ROM PD+9)
    private Human? _victim;

    // starts facing down, like a freshly spawned brain
    private int _walkCycleStep;         // index 0..3 into the current direction's 4-frame walk pattern

    // the human currently being reprogrammed, if any
    // Counts up to the next lift/drop step
    // next redraw lifts the human's Y (+), then drops it (-)

    /// <summary>Creates a brain; it takes its first step on its first beat.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the brain.</param>
    /// <param name="random">The random source: the fire timer and the reprogramming jitter.</param>
    /// <param name="beatDelayRomFrames">This wave's beat delay in ROM frames, added to the base beat (ROM <c>BRNSPD</c>): a bigger number is a SLOWER brain.</param>
    /// <param name="fireIntervalBeats">The most beats this wave's brain waits between cruise missiles: the interval is a random 1..this.</param>
    /// <param name="targetFamilySlot">The family slot this brain chases. The field hands the brain the
    /// slot <see cref="PlayField.GetNearestFamilySlot"/> returns AS IT IS CREATED, which is before the
    /// family exists — the ROM's own order, and the arcade's "all the brains chase Mikey" bug
    /// (notes §18.8).</param>
    public Brain(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int beatDelayRomFrames,
        int fireIntervalBeats,
        int targetFamilySlot)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _fireIntervalBeats = fireIntervalBeats;
        _targetSlot = targetFamilySlot;
        _beatPeriod = ArcadeClock.ToClockUnits(BeatExecutionRomFrames + beatDelayRomFrames);
        _fireBeatsRemaining = 1 + random.Next(fireIntervalBeats);
    }

    /// <summary>The brain sprite's own 14x16 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>The frame an explosion would copy (see <see cref="IAnimationFrameSource"/>).</summary>
    /// <returns>The texture for the current walk frame.</returns>
    public Texture2D CurrentAnimationFrame
        => _sprites.BrainAnimationFrames[WalkAnimationFrameIndex];

    /// <summary>True while this brain is reprogramming a human.</summary>
    /// <remarks>ROM: <c>BMUT</c>.</remarks>
    public bool IsReprogramming => _victim is not null;

    /// <summary>Alive until shot; never Dying — there is no death animation.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Top-left of the brain.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>The family member this brain is chasing, or null while it is hunting the player.</summary>
    /// <remarks>ROM: the object the brain's AI resolved into <c>Y</c> — null until its first beat, since
    /// the catch test lives inside that beat (notes §18.8).</remarks>
    internal Human? Target { get; private set; }

    /// <summary>The family-list slot this brain chases (ROM PD+9, the brain's own target field).</summary>
    internal int TargetFamilySlot => _targetSlot;

    /// <summary>The current walk frame, as an index into <see cref="SpriteSet.BrainAnimationFrames"/>.</summary>
    internal int WalkAnimationFrameIndex => (int)_facing * 3 + WalkCycle[_walkCycleStep];

    /// <summary>Draws the current walk frame — over a solid block while it is reprogramming.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        if (IsReprogramming)
        {
            // A solid block under the sprite (ROM: DRAW_BRAIN_IN_PROGGING_STATE).
            _sprites.Blitter.DrawSolidRectangle(spriteBatch, Bounds, _sprites.Blitter.GetSlotColour(ReprogramTuning.ShapeSlot));
        }

        _sprites.Blitter.DrawSprite(spriteBatch, CurrentAnimationFrame, Bounds, Color.White);
    }

    /// <summary>Kills the brain outright: no death animation of any kind.</summary>
    /// <remarks>ROM: RRB10.ASM's <c>BRNKIL</c>. A brain killed mid-reprogram releases its victim: the field's
    /// own human phase does that (see <see cref="PlayField.ReleaseVictimsOfDeadBrains"/>).</remarks>
    public void Kill()
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Runs one beat: chase, step, animate and count the missile timer down.</summary>
    /// <param name="gameTime">Unused — the beat timer is counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        if (field.RobotsFrozen)
        {
            return;
        }

        // ROM BMUT: while reprogramming, the brain runs its redraw loop instead of the chase.
        if (_victim is { } victim)
        {
            AdvanceReprogramming(field, victim);
            return;
        }

        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < _beatPeriod)
        {
            return;
        }

        _beatTimer -= _beatPeriod;

        // ROM BRAIN_AI: the target is re-read at the top of every body — only an empty slot sends the brain
        // to the player, or to a fresh search while the family is still about.
        Target = ResolveTarget(field);
        AdvanceWalkAnimation(StepTowardTarget(field, Target?.Position ?? field.Player.Position));

        if (--_fireBeatsRemaining <= 0)
        {
            FireIfPossible(field);
        }
    }

    /// <summary>Starts the reprogramming: sets the human beside the brain and begins the redraw loop.</summary>
    /// <param name="human">The victim, taken off the human list until the animation ends.</param>
    /// <param name="playfieldBounds">The playfield bounds, used to place the human.</param>
    /// <remarks>ROM: <c>BMUT</c> entry; the proximity test is <c>BRNL1</c>'s tail.</remarks>
    internal void BeginReprogramming(Human human, Rectangle playfieldBounds)
    {
        _victim = human;
        human.BeginReprogramming();
        _reprogramRedrawsRemaining = ReprogramTuning.Iterations * ReprogramTuning.RedrawsPerIteration;
        _reprogramLifting = true;
        _reprogramTimer = ArcadeClock.ToClockUnits(ReprogramTuning.StepRomFrames);

        // Placement and facing (ROM: BMUT00/BMUT10).
        int humanWidth = human.Bounds.Width;
        int x = _position.X - humanWidth - ScreenSize.ToPortPixels(VictimGapArcadePixels);
        WalkFacing facing = WalkFacing.Left;
        if (x < playfieldBounds.X)
        {
            x = _position.X + ScreenSize.ToPortPixels(VictimRightOffsetArcadePixels);
            facing = WalkFacing.Right;
            if (x >= playfieldBounds.Right - ScreenSize.ToPortPixels(VictimRightWallMarginArcadePixels))
            {
                x = _position.X - humanWidth - ScreenSize.ToPortPixels(VictimGapArcadePixels);
                facing = WalkFacing.Left;
            }
        }

        _facing = facing;
        _walkCycleStep = 0;

        human.MoveTo(new IntVector2(x, _position.Y + ScreenSize.ToPortPixels(VictimDropArcadePixels)));
    }

    /// <summary>Gives up the current victim, if any — used when the brain is killed mid-animation.</summary>
    /// <returns>The victim this brain was reprogramming, or null if there was none.</returns>
    /// <remarks>ROM: <c>BRNKIL</c>.</remarks>
    internal Human? ReleaseVictim()
    {
        Human? victim = _victim;
        _victim = null;
        return victim;
    }

    /// <summary>True when the brain's sprite box would still sit inside the playfield on X.</summary>
    private static bool FitsInsideX(Rectangle bounds, int x) =>
        x >= bounds.X && x + CollisionSize.Width <= bounds.Right;

    /// <summary>True when the brain's sprite box would still sit inside the playfield on Y.</summary>
    private static bool FitsInsideY(Rectangle bounds, int y) =>
        y >= bounds.Y && y + CollisionSize.Height <= bounds.Bottom;

    /// <summary>Runs one reprogramming iteration: lift the human, then drop it, then count down.</summary>
    /// <remarks>The lift/drop amount is a random 0..7 pixels each time (ROM: <c>BMUTL</c>).</remarks>
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
        int y = _reprogramLifting
            ? Math.Min(victim.Position.Y + jitter, bounds.Bottom - height)
            : Math.Max(victim.Position.Y - jitter, bounds.Y);
        victim.MoveTo(new IntVector2(victim.Position.X, y));

        if (_reprogramLifting)
        {
            // The sound is requested at the top of every iteration (ROM: BMUTL).
            field.PlaySoundFrom(SoundTables.Programming, victim.Bounds);
        }

        if (--_reprogramRedrawsRemaining > 0)
        {
            _reprogramLifting = !_reprogramLifting;
            return;
        }

        // The tail: conversion sound, free the human, spawn a PROG where it stood (ROM: PROGST).
        field.PlaySoundFrom(SoundTables.HumanProgFinalConversion, victim.Bounds);
        victim.FinishReprogramming();
        field.SpawnProg(victim.Position, victim.Kind);
        _victim = null;
    }

    /// <summary>Advances the walk pattern; the facing follows the step, and a change restarts the pattern.</summary>
    /// <param name="step">This beat's attempted step.</param>
    /// <remarks>ROM: <c>BRNDIR</c>/<c>BRNSD</c>.</remarks>
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

    /// <summary>Fires a cruise missile at the brain's own fixed offset, then re-arms the fire timer.</summary>
    /// <param name="field">The playfield, which owns the missile cap and the new missile.</param>
    /// <remarks>The re-arm takes place even when the cap blocks the shot, so a held-up brain keeps its
    /// cadence (ROM: the brain's fire beat).</remarks>
    private void FireIfPossible(PlayField field)
    {
        if (field.CanFireCruiseMissile())
        {
            field.SpawnCruiseMissile(_position + new IntVector2(
                ScreenSize.ToPortPixels(MissileMuzzleXArcadePixels),
                ScreenSize.ToPortPixels(MissileMuzzleYArcadePixels)));
        }

        _fireBeatsRemaining = 1 + _random.Next(_fireIntervalBeats);
    }

    /// <summary>
    /// The member in this brain's target slot, or null when the brain should hunt the player.
    /// </summary>
    /// <param name="field">The playfield, which owns the family list.</param>
    /// <returns>The member to chase, or null for the player.</returns>
    /// <remarks>ROM: <c>BRAIN_AI</c> dereferences its stored slot pointer; a NULL slot sends the brain to the
    /// player object, and if any member of the family is still alive it searches for a new slot on the spot
    /// and follows that. The slot is what makes every brain on a brain wave converge on Mikey
    /// (notes §18.8).</remarks>
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

    /// <summary>Steps one pixel each way toward <paramref name="target"/>, where the playfield allows it.</summary>
    /// <param name="field">The playfield, whose bounds the step is kept inside.</param>
    /// <param name="target">The position being closed on — the brain's family target, or the player.</param>
    /// <returns>The step it TRIED — the facing follows the intent, not whether a wall let it through.</returns>
    /// <remarks>ROM: <c>BRNL1</c>. X steps toward the target but stops short inside the dead zone; Y always
    /// steps, down when the target is level with it.</remarks>
    private IntVector2 StepTowardTarget(PlayField field, IntVector2 target)
    {
        int dx = 0;
        int targetDx = target.X - _position.X;
        if (Math.Abs(targetDx) > ApproachDeadZonePixels)
        {
            dx = Math.Sign(targetDx) * StepPixels;
        }

        int dy = target.Y >= _position.Y ? StepPixels : -StepPixels;

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
