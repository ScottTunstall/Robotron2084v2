using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The player. 8-way digital movement at 2/3 px/tick (X/Y), fires on the
/// rising edge of the fire button — a press fires immediately, and HOLDING
/// the button re-fires every <c>PlayerAutoFireTicks</c> (the 3-laser SLOT
/// cap is the binding limit — spec "when the player hits the FIRE button",
/// playtest 2026-09-13 round 6) — 2-second start grace (player acts,
/// robots frozen),
/// 2-second death animation with robot freeze, and post-respawn flickering
/// invincibility (Phase 11.4).
///
/// Two-stick controls (user requirement, 2026-09-12/13 — deliberate
/// deviation from the arcade's single 8-way joystick): movement (WASD)
/// and aim/fire (IJKL) are independent. The character FACES (and the walk
/// animation follows) its last MOVEMENT direction only; the aim direction
/// drives the laser direction without affecting facing/animation (round 8).
///
/// R5 animation (MOVE_PLAYER $2FD0, notes §17): the stick selects one of 4
/// walk sequences over frames 1..12 (left 1,2,1,3 / right 4,5,4,6 / down
/// 7,8,7,9 / up 10,11,10,12; diagonals reuse the horizontal sequence), each
/// frame drawn for 3 movement ticks, the sequence index resetting to 0 on a
/// direction change, and the animation FROZEN while the stick is centered
/// (descriptor 0 takes the early-out at $2FFE). A wave starts on frame 7
/// (WAVE_START_PLAYER points the metadata at $3603 = frame 7, first DOWN
/// frame). Port frame N = R5 frame N (verified against the ROM image table
/// $35EB + (N-1)*4, images $361B..$382B).
/// </summary>
public sealed class Player : IEntity
{
    /// <summary>
    /// Collision box = the ROM picture dimensions (COL0V intersects the
    /// PICTURE, not a 16x16 cell): 8x12 arcade px, top-left anchored at
    /// <see cref="Position"/> (the art is drawn exactly there).
    /// </summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.PlayerCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.PlayerCollisionSize.Height));

    /// <summary>R5 walk cycle per direction: [f0, f1, f0, f2] (e.g. left = 1,2,1,3).</summary>
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    /// <summary>Each R5 animation frame is drawn for 3 movement ticks ($70 counts 0→2).</summary>
    private const int FrameTicksPerAnimationFrame = 3;

    private readonly TimeSpan _graceDuration = TimeSpan.FromSeconds(GameplayConstants.PlayerStartGraceSeconds);
    private readonly Random _random;
    private TimeSpan _graceRemaining;
    private bool _wasFiring;
    private int _autoFireTicksRemaining;
    private int _invincibilityTicksRemaining;
    private int _invincibilityBlinkTicks;

    // PLAYER DEATH (ROM RRX7 `PDTHV`, notes §66): a solid-colour flash loop, then
    // the slot-12 fade. All on the exact-6ths clock (notes §52).
    private DeathStage _deathStage = DeathStage.White;
    private int _deathFifths;
    private int _deathFlashIterationsRemaining = GameplayConstants.PlayerDeathFlashIterations;
    private int _deathFlashSlot = GameplayConstants.PlayerDeathWhiteSlot;
    private int _deathFadeIndex;

    /// <summary>The ROM's `PDTHV` stages: $99 white, a random PDCTAB colour, then the slot-12 fade.</summary>
    private enum DeathStage
    {
        White,
        Colour,
        Fade,
    }

    private IntVector2 _position;
    // Animation state (R5 $71/$70): WAVE_START_PLAYER = frame 7 (first DOWN
    // frame). _animFrameTicks = ticks the current frame has been shown (1..3).
    private int _animGroup = WalkGroup(Direction8.Down);
    private int _animSequenceIndex;
    private int _animFrameTicks = 1;

    public Player(IntVector2 startPosition, int lives, Random? random = null)
    {
        _position = startPosition;
        _random = random ?? new Random();
        Lives = lives;
        _graceRemaining = _graceDuration;
        IsInStartGracePeriod = true;
    }

    public IntVector2 Position => _position;

    public Direction8 FacingDirection { get; private set; } = Direction8.Up;

    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    public int Lives { get; private set; }

    /// <summary>
    /// True when this update fired a laser (PlayField plays the ROM laser
    /// sound off this — the ROM requests $26E6 in the fire path, R5 $273A).
    /// </summary>
    public bool LasersFiredThisUpdate { get; private set; }

    public bool IsInStartGracePeriod { get; private set; }

    /// <summary>Phase 11.4: passes through hazards unharmed for a short time after a respawn.</summary>
    public bool IsInvincible => _invincibilityTicksRemaining > 0;

    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Awards an extra life (Phase 11.1, extra-life threshold crossings).</summary>
    public void AddLife() => Lives += 1;

    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        if (LifeState == EntityLifeState.Dying)
        {
            // Death animation: no movement; robots stay frozen via PlayField.RobotsFrozen.
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

        // Round 8: the aim (IJKL) drives the FIRE direction only — it must
        // NOT change the facing or the walk animation ("it's like the player
        // is facing the direction they are shooting"). Facing follows
        // movement only; with no aim the laser goes out in the facing.
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

        // A press fires immediately (arcade rising edge); holding the
        // button re-fires on a fixed cadence (playtest round 6). When all
        // three laser slots are busy TryFire simply no-ops.
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

        // R5 $2FFC-302E: the walk animation only advances while the stick is
        // not centered (descriptor 0 = early-out, animation frozen), and a
        // direction change resets the sequence index to 0.
        if (move != IntVector2.Zero)
        {
            int group = WalkGroup(FacingDirection);
            if (group != _animGroup)
            {
                // R5 $3003-3009: a new animation table resets the sequence
                // index to 0 (the ROM does not reset the 3-tick cadence
                // counter $70; we restart it so the new direction's first
                // frame shows immediately).
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

    /// <summary>
    /// ROM LTAB muzzle offset (RRG23; notes 2026-09-12 (19) and 2026-09-13
    /// (21)): spec px from the player cell's top-left, per facing. Replaces
    /// the old uniform 10 spec-px offset, which made the laser visibly spawn
    /// off the player (playtest 2026-09-13: "the laser's starting position
    /// is wrong when it fires").
    /// </summary>
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

    /// <summary>
    /// R5 stick→table rule: any leftward direction walks LEFT, any rightward
    /// walks RIGHT, pure up/down walk UP/DOWN (diagonals reuse the horizontal
    /// sequences — descriptor table $3031).
    /// </summary>
    internal static int WalkGroup(Direction8 direction) => direction switch
    {
        Direction8.Left or Direction8.UpLeft or Direction8.DownLeft => 0,
        Direction8.Right or Direction8.UpRight or Direction8.DownRight => 1,
        Direction8.Down => 2,
        _ => 3,
    };

    /// <summary>Current frame index into <c>SpriteSet.PlayerFrames</c> (R5 frame 1..12).</summary>
    internal int WalkFrameIndex => _animGroup * 3 + WalkCycle[_animSequenceIndex];

    /// <summary>Kills the player (contact with a live hazard). No-op while dying/dead.</summary>
    public void Kill()
    {
        if (GameplayConstants.PlayerInvincibleForTesting)
        {
            return; // TEMPORARY playtest aid (round 7) — see the constant.
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
        _deathFifths = 0;
        _deathFlashIterationsRemaining = GameplayConstants.PlayerDeathFlashIterations;
        _deathFlashSlot = GameplayConstants.PlayerDeathWhiteSlot;
        _deathFadeIndex = 0;
        Lives -= 1;
    }
    /// <summary>Respawn for "RESTART THE CURRENT LEVEL" (lives remaining).</summary>
    public void ResetForLevelRestart(IntVector2 startPosition)
    {
        _position = startPosition;
        LifeState = EntityLifeState.Alive;
        _deathStage = DeathStage.White;
        _deathFifths = 0;
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

    /// <summary>
    /// The palette slot the dying player is drawn SOLID in (the ROM's `OPON`
    /// colour): $99's slot 9, a random PDCTAB slot, or the fading slot 12.
    /// Test hook (notes §66).
    /// </summary>
    internal int DeathSolidSlot => _deathStage == DeathStage.Fade
        ? GameplayConstants.PlayerDeathFadeSlot
        : _deathFlashSlot;

    /// <summary>
    /// Starts the death animation, ignoring `PlayerInvincibleForTesting` — the
    /// playtest flag that makes <see cref="Kill"/> a no-op, and which otherwise
    /// makes the whole death sequence unreachable from a test (notes §66).
    /// </summary>
    internal void StartDeathForTesting()
    {
        if (LifeState == EntityLifeState.Alive)
        {
            StartDeath();
        }
    }

    /// <summary>
    /// ROM RRX7 `PDTHV` (notes §66): the death is a solid-colour FLASH loop — $99
    /// (slot 9) for 2 frames, then a random PDCTAB slot for 6 frames, ten times —
    /// followed by the slot-12 FADE: the DECAY process is stopped, the player is
    /// drawn solid in slot 12, and the eight fade bytes are written into slot 12
    /// one per 4 frames. The trailing $00 ends the death, leaving the player
    /// invisible (drawn in black) until the playfield respawns it.
    /// </summary>
    private void AdvanceDeath(PlayField field)
    {
        _deathFifths += 5;

        int romFrames = _deathStage switch
        {
            DeathStage.White => GameplayConstants.PlayerDeathWhiteRomFrames,
            DeathStage.Colour => GameplayConstants.PlayerDeathColourRomFrames,
            _ => GameplayConstants.PlayerDeathFadeRomFrames,
        };

        if (_deathFifths < romFrames * 6)
        {
            return;
        }

        _deathFifths -= romFrames * 6;

        switch (_deathStage)
        {
            case DeathStage.White:
                // PDTH1: a RANDOM colour (`LDA SEED / ANDA #3`) from PDCTAB.
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
                // PDTH3: write the next fade byte, 4 frames apart. The last one
                // ($00) is written too and then the death is over.
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

    /// <summary>
    /// PDTH2: the colour processes are restarted (`COLST` — nothing to do here,
    /// they never stopped), the DECAY process is KILLED OFF for slot 12
    /// (<see cref="GamePalette.SuspendSlot"/>), the player is drawn solid in slot
    /// 12, and the fade table's first byte goes in immediately.
    /// </summary>
    private void BeginDeathFade(PlayField field)
    {
        _deathStage = DeathStage.Fade;
        _deathFadeIndex = 0;
        field.Palette?.SuspendSlot(GameplayConstants.PlayerDeathFadeSlot);
        WriteDeathFade(field);
    }

    private void WriteDeathFade(PlayField field) =>
        field.Palette?.SetSlot(GameplayConstants.PlayerDeathFadeSlot, GameplayConstants.PlayerDeathFadeValues[_deathFadeIndex]);

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

        // Still drawn while Dying (death animation) — but, like the ROM's OPON
        // solid blit, in ONE colour: $99 white, a random PDCTAB slot, or the
        // fading slot 12 (notes §66).
        if (LifeState == EntityLifeState.Dying)
        {
            int slot = _deathStage == DeathStage.Fade
                ? GameplayConstants.PlayerDeathFadeSlot
                : _deathFlashSlot;
            sprites.DrawSpriteSolid(spriteBatch, sprites.PlayerFrames[WalkFrameIndex], Bounds, sprites.SlotColor(slot));
            return;
        }

        sprites.DrawSprite(spriteBatch, sprites.PlayerFrames[WalkFrameIndex], Bounds, Color.White);
    }
}
