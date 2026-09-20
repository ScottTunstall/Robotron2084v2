using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The human-controlled avatar. This is the character the player moves around the
/// playfield, shoots with, and can lose (and later respawn as). It has three life-cycle
/// states, reported through <see cref="LifeState"/>: <c>Alive</c> (normal play — the
/// state machine below runs), <c>Dying</c> (a ~2-second death animation of solid-colour
/// flashing then a fade to black — see <see cref="AdvanceDeath"/> — during which the
/// player can't move or fire and the robots are frozen too), and <c>Dead</c> (the
/// animation is finished and the player is invisible, waiting for the playfield to
/// respawn it with a life remaining, or end the game).
///
/// 8-way digital movement at 2/3 px/tick (X/Y), firing on the rising
/// edge of the fire button — "rising edge" means the moment the button goes from not
/// pressed to pressed, so a press fires immediately, and HOLDING the button
/// re-fires every <c>PlayerAutoFireTicks</c> (the 3-laser slot cap is the binding
/// limit) — plus a 2-second start grace (the player acts, the robots are frozen), a
/// 2-second death animation with robot freeze, and post-respawn flickering
/// invincibility.
///
/// Two-stick controls — a deliberate deviation from the arcade's single 8-way
/// joystick; don't revert without checking — movement (WASD) and aim/fire (IJKL) are
/// independent. The character FACES (and the walk animation follows) its last
/// MOVEMENT direction only; the aim direction drives the laser direction without
/// affecting facing or animation.
///
/// The walk animation picks one of 4 sequences of three pictures from the MOVEMENT
/// stick — left, right, down and up, with the diagonals reusing the horizontal
/// sequences — and holds each picture for 3 movement ticks. A direction change resets
/// the sequence index, the animation is FROZEN while the stick is centred, and a wave
/// starts on the first DOWN frame.
/// </summary>
/// <remarks>
/// <para>
/// See the terminology glossary on <see cref="IEntity"/> for what "ROM frame", the
/// "..Timer" fixed-point clock, "R5" (a specific 1982 ROM build) and "notes §NN" mean
/// generally.
/// </para>
/// The auto-fire cadence comes from the spec's "when the player hits the FIRE button";
/// the post-respawn invincibility is Phase 11.4.
///
/// Walk animation (ROM: the player-movement routine, notes §17): the stick selects one of 4
/// walk sequences over 12 numbered frames (left 1,2,1,3 / right 4,5,4,6 / down
/// 7,8,7,9 / up 10,11,10,12; diagonals reuse the horizontal sequence), each
/// frame drawn for 3 movement ticks, the sequence index resetting to 0 on a
/// direction change, and the animation FROZEN while the stick is centered. A
/// wave starts on frame 7, the first DOWN frame. Port frame N = arcade frame N,
/// verified against the ROM's own picture table.
/// </remarks>
public sealed class Player : IEntity
{
    /// <summary>
    /// Collision box = the player picture's own dimensions (8x12 arcade px),
    /// top-left anchored at <see cref="Position"/> (the art is drawn exactly there).
    /// </summary>
    /// <remarks>The ROM collides against the player's PICTURE, not a fixed 16x16 cell.</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.PlayerCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.PlayerCollisionSize.Height));

    /// <summary>Walk cycle per direction: [f0, f1, f0, f2] (e.g. left = 1,2,1,3).</summary>
    /// <remarks>The arcade's own walk cycle pattern.</remarks>
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    /// <summary>Each animation frame is drawn for 3 movement ticks.</summary>
    /// <remarks>The ROM's own frame-hold counter, which counts 0 to 2.</remarks>
    private const int FrameTicksPerAnimationFrame = 3;

    private readonly TimeSpan _graceDuration = TimeSpan.FromSeconds(GameplayConstants.PlayerStartGraceSeconds);
    private readonly Random _random;
    private TimeSpan _graceRemaining;
    private bool _wasFiring;
    private int _autoFireTicksRemaining;
    private int _invincibilityTicksRemaining;
    private int _invincibilityBlinkTicks;

    // Player death (ROM: RRX7.ASM's death routine, notes §66): a solid-colour flash loop, then
    // the slot-12 fade. All on the exact-6ths clock (notes §52).
    private DeathStage _deathStage = DeathStage.White;
    private int _deathTimer;
    private int _deathFlashIterationsRemaining = GameplayConstants.PlayerDeathFlashIterations;
    private int _deathFlashSlot = GameplayConstants.PlayerDeathWhiteSlot;
    private int _deathFadeIndex;

    /// <summary>
    /// The death animation's stages, played in order: the player sprite is drawn as a
    /// flat-colour silhouette (not its normal art) that alternates <see cref="White"/> and
    /// <see cref="Colour"/> several times — a strobing flash — and then <see cref="Fade"/>
    /// steps that silhouette's colour down to black before the player disappears.
    /// </summary>
    /// <remarks>The ROM's death stages: a fixed white, then a random colour, then the slot-12 fade.</remarks>
    private enum DeathStage
    {
        White,
        Colour,
        Fade,
    }

    private IntVector2 _position;
    // Animation state: a wave starts the player on frame 7 (the first DOWN
    // frame). _animFrameTicks = ticks the current frame has been shown (1..3).
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

    /// <summary>
    /// The way the player is DRAWN and walks — his last MOVEMENT direction only.
    /// The two-stick aim (round 8) deliberately does not touch it.
    /// </summary>
    public Direction8 FacingDirection { get; private set; } = Direction8.Up;

    /// <summary>Alive, Dying (the death animation's flash and fade), or Dead pending respawn.</summary>
    /// <remarks>The ROM's `PDTHV` flash and fade.</remarks>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Men remaining, including the one on screen; a death takes one off.</summary>
    public int Lives { get; private set; }

    /// <summary>
    /// True when this update fired a laser (PlayField plays the laser sound off this).
    /// </summary>
    /// <remarks>The ROM plays its laser sound at the same point in its own fire logic.</remarks>
    public bool LasersFiredThisUpdate { get; private set; }

    /// <summary>True until the start grace expires: the player acts, the robots do not.</summary>
    public bool IsInStartGracePeriod { get; private set; }

    /// <summary>Phase 11.4: passes through hazards unharmed for a short time after a respawn.</summary>
    public bool IsInvincible => _invincibilityTicksRemaining > 0;

    /// <summary>
    /// The TEMPORARY playtest aid of round 7 (<see cref="GameplayConstants.PlayerInvincibleForTesting"/>),
    /// per player so the attract DEMO can opt out: the machine plays by the
    /// arcade's rules, so its player must die when a robot, a missile or an
    /// electrode touches him — otherwise every contact path in the demo is dead
    /// code. While set, <see cref="Kill"/> is a complete no-op.
    /// </summary>
    /// <remarks>Notes §97.5.</remarks>
    public bool InvincibleForTesting { get; set; } = GameplayConstants.PlayerInvincibleForTesting;

    /// <summary>The player picture's own 8x12 box at <see cref="Position"/> (the ROM intersects the PICTURE).</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Awards an extra life (Phase 11.1, extra-life threshold crossings).</summary>
    public void AddLife() => Lives += 1;

    /// <summary>
    /// One tick of the player's life: while Dying it is the death animation instead; otherwise the
    /// grace and invincibility clocks, the movement (per axis, refusing a step into the wall), the
    /// fire button's edge and auto-fire, and the walk animation.
    /// </summary>
    /// <param name="gameTime">The elapsed time, used to run the start grace down.</param>
    /// <param name="field">The playfield: the input, the walls and the laser slots.</param>
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

        // The walk animation only advances while the stick is not centered —
        // when no direction is held, the ROM's equivalent logic bails out early
        // and leaves the current animation frame frozen — and a direction
        // change resets the sequence index to 0.
        if (move != IntVector2.Zero)
        {
            int group = WalkGroup(FacingDirection);
            if (group != _animGroup)
            {
                // Switching to a new direction's walk sequence resets the
                // sequence index to 0 (the ROM does not reset its own 3-tick
                // frame-hold counter when this happens; the port restarts it
                // too, so the new direction's first frame shows immediately).
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
    /// Where a shot of this facing leaves the player: spec px from the player cell's
    /// top-left, per facing. (The port used a uniform 10 spec-px offset, which made the
    /// laser visibly spawn off the player.)
    /// </summary>
    /// <param name="direction">The direction the shot is fired in.</param>
    /// <returns>The muzzle's offset from the player's top-left, in port pixels.</returns>
    /// <remarks>The ROM's own muzzle-offset table (ROM: RRG23.ASM; notes §19, §21).</remarks>
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
    /// Which walk sequence a facing uses: any leftward direction walks LEFT, any
    /// rightward walks RIGHT, pure up/down walk UP/DOWN (diagonals reuse the horizontal
    /// sequences).
    /// </summary>
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

    /// <summary>
    /// The palette slot the dying player is drawn SOLID in: the white flash's slot, a
    /// random colour slot, or the fading slot. Test hook.
    /// </summary>
    /// <remarks>The ROM's death colour: a fixed white slot, a random colour slot, or the fading
    /// slot (notes §66).</remarks>
    internal int DeathSolidSlot => _deathStage == DeathStage.Fade
        ? GameplayConstants.PlayerDeathFadeSlot
        : _deathFlashSlot;

    /// <summary>
    /// Starts the death animation, ignoring `PlayerInvincibleForTesting` — the
    /// playtest flag that makes <see cref="Kill"/> a no-op, and which otherwise
    /// makes the whole death sequence unreachable from a test.
    /// </summary>
    /// <remarks>Notes §66.</remarks>
    internal void StartDeathForTesting()
    {
        if (LifeState == EntityLifeState.Alive)
        {
            StartDeath();
        }
    }

    /// <summary>
    /// One tick of the death animation: the solid-colour FLASH loop first — white
    /// (slot 9) for 2 frames, then a random colour slot for 6 frames, ten times —
    /// followed by the slot-12 FADE, whose eight bytes are written one per 4 frames.
    /// The trailing zero byte ends the death, leaving the player invisible (drawn in
    /// black) until the playfield respawns it.
    /// </summary>
    /// <param name="field">The playfield, whose palette runs the fade.</param>
    /// <remarks>
    /// Ported from the arcade's own death sequence (ROM: RRX7.ASM's death routine, notes §66).
    /// "Slot" here means a palette slot — one of the arcade's shared colour-table entries
    /// (see <see cref="GamePalette"/>); the death animation temporarily takes over one slot
    /// to run its own fade-to-black instead of whatever the palette's normal colour-cycling
    /// ("decay") process would otherwise be doing with it, which is why that process is
    /// suspended while the fade runs and resumed afterwards.
    /// </remarks>
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
                // After the white flash, pick a RANDOM colour slot from the death flash palette.
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
                // Write the next fade byte, 4 frames apart. The last one (black)
                // is written too and then the death is over.
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
    /// Starts the fade: the palette's slot-12 decay is suspended, the player is drawn
    /// solid in slot 12, and the fade table's first byte goes in immediately.
    /// </summary>
    /// <param name="field">The playfield, whose palette runs the fade.</param>
    /// <remarks>The ROM's own fade-start step: the colour processes carry on as normal (they
    /// never stopped), and the palette's decay process is suspended for the death slot
    /// (<see cref="GamePalette.SuspendSlot"/>).</remarks>
    private void BeginDeathFade(PlayField field)
    {
        _deathStage = DeathStage.Fade;
        _deathFadeIndex = 0;
        field.Palette?.SuspendSlot(GameplayConstants.PlayerDeathFadeSlot);
        WriteDeathFade(field);
    }

    /// <summary>Writes the next fade byte into the death colour's palette slot.</summary>
    /// <param name="field">The playfield, whose palette holds the death slot.</param>
    /// <remarks>The ROM's own fade-byte-write step.</remarks>
    private void WriteDeathFade(PlayField field) =>
        field.Palette?.SetSlot(GameplayConstants.PlayerDeathFadeSlot, GameplayConstants.PlayerDeathFadeValues[_deathFadeIndex]);

    /// <summary>
    /// Draws the walk frame — skipped on the hidden half of the invincibility flicker, and drawn as a
    /// one-colour solid silhouette while the death animation is playing.
    /// </summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the player frames and slot colours.</param>
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

        // Still drawn while Dying (death animation) — but, like the ROM's own
        // solid-colour draw, in ONE colour: white, a random colour slot, or the
        // fading slot (notes §66).
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
