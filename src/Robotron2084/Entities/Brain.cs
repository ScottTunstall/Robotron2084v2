using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A brain — a hovering robot that turns humans into progs and fires homing missiles.</summary>
/// <seealso cref="PlayField"/>
/// <seealso cref="Human"/>
/// <remarks>ROM: RRB10.ASM's <c>BRNORG</c>/<c>BRNKIL</c> (notes §18). Its beat is 1 + this wave's
/// <c>BRNSPD</c> frames: one 1-px step per axis, the walk animation advances and the missile timer
/// ticks (reload <c>BSHTIM</c>). X has a ±2 arcade px dead
/// zone, Y has none, so a brain on the target's row oscillates ±1 px. Each axis is wall-checked
/// separately, so a blocked brain slides along the wall instead of freezing. Facing follows the move
/// (X wins) and changing it restarts the walk pattern. It targets the nearest living human by
/// Manhattan distance, else the player. Timers count 5 per tick and 6 per arcade frame, so an
/// interval of N frames is due at 6 x N.</remarks>
public sealed class Brain : IEntity, IExplodable, IRemovable
{
    private readonly SpriteSet _sprites;    /// <summary>Extra ROM frames added to this wave's brain speed to get the beat.</summary>
    private const int BeatExecutionRomTicks = 1;

    /// <summary>How far the brain moves on each axis per step: one arcade px.</summary>
    private static readonly int StepPixels = ScreenSize.Scaled(1);

    /// <summary>X approach dead zone, in port pixels; there is no equivalent zone on Y.</summary>
    private static readonly int ApproachDeadZonePixels = ScreenSize.Scaled(2);

    /// <summary>The brain picture's own 14x16 arcade px box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.BrainCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.BrainCollisionSize.Height));

    /// <summary>The walk pattern's frame order: picture 1, 2, 1, 3 (an A-B-A-C cycle).</summary>
    /// <remarks>ROM: each direction's animation table (<c>BRNAL</c>/<c>BRNAR</c>/<c>BRNAD</c>/
    /// <c>BRNAU</c>) plays its 3 pictures in this order.</remarks>
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    /// <summary>The four directions' picture bases, in <see cref="SpriteSet.BrainFrames"/> order (left, right, down, up).</summary>
    private const int LeftDirectionBase = 0;
    private const int RightDirectionBase = 1;
    private const int DownDirectionBase = 2;
    private const int UpDirectionBase = 3;

    private readonly Random _random;
    private readonly int _beatPeriod; // how long between beats
    private readonly int _fireDelayRomTicks;
    private IntVector2 _position;
    private int _beatTimer; // counts up toward the next beat
    private int _directionBase = DownDirectionBase; // starts facing down, like a freshly spawned brain
    private int _frameStep;         // index 0..3 into the current direction's 4-frame walk pattern
    private int _fireBeatsRemaining; // beats left before the next missile
    private Human? _victim;                 // the human currently being reprogrammed, if any
    private int _reprogramRedrawsRemaining;
    private int _reprogramTimer;           // Counts up to the next lift/drop step
    private bool _reprogramLifting;         // next redraw lifts the human's Y (+), then drops it (-)

    /// <summary>Creates a brain; it takes its first step on its first beat.</summary>
    /// <param name="position">Top-left of the brain.</param>
    /// <param name="random">The random source: the fire timer and the reprogramming jitter.</param>
    /// <param name="brainSpeedRomTicks">How many ROM frames this wave's brain waits between beats.</param>
    /// <param name="fireDelayRomTicks">How many ROM frames this wave's brain waits between cruise missiles.</param>
    public Brain(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int brainSpeedRomTicks,
        int fireDelayRomTicks)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _fireDelayRomTicks = fireDelayRomTicks;
        _beatPeriod = (BeatExecutionRomTicks + brainSpeedRomTicks) * 6;
        _fireBeatsRemaining = 1 + random.Next(fireDelayRomTicks);
    }

    /// <summary>Top-left of the brain.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>The brain picture's own 14x16 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until shot; never Dying — there is no death animation.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Kills the brain outright: no death animation of any kind.</summary>
    /// <remarks>ROM: RRB10.ASM's <c>BRNKIL</c>. A brain killed mid-reprogram releases its victim.</remarks>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>Runs one beat: chase, step, animate and count the missile timer down.</summary>
    /// <param name="gameTime">Unused — the beat timer is counted in ticks.</param>
    /// <param name="field">The playfield.</param>
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

        // ROM BMUT: while reprogramming, the brain runs its redraw loop instead of the chase.
        if (_victim is { } victim)
        {
            AdvanceReprogramming(field, victim);
            return;
        }

        _beatTimer += 5;
        if (_beatTimer < _beatPeriod)
        {
            return;
        }

        _beatTimer -= _beatPeriod;

        AdvanceWalkAnimation(StepTowardTarget(field));

        if (--_fireBeatsRemaining <= 0)
        {
            FireIfPossible(field);
        }
    }

    /// <summary>Steps one pixel each way toward the target, where the playfield allows it.</summary>
    /// <param name="field">The playfield, whose bounds the step is kept inside.</param>
    /// <returns>The step it TRIED — the facing follows the intent, not whether a wall let it through.</returns>
    /// <remarks>ROM: <c>BRNL1</c>. X steps toward the target but stops short inside the dead zone; Y always
    /// steps, down when the target is level with it.</remarks>
    private IntVector2 StepTowardTarget(PlayField field)
    {
        // Nearest living human, else the player (ROM: GETHTG).
        IntVector2 target = field.NearestHumanPositionTo(_position) ?? field.Player.Position;

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

    /// <summary>Advances the walk pattern; the facing follows the step, and a change restarts the pattern.</summary>
    /// <param name="step">This beat's attempted step.</param>
    /// <remarks>ROM: <c>BRNDIR</c>/<c>BRNSD</c>.</remarks>
    private void AdvanceWalkAnimation(IntVector2 step)
    {
        int nextBase = step.X != 0
            ? (step.X > 0 ? RightDirectionBase : LeftDirectionBase)
            : (step.Y < 0 ? UpDirectionBase : DownDirectionBase);

        if (nextBase == _directionBase)
        {
            _frameStep = (_frameStep + 1) % WalkCycle.Length;
        }
        else
        {
            _directionBase = nextBase;
            _frameStep = 0;
        }
    }

    /// <summary>Fires a cruise missile at the brain's own fixed offset, then re-arms the fire timer.</summary>
    /// <param name="field">The playfield, which owns the missile cap and the new missile.</param>
    /// <remarks>The re-arm takes place even when the cap blocks the shot, so a held-up brain keeps its
    /// cadence (ROM: the brain's fire beat).</remarks>
    private void FireIfPossible(PlayField field)
    {
        if (field.CanFireCruiseMissile)
        {
            field.SpawnCruiseMissile(_position + new IntVector2(
                3 * ScreenSize.SpecScale, 4 * ScreenSize.SpecScale));
        }

        _fireBeatsRemaining = 1 + _random.Next(_fireDelayRomTicks);
    }

    /// <summary>True when the brain's picture box would still sit inside the playfield on X.</summary>
    private static bool FitsInsideX(Rectangle bounds, int x) =>
        x >= bounds.X && x + CollisionSize.Width <= bounds.Right;

    /// <summary>True when the brain's picture box would still sit inside the playfield on Y.</summary>
    private static bool FitsInsideY(Rectangle bounds, int y) =>
        y >= bounds.Y && y + CollisionSize.Height <= bounds.Bottom;

    /// <summary>True while this brain is reprogramming a human.</summary>
    /// <remarks>ROM: <c>BMUT</c>.</remarks>
    public bool IsReprogramming => _victim is not null;

    /// <summary>Gives up the current victim, if any — used when the brain is killed mid-animation.</summary>
    /// <returns>The victim this brain was reprogramming, or null if there was none.</returns>
    /// <remarks>ROM: <c>BRNKIL</c>.</remarks>
    internal Human? ReleaseVictim()
    {
        Human? victim = _victim;
        _victim = null;
        return victim;
    }

    /// <summary>Starts the reprogramming: sets the human beside the brain and begins the redraw loop.</summary>
    /// <param name="human">The victim, taken off the human list until the animation ends.</param>
    /// <param name="playfieldBounds">The playfield bounds, used to place the human.</param>
    /// <remarks>ROM: <c>BMUT</c> entry; the proximity test is <c>BRNL1</c>'s tail.</remarks>
    internal void BeginReprogramming(Human human, Rectangle playfieldBounds)
    {
        _victim = human;
        human.BeginReprogramming();
        _reprogramRedrawsRemaining = GameplayConstants.ReprogramIterations * GameplayConstants.ReprogramRedrawsPerIteration;
        _reprogramLifting = true;
        _reprogramTimer = GameplayConstants.ReprogramStepRomTicks * 6;

        // Placement and facing (ROM: BMUT00/BMUT10).
        int humanWidth = human.Bounds.Width;
        int x = _position.X - humanWidth - ScreenSize.Scaled(1);
        int facingBase = LeftDirectionBase;
        if (x < playfieldBounds.X)
        {
            x = _position.X + ScreenSize.Scaled(8);
            facingBase = RightDirectionBase;
            if (x >= playfieldBounds.Right - ScreenSize.Scaled(4))
            {
                x = _position.X - humanWidth - ScreenSize.Scaled(1);
                facingBase = LeftDirectionBase;
            }
        }

        _directionBase = facingBase;
        _frameStep = 0;

        human.TeleportTo(new IntVector2(x, _position.Y + ScreenSize.Scaled(2)));
    }

    /// <summary>Runs one reprogramming iteration: lift the human, then drop it, then count down.</summary>
    /// <remarks>The lift/drop amount is a random 0..7 pixels each time (ROM: <c>BMUTL</c>).</remarks>
    private void AdvanceReprogramming(PlayField field, Human victim)
    {
        _reprogramTimer += 5;
        if (_reprogramTimer < GameplayConstants.ReprogramStepRomTicks * 6)
        {
            return;
        }

        _reprogramTimer -= GameplayConstants.ReprogramStepRomTicks * 6;

        Rectangle bounds = field.Wall.PlayfieldBounds;
        int jitter = _random.Next(GameplayConstants.ReprogramJitterPixels);
        int height = victim.Bounds.Height;
        int y = _reprogramLifting
            ? Math.Min(victim.Position.Y + jitter, bounds.Bottom - height)
            : Math.Max(victim.Position.Y - jitter, bounds.Y);
        victim.TeleportTo(new IntVector2(victim.Position.X, y));

        if (_reprogramLifting)
        {
            // The sound is requested at the top of every iteration (ROM: BMUTL).
            Sound.Play(SoundTables.ProgProgramming);
        }

        if (--_reprogramRedrawsRemaining > 0)
        {
            _reprogramLifting = !_reprogramLifting;
            return;
        }

        // The tail: conversion sound, free the human, spawn a PROG where it stood (ROM: PROGST).
        Sound.Play(SoundTables.HumanProgConversion);
        victim.FinishReprogramming();
        field.SpawnProg(victim.Position, victim.Kind);
        _victim = null;
    }

    /// <summary>The current walk frame, as an index into <see cref="SpriteSet.BrainFrames"/>.</summary>
    internal int WalkFrameIndex => _directionBase * 3 + WalkCycle[_frameStep];

    /// <summary>Draws the current walk frame — over a solid block while it is reprogramming.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        if (IsReprogramming)
        {
            // A solid block under the picture (ROM: DRAW_BRAIN_IN_PROGGING_STATE).
            _sprites.DrawSolidRectangle(spriteBatch, Bounds, _sprites.SlotColor(GameplayConstants.ReprogramShapeSlot));
        }

        _sprites.DrawSprite(spriteBatch, CurrentAnimationFrame, Bounds, Color.White);
    }

    /// <summary>The frame an explosion would copy (see <see cref="IArtSource"/>).</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <returns>The texture for the current walk frame.</returns>
    public Texture2D CurrentAnimationFrame
        => _sprites.BrainFrames[WalkFrameIndex];
}
