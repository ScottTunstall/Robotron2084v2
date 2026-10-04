using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The player is the hero the person plays as: it moves around, shoots at enemies, and can be killed and brought back to try again.</summary>
/// <seealso cref="PlayField"/>
/// <seealso cref="PlayerLaser"/>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRF.ASM</c>, routine <c>PLAYER</c> (called every interrupt; see also <c>PDEATH</c> and RRG23.ASM's <c>LTAB</c> muzzle-offset table)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$2FD0</c> (<c>MOVE_PLAYER</c>)</item>
/// </list>
/// </remarks>
public sealed class Player : IEntity, IAnimationFrameSource
{
    /// <summary>How many movement ticks each animation frame is shown for. <see cref="_animationFrameTicks"/> counts up to this and then the next animation frame is shown.</summary>
    private const int FrameTicksPerAnimationFrame = 3;

    /// <summary>Collision box = the player sprite's own 8x12 arcade px.</summary>
    /// <remarks>The ROM collides against the player's sprite, not a fixed 16x16 cell.</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.PlayerCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.PlayerCollisionSize.Height));

    /// <summary>Walk cycle per direction: [f0, f1, f0, f2] (e.g. left = 1,2,1,3).</summary>
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    private readonly TimeSpan _graceDuration = TimeSpan.FromSeconds(PlayerTuning.PlayerStartGraceSeconds);
    private readonly Random _random;
    private readonly SpriteSet _sprites;

    // A wave starts on frame 7, the first DOWN frame; _animationFrameTicks counts 1..3.
    private WalkFacing _animationFacing = GetWalkFacing(Direction8.Down);

    private int _animationFrameTicks = 1;
    private int _animationSequenceIndex;
    private int _autoFireTicksRemaining;
    private int _deathFadeIndex;
    private int _deathFlashIterationsRemaining = PlayerTuning.PlayerDeathFlashIterations;
    private int _deathFlashSlot = PlayerTuning.PlayerDeathWhiteSlot;

    // Death: a solid-colour flash loop, then the slot-12 fade (ROM: RRX7.ASM; see the remarks).
    private DeathStage _deathStage = DeathStage.White;

    private int _deathTimer;
    private TimeSpan _graceRemaining;
    private int _invincibilityBlinkTicks;
    private int _invincibilityTicksRemaining;
    private IntVector2 _position;
    private bool _wasFiring;

    /// <summary>Spawns the player at <paramref name="startPosition"/> with <paramref name="lives"/> men and the start grace running.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="startPosition">Top-left of the player.</param>
    /// <param name="lives">How many men the player starts with; a death takes one off.</param>
    /// <param name="random">The random source for the death animation's colour, or null to create one.</param>
    public Player(SpriteSet sprites, IntVector2 startPosition, int lives, Random? random = null)
    {
        _sprites = sprites;
        _position = startPosition;
        _random = random ?? new Random();
        Lives = lives;
        _graceRemaining = _graceDuration;
        IsInStartGracePeriod = true;
    }

    /// <summary>The death animation's stages: a white flash, a colour flash, then the fade to black.</summary>
    /// <remarks>The ROM's death stages: a fixed white, then a random colour, then the slot-12 fade.</remarks>
    private enum DeathStage
    {
        White,
        Colour,
        Fade,
    }

    /// <summary>The player sprite's own 8x12 box at <see cref="Position"/> (the ROM intersects the sprite).</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>The walk frame this player is showing — a dying player is the same shape, drawn as a solid colour.</summary>
    public Texture2D GetCurrentAnimationFrame() => _sprites.PlayerAnimationFrames[WalkAnimationFrameIndex];

    /// <summary>The way the player is drawn and walks — his last MOVEMENT direction.</summary>
    public Direction8 FacingDirection { get; private set; } = Direction8.Up;

    /// <summary>TEMPORARY playtest aid: while set, <see cref="Kill"/> is a complete no-op.</summary>
    /// <remarks>Per player, so the attract DEMO can opt out and the machine still plays by the
    /// arcade's rules (otherwise every contact path in the demo is dead code).</remarks>
    public bool InvincibleForTesting { get; set; } = PlayerTuning.PlayerInvincibleForTesting;

    /// <summary>True until the start grace expires.</summary>
    public bool IsInStartGracePeriod { get; private set; }

    /// <summary>Passes through hazards unharmed for a short time after a respawn.</summary>
    public bool IsInvincible => _invincibilityTicksRemaining > 0;

    /// <summary>True when this update fired a laser.</summary>
    public bool LasersFiredThisUpdate { get; private set; }

    /// <summary>Alive, Dying (the death animation's flash and fade), or Dead pending respawn.</summary>
    /// <remarks>The ROM's `PDTHV` flash and fade.</remarks>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Men remaining, including the one on screen; a death takes one off.</summary>
    public int Lives { get; private set; }

    /// <summary>Top-left of the player (the ROM's OBJX/OBJY).</summary>
    public IntVector2 Position => _position;

    /// <summary>The palette slot the dying player is drawn solid in.</summary>
    /// <remarks>The ROM's death colour: a fixed white slot, a random colour slot, or the fading slot.</remarks>
    internal int DeathSolidSlot => _deathStage == DeathStage.Fade
        ? PlayerTuning.PlayerDeathFadeSlot
        : _deathFlashSlot;

    /// <summary>0-based index into <see cref="SpriteSet.PlayerAnimationFrames"/>.</summary>
    /// <remarks>The arcade numbers its frames 1 through 12, so arcade frame N is index N - 1.</remarks>
    internal int WalkAnimationFrameIndex => (int)_animationFacing * 3 + WalkCycle[_animationSequenceIndex];

    /// <summary>Awards an extra life when a score crosses an extra-life threshold.</summary>
    public void AddLife() => Lives += 1;

    /// <summary>Draws the walk frame, or a one-colour silhouette while dying.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (this.IsDead())
        {
            return;
        }

        // While invincible: visible/hidden tick-toggle flicker (no alpha blending).
        if (IsInvincible && _invincibilityBlinkTicks >= PlayerTuning.InvincibilityFlickerVisibleTicks)
        {
            return;
        }

        // One colour while dying, like the ROM's own solid-colour draw.
        if (this.IsDying())
        {
            _sprites.Blitter.DrawSpriteSolid(spriteBatch, GetCurrentAnimationFrame(), Bounds, _sprites.Blitter.GetSlotColour(DeathSolidSlot));
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), Bounds, Color.White);
    }

    /// <summary>Kills the player (contact with a live hazard). No-op while dying/dead.</summary>
    public void Kill()
    {
        if (InvincibleForTesting)
        {
            return; // TEMPORARY playtest aid — see the property and the constant.
        }

        if (!this.IsAlive())
        {
            return;
        }

        StartDeath();
    }

    /// <summary>Respawn for "RESTART THE CURRENT LEVEL" (lives remaining).</summary>
    /// <param name="startPosition">Where to place the player.</param>
    public void ResetForLevelRestart(IntVector2 startPosition)
    {
        _position = startPosition;
        LifeState = EntityLifeState.Alive;
        ResetDeathAnimation();
        _graceRemaining = _graceDuration;
        IsInStartGracePeriod = true;
        _invincibilityTicksRemaining = PlayerTuning.PlayerInvincibilityTicks;
        _wasFiring = false;
        _autoFireTicksRemaining = PlayerTuning.PlayerAutoFireTicks;
    }

    /// <summary>One tick: the death animation while dying, else clocks, movement, firing and animation.</summary>
    /// <param name="gameTime">The elapsed time, used to run the start grace down.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (this.IsDead())
        {
            return;
        }

        if (this.IsDying())
        {
            // Death animation: no movement.
            AdvanceDeath(field);
            return;
        }

        AdvanceInvincibility(gameTime);

        PlayerInputState input = field.Input.Poll();
        IntVector2 move = input.MoveDirection;

        // The aim drives the FIRE direction only — never the facing or the animation.
        Direction8? aim = Direction8Extensions.CreateFromDelta(input.ShootDirection);

        MoveFromInput(move, field);
        UpdateFiring(input, aim, field);
        AdvanceWalkAnimation(move);
    }

    /// <summary>Which walk sequence a facing uses: diagonals reuse the horizontal sequences.</summary>
    /// <param name="direction">The facing to map.</param>
    /// <returns>The walk sequence the facing uses.</returns>
    /// <remarks>The arcade's own stick-to-walk-sequence rule.</remarks>
    internal static WalkFacing GetWalkFacing(Direction8 direction) => direction switch
    {
        Direction8.Left or Direction8.UpLeft or Direction8.DownLeft => WalkFacing.Left,
        Direction8.Right or Direction8.UpRight or Direction8.DownRight => WalkFacing.Right,
        Direction8.Down => WalkFacing.Down,
        _ => WalkFacing.Up,
    };

    /// <summary>Starts the death animation, ignoring <c>PlayerInvincibleForTesting</c> (test hook).</summary>
    internal void StartDeathForTesting()
    {
        if (this.IsAlive())
        {
            StartDeath();
        }
    }

    /// <summary>Test-only positioning hook (InternalsVisibleTo the test assembly).</summary>
    internal void TeleportTo(IntVector2 position) => _position = position;

    /// <summary>Where a shot of this facing leaves the player: spec px from the player cell's top-left.</summary>
    /// <param name="direction">The direction the shot is fired in.</param>
    /// <returns>The muzzle's offset from the player's top-left, in port pixels.</returns>
    /// <remarks>ROM: the muzzle-offset table (RRG23.ASM).</remarks>
    private static IntVector2 GetMuzzleOffset(Direction8 direction)
    {
        (int x, int y) offset = direction switch
        {
            Direction8.Up => (2, -1),
            Direction8.Down => (2, 4),
            Direction8.Left => (0, 4),
            Direction8.Right => (2, 4),
            Direction8.UpLeft => (0, 0),
            Direction8.DownLeft => (0, 8),
            Direction8.UpRight => (2, 0),
            _ => (2, 12), // DownRight
        };
        return new(ScreenSize.ToPortPixels(offset.x), ScreenSize.ToPortPixels(offset.y));
    }

    /// <summary>Steps along one axis, unless the player's box would then overlap the wall.</summary>
    /// <param name="from">The position to step from.</param>
    /// <param name="delta">The step, on one axis only.</param>
    /// <param name="wall">The wall the player must stay clear of.</param>
    /// <returns>The stepped position, or <paramref name="from"/> when the wall blocks it.</returns>
    private static IntVector2 StepAxis(IntVector2 from, IntVector2 delta, PlayfieldWall wall)
    {
        IntVector2 stepped = from + delta;
        Rectangle box = new(stepped.X, stepped.Y, CollisionSize.Width, CollisionSize.Height);
        return wall.Intersects(box) ? from : stepped;
    }

    /// <summary>One tick of the death animation: the flash loop, then the fade.</summary>
    /// <param name="field">The playfield, whose palette runs the fade.</param>
    /// <remarks>ROM: RRX7.ASM's death routine. The fade takes over palette slot 12 and suspends that
    /// slot's own colour-cycling ("decay") process while it runs, then resumes it.</remarks>
    private void AdvanceDeath(PlayField field)
    {
        _deathTimer += ArcadeClock.UnitsPerPortTick;

        int romFrames = _deathStage switch
        {
            DeathStage.White => PlayerTuning.PlayerDeathWhiteRomFrames,
            DeathStage.Colour => PlayerTuning.PlayerDeathColourRomFrames,
            _ => PlayerTuning.PlayerDeathFadeRomFrames,
        };

        if (_deathTimer < ArcadeClock.ToClockUnits(romFrames))
        {
            return;
        }

        _deathTimer -= ArcadeClock.ToClockUnits(romFrames);

        switch (_deathStage)
        {
            case DeathStage.White:
                // After the white flash, pick a random colour slot.
                _deathFlashSlot = PlayerTuning.PlayerDeathFlashSlots[_random.Next(PlayerTuning.PlayerDeathFlashSlots.Length)];
                _deathStage = DeathStage.Colour;
                break;

            case DeathStage.Colour:
                if (--_deathFlashIterationsRemaining <= 0)
                {
                    BeginDeathFade(field);
                }
                else
                {
                    _deathStage = DeathStage.White;
                }

                break;

            default:
                // Write the next fade byte, 4 frames apart; the last (black) ends the death.
                _deathFadeIndex++;
                WriteDeathFade(field);
                if (_deathFadeIndex >= PlayerTuning.PlayerDeathFadeValues.Length - 1)
                {
                    field.Palette?.ResumeSlot(PlayerTuning.PlayerDeathFadeSlot);
                    LifeState = EntityLifeState.Dead;
                }

                break;
        }
    }

    /// <summary>Runs one tick of the invincibility countdown, its flicker, and the start-of-life grace.</summary>
    /// <param name="gameTime">The elapsed time, used to run the start grace down.</param>
    private void AdvanceInvincibility(GameTime gameTime)
    {
        if (_invincibilityTicksRemaining > 0)
        {
            _invincibilityTicksRemaining--;
        }

        _invincibilityBlinkTicks =
            (_invincibilityBlinkTicks + 1) % (PlayerTuning.InvincibilityFlickerVisibleTicks + PlayerTuning.InvincibilityFlickerHiddenTicks);

        if (IsInStartGracePeriod)
        {
            _graceRemaining -= gameTime.ElapsedGameTime;
            if (_graceRemaining <= TimeSpan.Zero)
            {
                IsInStartGracePeriod = false;
            }
        }
    }

    /// <summary>Advances the walk cycle; with the stick centred the frame holds where it is.</summary>
    /// <param name="move">The stick's per-axis direction, -1 / 0 / +1.</param>
    private void AdvanceWalkAnimation(IntVector2 move)
    {
        if (move == IntVector2.Zero)
        {
            return;
        }

        WalkFacing facing = GetWalkFacing(FacingDirection);
        if (facing != _animationFacing)
        {
            // A new direction resets the sequence index and the frame hold, so its first
            // frame shows immediately.
            _animationFacing = facing;
            _animationSequenceIndex = 0;
            _animationFrameTicks = 1;
        }
        else if (_animationFrameTicks >= FrameTicksPerAnimationFrame)
        {
            _animationFrameTicks = 1;
            _animationSequenceIndex = (_animationSequenceIndex + 1) % WalkCycle.Length;
        }
        else
        {
            _animationFrameTicks++;
        }
    }

    /// <summary>Starts the fade: suspends the slot-12 decay and writes the first byte.</summary>
    /// <param name="field">The playfield, whose palette runs the fade.</param>
    /// <remarks>ROM: the colour processes carry on as normal; only the decay suspends.</remarks>
    private void BeginDeathFade(PlayField field)
    {
        _deathStage = DeathStage.Fade;
        _deathFadeIndex = 0;
        field.Palette?.SuspendSlot(PlayerTuning.PlayerDeathFadeSlot);
        WriteDeathFade(field);
    }

    /// <summary>Applies the stick for one tick: the facing follows it, and the wall bounds the move.</summary>
    /// <param name="move">The stick's per-axis direction, -1 / 0 / +1.</param>
    /// <param name="field">The playfield, whose wall bounds the move.</param>
    private void MoveFromInput(IntVector2 move, PlayField field)
    {
        if (move == IntVector2.Zero)
        {
            return;
        }

        FacingDirection = Direction8Extensions.CreateFromDelta(move)!.Value;

        // Per-axis move with wall revert: never allowed to overlap the wall.
        IntVector2 candidate = _position;
        int dx = move.X * PlayerTuning.PlayerSpeedX;
        if (dx != 0)
        {
            candidate = StepAxis(candidate, new IntVector2(dx, 0), field.Wall);
        }

        int dy = move.Y * PlayerTuning.PlayerSpeedY;
        if (dy != 0)
        {
            candidate = StepAxis(candidate, new IntVector2(0, dy), field.Wall);
        }

        _position = candidate;
    }

    /// <summary>Puts the death animation back at its first white flash.</summary>
    private void ResetDeathAnimation()
    {
        _deathStage = DeathStage.White;
        _deathTimer = 0;
        _deathFlashIterationsRemaining = PlayerTuning.PlayerDeathFlashIterations;
        _deathFlashSlot = PlayerTuning.PlayerDeathWhiteSlot;
        _deathFadeIndex = 0;
    }

    /// <summary>The death state machine's setup, shared by <see cref="Kill"/> and the test hook.</summary>
    private void StartDeath()
    {
        LifeState = EntityLifeState.Dying;
        ResetDeathAnimation();
        Lives -= 1;
    }

    /// <summary>Fires on the fire control: a fresh press at once, a held one on the auto-fire cadence.</summary>
    /// <param name="input">This tick's controls.</param>
    /// <param name="aim">The aim stick's direction, or null when it is centred.</param>
    /// <param name="field">The playfield, which owns the laser slots.</param>
    /// <remarks>A shot re-fires every <see cref="PlayerTuning.PlayerAutoFireTicks"/> ticks; the attempt
    /// is a no-op when the three slots are full.</remarks>
    private void UpdateFiring(PlayerInputState input, Direction8? aim, PlayField field)
    {
        bool fire = input.FireHeld;
        if (fire && (!_wasFiring || --_autoFireTicksRemaining <= 0))
        {
            Direction8 fireDirection = aim ?? FacingDirection;
            IntVector2 muzzle = _position + GetMuzzleOffset(fireDirection);
            LasersFiredThisUpdate = field.TryFirePlayerLaser(muzzle, fireDirection);
            _autoFireTicksRemaining = PlayerTuning.PlayerAutoFireTicks;
        }
        else
        {
            LasersFiredThisUpdate = false;
        }

        _wasFiring = fire;
    }

    /// <summary>Writes the next fade byte into the death colour's palette slot.</summary>
    /// <param name="field">The playfield, whose palette holds the death slot.</param>
    private void WriteDeathFade(PlayField field) =>
        field.Palette?.SetSlot(PlayerTuning.PlayerDeathFadeSlot, PlayerTuning.PlayerDeathFadeValues[_deathFadeIndex]);
}
