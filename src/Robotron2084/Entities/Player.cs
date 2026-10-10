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
/// It has no beat and acts on every tick. The <see cref="PlayField"/> calls <see cref="Update"/> directly, on
/// nearly every tick, in <see cref="PlayField.Update"/>. <see cref="_deathTimer"/> times the stages of its death
/// (see <see cref="ArcadeClock"/>), <see cref="_animationFrameTicks"/> counts the ticks each animation frame is
/// shown, and <see cref="_invincibilityBlinkTicks"/> counts the ticks of its blinking.
///
/// <list type="bullet">
/// <item>Original source: <c>RRF.ASM</c>, routine <c>PLAYER</c> (called every interrupt; see also
/// <c>PDEATH</c> and RRG23.ASM's <c>LTAB</c> muzzle-offset table)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$2FD0</c> (<c>MOVE_PLAYER</c>)</item>
/// </list>
/// </remarks>
public sealed class Player : IEntity, IAnimationFrameSource
{
    /// <summary>How many movement ticks each animation frame is shown for. <see cref="_animationFrameTicks"/> counts up to this and then the next animation frame is shown.</summary>
    private const int FrameTicksPerAnimationFrame = 3;

    /// <summary>Collision box = the player sprite's own 8x12 arcade px.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRF.ASM</c> <c>PLAYER</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>MOVE_PLAYER</c> (<c>$2FD0</c>).</item>
    /// </list>
    ///
    /// The arcade collides against the player's sprite, not a fixed
    /// 16x16 cell.
    /// </remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.PlayerCollisionSize.Width),
            ScreenSize.ToPortPixels(CollisionSizes.PlayerCollisionSize.Height));

    /// <summary>Walk cycle per direction: [f0, f1, f0, f2] (e.g. left = 1,2,1,3).</summary>
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    private readonly Random _random;
    private readonly SpriteSet _sprites;

    // A wave starts on frame 7, the first DOWN frame. _animationFrameTicks counts from 1 to 3 while a frame is shown.
    private WalkSequence _walkSequence = GetWalkSequence(Direction8.Down);

    private int _animationFrameTicks = 1;
    private int _walkCycleStep;
    private int _autoFireTicksRemaining;
    private int _deathFadeIndex;
    private int _deathFlashIterationsRemaining = PlayerTuning.PlayerDeathFlashIterations;
    private int _deathFlashSlot = PlayerTuning.PlayerDeathWhiteSlot;

    // Death: a loop of solid-colour flashes, then a fade through colour slot 12 (ROM: RRX7.ASM).
    private DeathStage _deathStage = DeathStage.White;

    private int _deathTimer;
    private int _invincibilityBlinkTicks;
    private int _invincibilityTicksRemaining;
    private IntVector2 _position;
    private bool _wasFiring;

    /// <summary>Spawns the player at <paramref name="startPosition"/> with <paramref name="lives"/> men.</summary>
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
    }

    /// <summary>The death animation's stages: a white flash, a colour flash, then the fade to black.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRF.ASM</c> <c>PDEATH</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>KILL_PLAYER</c>.</item>
    /// </list>
    ///
    /// The death stages: a fixed white, then a random colour, then the slot-12 fade.
    /// </remarks>
    private enum DeathStage
    {
        White,
        Colour,
        Fade,
    }

    /// <summary>The player sprite's own 8x12 box at <see cref="Position"/>.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRF.ASM</c> <c>PLAYER</c> — the arcade intersects the sprite, not a fixed
    /// cell.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>MOVE_PLAYER</c> (<c>$2FD0</c>).</item>
    /// </list>
    /// </remarks>
    public Rectangle GetBounds() => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>The walk frame this player is showing — a dying player is the same shape, drawn as a solid colour.</summary>
    public Texture2D GetCurrentAnimationFrame() => _sprites.PlayerAnimationFrames[GetWalkAnimationFrameIndex()];

    /// <summary>The way the player is drawn and walks — his last MOVEMENT direction.</summary>
    public Direction8 FacingDirection { get; private set; } = Direction8.Up;

    /// <summary>TEMPORARY playtest aid: while set, <see cref="Kill"/> is a complete no-op.</summary>
    /// <remarks>Per player, so the attract DEMO can opt out and the machine still plays by the
    /// arcade's rules (otherwise every contact path in the demo is dead code).</remarks>
    public bool InvincibleForTesting { get; set; } = PlayerTuning.PlayerInvincibleForTesting;

    /// <summary>Passes through hazards unharmed for a short time after a respawn.</summary>
    public bool IsInvincible() => _invincibilityTicksRemaining > 0;

    /// <summary>True when this update fired a laser.</summary>
    public bool FiredLaserThisUpdate { get; private set; }

    /// <summary>Alive, Dying (the death animation's flash and fade), or Dead pending respawn.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRX7.ASM</c> <c>PDTHV</c> — the flash and fade.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>KILL_PLAYER</c>.</item>
    /// </list>
    /// </remarks>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Men remaining, including the one on screen; a death takes one off.</summary>
    public int Lives { get; private set; }

    /// <summary>Top-left of the player.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRF.ASM</c> the OBJX/OBJY registers.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>MOVE_PLAYER</c> (<c>$2FD0</c>).</item>
    /// </list>
    /// </remarks>
    public IntVector2 Position => _position;

    /// <summary>The palette slot the dying player is drawn solid in.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRF.ASM</c> <c>PDEATH</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>KILL_PLAYER</c>.</item>
    /// </list>
    ///
    /// The death colour: a fixed white slot, a random colour slot, or the fading slot.
    /// </remarks>
    internal int GetDeathSolidSlot() => _deathStage == DeathStage.Fade
        ? PlayerTuning.PlayerDeathFadeSlot
        : _deathFlashSlot;

    /// <summary>0-based index into <see cref="SpriteSet.PlayerAnimationFrames"/>.</summary>
    /// <remarks>The arcade numbers its frames 1 through 12, so arcade frame N is index N - 1.</remarks>
    internal int GetWalkAnimationFrameIndex() => (int)_walkSequence * 3 + WalkCycle[_walkCycleStep];

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

        // While invincible, the Player flickers on and off every tick (see ArcadeClock). There is no see-through blending.
        if (IsInvincible() && _invincibilityBlinkTicks >= PlayerTuning.InvincibilityFlickerVisibleTicks)
        {
            return;
        }

        // One colour while dying, like the arcade's own solid-colour drawing.
        if (this.IsDying())
        {
            _sprites.Blitter.DrawSpriteSolid(spriteBatch, GetCurrentAnimationFrame(), GetBounds(),
                _sprites.Blitter.GetSlotColour(GetDeathSolidSlot()));
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), Color.White);
    }

    /// <summary>Kills the player (contact with a live hazard). No-op while dying/dead.</summary>
    public void Kill()
    {
        if (InvincibleForTesting)
        {
            return; // A playtest aid that makes the Player unkillable (see InvincibleForTesting).
        }

        if (!this.IsAlive())
        {
            return;
        }

        StartDeath();
    }

    /// <summary>One tick: the death animation while dying, else clocks, movement, firing and animation. Before the game is live the player does nothing.</summary>
    /// <param name="gameTime">Not used: the player counts in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (this.IsDead())
        {
            return;
        }

        if (this.IsDying())
        {
            // While the death animation plays, the Player does not move.
            AdvanceDeath(field);
            return;
        }

        // The arcade holds the Player's movement and firing until the game goes live (ROM: RRG23.ASM PLAYRV, BITA #$01).
        if (!field.IsLive())
        {
            return;
        }

        AdvanceInvincibility();

        PlayerInputState input = field.Input.Poll();
        IntVector2 move = input.MoveDirection;

        // The aim sets only the direction of fire. It never changes the facing or the animation.
        Direction8? aimDirection = Direction8Extensions.CreateFromDelta(input.ShootDirection);

        MoveFromInput(move, field);
        UpdateFiring(input, aimDirection, field);
        AdvanceWalkAnimation(move);
    }

    /// <summary>Which walk sequence a direction uses: diagonals reuse the horizontal sequences.</summary>
    /// <param name="direction">The direction to map.</param>
    /// <returns>The walk sequence the direction uses.</returns>
    /// <remarks>The arcade's own stick-to-walk-sequence rule.</remarks>
    internal static WalkSequence GetWalkSequence(Direction8 direction) => direction switch
    {
        Direction8.Left or Direction8.UpLeft or Direction8.DownLeft => WalkSequence.Left,
        Direction8.Right or Direction8.UpRight or Direction8.DownRight => WalkSequence.Right,
        Direction8.Down => WalkSequence.Down,
        _ => WalkSequence.Up,
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

    /// <summary>Where a shot in this direction leaves the player: spec px from the player cell's top-left.</summary>
    /// <param name="direction">The direction the shot is fired in.</param>
    /// <returns>The muzzle's offset from the player's top-left, in port pixels.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> the muzzle-offset table.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>MOVE_PLAYER_LASER_RIGHT</c> (<c>$3279</c>) area.</item>
    /// </list>
    /// </remarks>
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
            _ => (2, 12), // Down and to the right.
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
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRX7.ASM</c> the death routine.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>KILL_PLAYER</c>.</item>
    /// </list>
    ///
    /// The fade takes over palette slot 12 and suspends that slot's own colour-cycling
    /// ("decay") process while it runs, then resumes it.
    /// </remarks>
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
                _deathFlashSlot =
                    PlayerTuning.PlayerDeathFlashSlots[_random.Next(PlayerTuning.PlayerDeathFlashSlots.Length)];
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
                // Write the next fade step, four frames after the last. The last step, which is black, ends the death.
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

    /// <summary>Runs one tick of the invincibility countdown and its flicker.</summary>
    private void AdvanceInvincibility()
    {
        if (_invincibilityTicksRemaining > 0)
        {
            _invincibilityTicksRemaining--;
        }

        _invincibilityBlinkTicks =
            (_invincibilityBlinkTicks + 1) % (PlayerTuning.InvincibilityFlickerVisibleTicks +
                                              PlayerTuning.InvincibilityFlickerHiddenTicks);

    }

    /// <summary>Advances the walk cycle; with the stick centred the frame holds where it is.</summary>
    /// <param name="move">The stick's per-axis direction, -1 / 0 / +1.</param>
    private void AdvanceWalkAnimation(IntVector2 move)
    {
        if (move == IntVector2.Zero)
        {
            return;
        }

        WalkSequence walkSequence = GetWalkSequence(FacingDirection);
        if (walkSequence != _walkSequence)
        {
            // A new direction resets the walk sequence and the frame hold, so the first frame
            // of the new direction shows at once.
            _walkSequence = walkSequence;
            _walkCycleStep = 0;
            _animationFrameTicks = 1;
        }
        else if (_animationFrameTicks >= FrameTicksPerAnimationFrame)
        {
            _animationFrameTicks = 1;
            _walkCycleStep = (_walkCycleStep + 1) % WalkCycle.Length;
        }
        else
        {
            _animationFrameTicks++;
        }
    }

    /// <summary>Starts the fade: suspends the slot-12 decay and writes the first byte.</summary>
    /// <param name="field">The playfield, whose palette runs the fade.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRX7.ASM</c> the death routine (the colour processes live in RRS22.ASM).</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>KILL_PLAYER</c>.</item>
    /// </list>
    ///
    /// The colour processes carry on as normal;
    /// only the decay suspends.
    /// </remarks>
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

        // Each axis moves separately. If a move would overlap a wall, that axis is put back, so the Player never overlaps a wall.
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
    /// <param name="aimDirection">The aim stick's direction, or null when it is centred.</param>
    /// <param name="field">The playfield, which owns the laser slots.</param>
    /// <remarks>A shot re-fires every <see cref="PlayerTuning.PlayerAutoFireTicks"/> ticks; the attempt
    /// is a no-op when the three slots are full.</remarks>
    private void UpdateFiring(PlayerInputState input, Direction8? aimDirection, PlayField field)
    {
        bool isFireHeld = input.FireHeld;
        if (isFireHeld && (!_wasFiring || --_autoFireTicksRemaining <= 0))
        {
            Direction8 fireDirection = aimDirection ?? FacingDirection;
            IntVector2 muzzle = _position + GetMuzzleOffset(fireDirection);
            FiredLaserThisUpdate = field.TryFirePlayerLaser(muzzle, fireDirection);
            _autoFireTicksRemaining = PlayerTuning.PlayerAutoFireTicks;
        }
        else
        {
            FiredLaserThisUpdate = false;
        }

        _wasFiring = isFireHeld;
    }

    /// <summary>Writes the next fade byte into the death colour's palette slot.</summary>
    /// <param name="field">The playfield, whose palette holds the death slot.</param>
    private void WriteDeathFade(PlayField field) =>
        field.Palette?.SetSlot(PlayerTuning.PlayerDeathFadeSlot, PlayerTuning.PlayerDeathFadeValues[_deathFadeIndex]);
}
