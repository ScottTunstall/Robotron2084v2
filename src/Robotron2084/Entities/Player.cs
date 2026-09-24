using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The human-controlled avatar: moves, shoots, and can be lost and respawned.</summary>
/// <seealso cref="PlayField"/>
/// <seealso cref="PlayerLaser"/>
/// <remarks>ROM: RRX7.ASM's death routine, RRG23.ASM's muzzle-offset table and the player-movement
/// routine (notes §17). Movement is 8-way digital at 2/3 px a tick (X/Y); the fire button fires on its
/// rising edge and then re-fires every <c>PlayerAutoFireTicks</c> while held, with the 3-laser slot cap
/// as the binding limit. The two-stick controls are a deliberate deviation from the arcade's single
/// 8-way joystick — movement (WASD) and aim/fire (IJKL) are independent — so don't revert it without
/// checking. Facing follows MOVEMENT only; the aim sets the laser direction but never the facing or the
/// walk animation. The walk uses 4 sequences of 3 pictures (left 1,2,1,3 / right 4,5,4,6 / down
/// 7,8,7,9 / up 10,11,10,12; diagonals reuse the horizontal pair), each frame held 3 movement ticks,
/// the index resetting on a direction change and the animation frozen while the stick is centred; a
/// wave starts on frame 7, the first DOWN frame. Port frame N = arcade frame N. Death is a ~2 s
/// solid-colour flash loop then the slot-12 fade. Timers count 5 per tick and 6 per arcade frame, so
/// an interval of N frames is due at 6 x N.</remarks>
public sealed class Player : IEntity, IArtSource
{
    /// <summary>Collision box = the player picture's own 8x12 arcade px.</summary>
    /// <remarks>The ROM collides against the player's PICTURE, not a fixed 16x16 cell.</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.PlayerCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.PlayerCollisionSize.Height));

    /// <summary>Walk cycle per direction: [f0, f1, f0, f2] (e.g. left = 1,2,1,3).</summary>
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    /// <summary>Each animation frame is drawn for 3 movement ticks.</summary>
    private const int FrameTicksPerAnimationFrame = 3;

    private readonly TimeSpan _graceDuration = TimeSpan.FromSeconds(GameplayConstants.PlayerStartGraceSeconds);
    private readonly Random _random;
    private TimeSpan _graceRemaining;
    private bool _wasFiring;
    private int _autoFireTicksRemaining;
    private int _invincibilityTicksRemaining;
    private int _invincibilityBlinkTicks;

    // Death: a solid-colour flash loop, then the slot-12 fade (ROM: RRX7.ASM; see the remarks).
    private DeathStage _deathStage = DeathStage.White;
    private int _deathTimer;
    private int _deathFlashIterationsRemaining = GameplayConstants.PlayerDeathFlashIterations;
    private int _deathFlashSlot = GameplayConstants.PlayerDeathWhiteSlot;
    private int _deathFadeIndex;

    /// <summary>The death animation's stages: a white flash, a colour flash, then the fade to black.</summary>
    /// <remarks>The ROM's death stages: a fixed white, then a random colour, then the slot-12 fade.</remarks>
    private enum DeathStage
    {
        White,
        Colour,
        Fade,
    }

    private IntVector2 _position;
    // A wave starts on frame 7, the first DOWN frame; _animFrameTicks counts 1..3.
    private int _animGroup = WalkGroup(Direction8.Down);
    private int _animSequenceIndex;
    private int _animFrameTicks = 1;

    /// <summary>Spawns the player at <paramref name="startPosition"/> with <paramref name="lives"/> men and the start grace running.</summary>
    /// <param name="startPosition">Top-left of the player.</param>
    /// <param name="lives">How many men the player starts with; a death takes one off.</param>
    /// <param name="random">The random source for the death animation's colour, or null to create one.</param>
    public Player(IntVector2 startPosition, int lives, Random? random = null)
    {
        _position = startPosition;
        _random = random ?? new Random();
        Lives = lives;
        _graceRemaining = _graceDuration;
        IsInStartGracePeriod = true;
    }

    /// <summary>Top-left of the player (the ROM's OBJX/OBJY).</summary>
    public IntVector2 Position => _position;

    /// <summary>The way the player is drawn and walks — his last MOVEMENT direction.</summary>
    public Direction8 FacingDirection { get; private set; } = Direction8.Up;

    /// <summary>Alive, Dying (the death animation's flash and fade), or Dead pending respawn.</summary>
    /// <remarks>The ROM's `PDTHV` flash and fade.</remarks>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Men remaining, including the one on screen; a death takes one off.</summary>
    public int Lives { get; private set; }

    /// <summary>True when this update fired a laser.</summary>
    public bool LasersFiredThisUpdate { get; private set; }

    /// <summary>True until the start grace expires.</summary>
    public bool IsInStartGracePeriod { get; private set; }

    /// <summary>Phase 11.4: passes through hazards unharmed for a short time after a respawn.</summary>
    public bool IsInvincible => _invincibilityTicksRemaining > 0;

    /// <summary>TEMPORARY round-7 playtest aid: while set, <see cref="Kill"/> is a complete no-op.</summary>
    /// <remarks>Per player, so the attract DEMO can opt out and the machine still plays by the
    /// arcade's rules (otherwise every contact path in the demo is dead code).</remarks>
    public bool InvincibleForTesting { get; set; } = GameplayConstants.PlayerInvincibleForTesting;

    /// <summary>The player picture's own 8x12 box at <see cref="Position"/> (the ROM intersects the PICTURE).</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Awards an extra life (Phase 11.1, extra-life threshold crossings).</summary>
    public void AddLife() => Lives += 1;

    /// <summary>One tick: the death animation while dying, else clocks, movement, firing and animation.</summary>
    /// <param name="gameTime">The elapsed time, used to run the start grace down.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        if (LifeState == EntityLifeState.Dying)
        {
            // Death animation: no movement.
            AdvanceDeath(field);
            return;
        }

        if (_invincibilityTicksRemaining > 0)
        {
            _invincibilityTicksRemaining--;
        }

        _invincibilityBlinkTicks =
            (_invincibilityBlinkTicks + 1) % (GameplayConstants.InvincibilityFlickerVisibleTicks + GameplayConstants.InvincibilityFlickerHiddenTicks);

        if (IsInStartGracePeriod)
        {
            _graceRemaining -= gameTime.ElapsedGameTime;
            if (_graceRemaining <= TimeSpan.Zero)
            {
                IsInStartGracePeriod = false;
            }
        }

        PlayerInputState input = field.Input.Poll();
        IntVector2 move = input.MoveDirection;

        // The aim drives the FIRE direction only — never the facing or the animation.
        Direction8? aim = Direction8Extensions.FromDelta(input.AimDirection);

        if (move != IntVector2.Zero)
        {
            FacingDirection = Direction8Extensions.FromDelta(move)!.Value;
            // Per-axis move with wall revert: never allowed to overlap the wall.
            IntVector2 candidate = _position;
            int dx = move.X * GameplayConstants.PlayerSpeedX;
            if (dx != 0)
            {
                IntVector2 movedX = candidate + new IntVector2(dx, 0);
                if (!field.Wall.Intersects(new Rectangle(movedX.X, movedX.Y, CollisionSize.Width, CollisionSize.Height)))
                {
                    candidate = movedX;
                }
            }

            int dy = move.Y * GameplayConstants.PlayerSpeedY;
            if (dy != 0)
            {
                IntVector2 movedY = candidate + new IntVector2(0, dy);
                if (!field.Wall.Intersects(new Rectangle(movedY.X, movedY.Y, CollisionSize.Width, CollisionSize.Height)))
                {
                    candidate = movedY;
                }
            }

            _position = candidate;
        }

        // A press fires at once; holding re-fires on the auto-fire cadence (TryFire no-ops when full).
        bool fire = input.FirePressed;
        if (fire && (!_wasFiring || --_autoFireTicksRemaining <= 0))
        {
            Direction8 fireDirection = aim ?? FacingDirection;
            IntVector2 muzzle = _position + MuzzleOffset(fireDirection);
            LasersFiredThisUpdate = field.PlayerLasers.TryFire(muzzle, fireDirection, out _);
            _autoFireTicksRemaining = GameplayConstants.PlayerAutoFireTicks;
        }
        else
        {
            LasersFiredThisUpdate = false;
        }

        _wasFiring = fire;

        // The animation only advances while the stick is not centred.
        if (move != IntVector2.Zero)
        {
            int group = WalkGroup(FacingDirection);
            if (group != _animGroup)
            {
                // A new direction resets the sequence index and the frame hold, so its first
                // frame shows immediately.
                _animGroup = group;
                _animSequenceIndex = 0;
                _animFrameTicks = 1;
            }
            else if (_animFrameTicks >= FrameTicksPerAnimationFrame)
            {
                _animFrameTicks = 1;
                _animSequenceIndex = (_animSequenceIndex + 1) & 3;
            }
            else
            {
                _animFrameTicks++;
            }
        }
    }

    /// <summary>Where a shot of this facing leaves the player: spec px from the player cell's top-left.</summary>
    /// <param name="direction">The direction the shot is fired in.</param>
    /// <returns>The muzzle's offset from the player's top-left, in port pixels.</returns>
    /// <remarks>ROM: the muzzle-offset table (RRG23.ASM).</remarks>
    private static IntVector2 MuzzleOffset(Direction8 direction)
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
        return new(ScreenSize.Scaled(offset.x), ScreenSize.Scaled(offset.y));
    }

    /// <summary>Which walk sequence a facing uses: diagonals reuse the horizontal sequences.</summary>
    /// <param name="direction">The facing to map.</param>
    /// <returns>The walk-sequence group: 0 left, 1 right, 2 down, 3 up.</returns>
    /// <remarks>The arcade's own stick-to-walk-sequence rule.</remarks>
    internal static int WalkGroup(Direction8 direction) => direction switch
    {
        Direction8.Left or Direction8.UpLeft or Direction8.DownLeft => 0,
        Direction8.Right or Direction8.UpRight or Direction8.DownRight => 1,
        Direction8.Down => 2,
        _ => 3,
    };

    /// <summary>Current frame index into <c>SpriteSet.PlayerFrames</c> (frame 1..12).</summary>
    /// <remarks>Matches the arcade's own frame numbering, 1 through 12.</remarks>
    internal int WalkFrameIndex => _animGroup * 3 + WalkCycle[_animSequenceIndex];

    /// <summary>Kills the player (contact with a live hazard). No-op while dying/dead.</summary>
    public void Kill()
    {
        if (InvincibleForTesting)
        {
            return; // TEMPORARY playtest aid (round 7) — see the property / the constant.
        }

        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        StartDeath();
    }

    /// <summary>The death state machine's setup, shared by <see cref="Kill"/> and the test hook.</summary>
    private void StartDeath()
    {
        LifeState = EntityLifeState.Dying;
        _deathStage = DeathStage.White;
        _deathTimer = 0;
        _deathFlashIterationsRemaining = GameplayConstants.PlayerDeathFlashIterations;
        _deathFlashSlot = GameplayConstants.PlayerDeathWhiteSlot;
        _deathFadeIndex = 0;
        Lives -= 1;
    }
    /// <summary>Respawn for "RESTART THE CURRENT LEVEL" (lives remaining).</summary>
    /// <param name="startPosition">Where to place the player.</param>
    public void ResetForLevelRestart(IntVector2 startPosition)
    {
        _position = startPosition;
        LifeState = EntityLifeState.Alive;
        _deathStage = DeathStage.White;
        _deathTimer = 0;
        _deathFlashIterationsRemaining = GameplayConstants.PlayerDeathFlashIterations;
        _deathFlashSlot = GameplayConstants.PlayerDeathWhiteSlot;
        _deathFadeIndex = 0;
        _graceRemaining = _graceDuration;
        IsInStartGracePeriod = true;
        _invincibilityTicksRemaining = GameplayConstants.PlayerInvincibilityTicks;
        _wasFiring = false;
        _autoFireTicksRemaining = GameplayConstants.PlayerAutoFireTicks;
    }

    /// <summary>Test-only positioning hook (InternalsVisibleTo the test assembly).</summary>
    internal void TeleportTo(IntVector2 position) => _position = position;

    /// <summary>The palette slot the dying player is drawn solid in (test hook).</summary>
    /// <remarks>The ROM's death colour: a fixed white slot, a random colour slot, or the fading slot.</remarks>
    internal int DeathSolidSlot => _deathStage == DeathStage.Fade
        ? GameplayConstants.PlayerDeathFadeSlot
        : _deathFlashSlot;

    /// <summary>Starts the death animation, ignoring <c>PlayerInvincibleForTesting</c> (test hook).</summary>
    internal void StartDeathForTesting()
    {
        if (LifeState == EntityLifeState.Alive)
        {
            StartDeath();
        }
    }

    /// <summary>One tick of the death animation: the flash loop, then the fade.</summary>
    /// <param name="field">The playfield, whose palette runs the fade.</param>
    /// <remarks>ROM: RRX7.ASM's death routine. The fade takes over palette slot 12 and suspends that
    /// slot's own colour-cycling ("decay") process while it runs, then resumes it.</remarks>
    private void AdvanceDeath(PlayField field)
    {
        _deathTimer += 5;

        int romFrames = _deathStage switch
        {
            DeathStage.White => GameplayConstants.PlayerDeathWhiteRomFrames,
            DeathStage.Colour => GameplayConstants.PlayerDeathColourRomFrames,
            _ => GameplayConstants.PlayerDeathFadeRomFrames,
        };

        if (_deathTimer < romFrames * 6)
        {
            return;
        }

        _deathTimer -= romFrames * 6;

        switch (_deathStage)
        {
            case DeathStage.White:
                // After the white flash, pick a random colour slot.
                _deathFlashSlot = GameplayConstants.PlayerDeathFlashSlots[_random.Next(GameplayConstants.PlayerDeathFlashSlots.Length)];
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
                if (_deathFadeIndex >= GameplayConstants.PlayerDeathFadeValues.Length - 1)
                {
                    field.Palette?.ResumeSlot(GameplayConstants.PlayerDeathFadeSlot);
                    LifeState = EntityLifeState.Dead;
                }

                break;
        }
    }

    /// <summary>Starts the fade: suspends the slot-12 decay and writes the first byte.</summary>
    /// <param name="field">The playfield, whose palette runs the fade.</param>
    /// <remarks>ROM: the colour processes carry on as normal; only the decay suspends.</remarks>
    private void BeginDeathFade(PlayField field)
    {
        _deathStage = DeathStage.Fade;
        _deathFadeIndex = 0;
        field.Palette?.SuspendSlot(GameplayConstants.PlayerDeathFadeSlot);
        WriteDeathFade(field);
    }

    /// <summary>Writes the next fade byte into the death colour's palette slot.</summary>
    /// <param name="field">The playfield, whose palette holds the death slot.</param>
    private void WriteDeathFade(PlayField field) =>
        field.Palette?.SetSlot(GameplayConstants.PlayerDeathFadeSlot, GameplayConstants.PlayerDeathFadeValues[_deathFadeIndex]);

    /// <summary>Draws the walk frame, or a one-colour silhouette while dying.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        // While invincible: visible/hidden tick-toggle flicker (no alpha blending).
        if (IsInvincible && _invincibilityBlinkTicks >= GameplayConstants.InvincibilityFlickerVisibleTicks)
        {
            return;
        }

        // One colour while dying, like the ROM's own solid-colour draw.
        if (LifeState == EntityLifeState.Dying)
        {
            int slot = _deathStage == DeathStage.Fade
                ? GameplayConstants.PlayerDeathFadeSlot
                : _deathFlashSlot;
            sprites.DrawSpriteSolid(spriteBatch, CurrentFrameArt(sprites), Bounds, sprites.SlotColor(slot));
            return;
        }

        sprites.DrawSprite(spriteBatch, CurrentFrameArt(sprites), Bounds, Color.White);
    }

    /// <summary>The walk frame this player is showing — a dying player is the same shape, drawn as a solid colour.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    public Texture2D CurrentFrameArt(SpriteSet sprites) => sprites.PlayerFrames[WalkFrameIndex];
}
