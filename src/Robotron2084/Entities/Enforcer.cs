using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// An enforcer — a small, fast enemy robot that a <see cref="Spheroid"/> drops (spheroids are its
/// only source; none are ever present when a wave starts). It begins life immobile while it GROWS
/// through five spawn pictures (about 40 ticks, roughly 0.8 s — see the terminology glossary on
/// <see cref="IEntity"/> for "beat"/"ROM frame"/"..Timer"), and then flies around firing "sparks"
/// (its projectile) at the player.
///
/// Its movement is deliberately indirect: every so often it picks a point in a 32x32-pixel zone
/// down-right of the player and glides at it with a speed proportional to how far away it still is —
/// fast at a distance, crawling as it closes — so it loiter-circles near the player instead of running
/// straight into him. When its fire timer expires it shoots a spark at the player, even if the global
/// spark limit swallows the shot (the timer re-arms either way). It flies over electrodes (the wall
/// hazard ground-bound entities must avoid).
///
/// A laser destroys it instantly — no flash, no death animation — and the field bursts it.
/// </summary>
/// <remarks>
/// Ported from the arcade's own behaviour (ROM: RRC11.ASM's `ENFR1`/`ENFNV`/`ENFDRP`; notes §17/§93).
/// Firing re-arms the countdown BEFORE the 20-spark global cap is checked, so a shot the cap
/// swallows is simply lost, same as a missed shot.
/// </remarks>
public sealed class Enforcer : IEntity, IExplodable
{
    /// <summary>The collision box: the enforcer picture's own size, 10x11 arcade px, in port pixels,
    /// top-left anchored at <see cref="Position"/>.</summary>
    /// <remarks>The ROM picture's dimensions.</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.EnforcerCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.EnforcerCollisionSize.Height));
    private readonly Random _random;
    private readonly int _fireDelayRomTicks;
    private IntVector2 _position;

    /// <summary>
    /// Velocity in 1/256 port units per ROM frame, carried by a remainder (like the quark's)
    /// so the sub-pixel part is not lost between integrations.
    /// </summary>
    /// <remarks>The ROM's mover integrates it once per frame while the enforcer's own logic
    /// only runs once per beat (notes §43 fact 2, §93).</remarks>
    private IntVector2 _velocitySubpixels;

    private IntVector2 _remainderSubpixels;
    private int _beatTimer;
    private int _moveTimer; // OPB80 cadence: one velocity integration per 6 fifth-ticks = 1 ROM frame
    private int _reaimBeatsRemaining;
    private int _fireCooldownBeats;
    private int _growthRemaining;

    /// <summary>Creates an enforcer at <paramref name="position"/>; it is immobile until its grow-up finishes.</summary>
    /// <param name="position">Top-left of the enforcer.</param>
    /// <param name="random">The random source for the re-aim destination and the fire timer.</param>
    /// <param name="fireDelayRomTicks">This wave's fire delay, in ROM frames: the interval is a random 1..this.</param>
    /// <param name="speedBonus">Unused by this entity (kept for the field's uniform spawn shape).</param>
    /// <remarks>This wave's fire delay (ROM: ENSTIM, notes §11.2/§17): the fire interval is a random
    /// 1..that many AI passes, each 3 ticks.</remarks>
    public Enforcer(IntVector2 position, Random random, int fireDelayRomTicks = 24, int speedBonus = 0)
    {
        _position = position;
        _random = random;
        _fireDelayRomTicks = fireDelayRomTicks;
        _growthRemaining = GameplayConstants.EnforcerGrowUpRomFrames * 6;
        // The arcade re-aims the instant the grow-up ends, and seeds the fire timer
        // with a random 1..(this wave's fire delay) — both countdowns are counted in beats.
        _reaimBeatsRemaining = 0;
        _fireCooldownBeats = 1 + random.Next(0, _fireDelayRomTicks);
        // The mover moves it from its first active frame (notes §93); the
        // accumulator does not advance during the immobile grow-up.
        _moveTimer = 6;
    }

    /// <summary>Top-left of the enforcer.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>The enforcer picture's own 10x11 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until shot or until it walks into an electrode; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Kills the enforcer: it is gone at once, and the field bursts it. There is no death animation.</summary>
    /// <remarks>ROM: RRC11.ASM's `ENFKIL` — no blink (same defect class as the grunt in §44).</remarks>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>
    /// Runs one step of the enforcer's life when its clocks say so: the grow-up counts down (immobile and
    /// silent), the mover slides it toward the destination once per ROM frame, and each AI beat re-aims and
    /// fires a spark. Held completely still while <see cref="PlayField.RobotsFrozen"/>. 
    /// </summary>
    /// <param name="gameTime">Unused — every clock here is counted in ROM frames.</param>
    /// <param name="field">The playfield: the player to aim at, the wall, and the spawn helpers.</param>
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

        // Grow-up phase: immobile, no firing (the arcade's own spawn animation). This
        // countdown uses the fixed-point timer trick (see IEntity) so the five grow
        // pictures change on the ROM's true 9-frame boundaries (10.8 ticks) rather than
        // a rounded-down 10, and the first active tick is the one it reaches zero on
        // (the ROM runs its grow-up-finished step immediately when the countdown ends).
        if (_growthRemaining > 0)
        {
            _growthRemaining -= 5;
            if (_growthRemaining > 0)
            {
                return;
            }
        }

        // Glide toward the current destination: the ROM's OS integrates the velocity
        // once per ROM frame (notes §43 fact 2) — a frame is 6/5 of a tick, so every
        // 6 fifth-ticks, not every tick (that ran the enforcer 20% fast, notes §93) — and
        // only the 4-frame beat re-aims.
        _moveTimer += 5;
        if (_moveTimer >= 6)
        {
            _moveTimer -= 6;
            AdvancePosition(field);
        }

        _beatTimer += 5;
        if (_beatTimer < GameplayConstants.EnforcerBeatRomFrames * 6)
        {
            return;
        }

        _beatTimer -= GameplayConstants.EnforcerBeatRomFrames * 6;

        // Re-aim when the aim timer hits 0, fire when the fire timer hits 0 — both
        // countdowns tick down once per beat. (ROM: RRC11.ASM's `ENFR1`.)
        if (--_reaimBeatsRemaining <= 0)
        {
            _reaimBeatsRemaining = NextReaimBeats(_random);
            RollVelocity(field);
        }

        if (--_fireCooldownBeats <= 0)
        {
            _fireCooldownBeats = NextFireBeats(_random);
            if (field.ActiveSparkCount < GameplayConstants.GlobalActiveSparkCap)
            {
                // Notes 27: the ROM aims with the PLAYER position (velocity
                // ∝ distance, per axis) — pass it through instead of a unit
                // direction.
                field.SpawnSpark(_position, field.Player.Position);
            }
        }
    }

    /// <summary>
    /// Picks the next destination — the player plus a random offset — and sets the velocity toward it: quick
    /// at a distance, crawling as it arrives.
    /// </summary>
    /// <param name="field">The playfield: the player to aim past, and the wall to clamp to.</param>
    /// <remarks>Velocity is set to HALF the remaining distance, so speed decays smoothly as the
    /// enforcer nears its destination instead of arriving at a constant speed (ROM: RRC11.ASM's `ENFNV`).</remarks>
    private void RollVelocity(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        int targetX = field.Player.Position.X + ScreenSize.Scaled(2 * _random.Next(0, 32));
        int targetY = field.Player.Position.Y + ScreenSize.Scaled(_random.Next(0, 32));
        targetX = Math.Clamp(targetX, bounds.X, bounds.Right - CollisionSize.Width);
        targetY = Math.Clamp(targetY, bounds.Y, bounds.Bottom - CollisionSize.Height);

        IntVector2 delta = new(targetX - _position.X, targetY - _position.Y);

        // 1/256-px fixed-point units; the mover integrates once per ROM frame (notes §93).
        _velocitySubpixels = new IntVector2(delta.X / 2, delta.Y / 2);
    }

    /// <summary>
    /// Moves the enforcer by one frame's worth of velocity. An axis whose step would leave the playfield is
    /// refused, so it slides along the wall instead of bouncing off it.
    /// </summary>
    /// <param name="field">The playfield wall.</param>
    /// <remarks>The ROM's generic mover (RRS22 `OPB80`, notes §43).</remarks>
    private void AdvancePosition(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        _remainderSubpixels += _velocitySubpixels;
        int stepX = _remainderSubpixels.X / 256;
        int stepY = _remainderSubpixels.Y / 256;
        _remainderSubpixels -= new IntVector2(stepX * 256, stepY * 256);

        int x = _position.X + stepX;
        if (stepX != 0 && x >= bounds.X && x + CollisionSize.Width <= bounds.Right)
        {
            _position = _position with { X = x };
        }

        int y = _position.Y + stepY;
        if (stepY != 0 && y >= bounds.Y && y + CollisionSize.Height <= bounds.Bottom)
        {
            _position = _position with { Y = y };
        }
    }

    /// <summary>Rolls the re-aim countdown, in beats: 0..31 (0 means re-aim again next beat).</summary>
    /// <remarks>ROM ENFNV: `ANDA #$1F` — the re-aim countdown is 0..31 beats (0 re-aims again next beat).</remarks>
    private static int NextReaimBeats(Random random) => random.Next(0, 32);

    /// <summary>Which of the five grow-up pictures is showing (0..4), or -1 once the grow-up has finished (test hook).</summary>
    /// <remarks>The ROM changes pictures every 9 frames, so the index is derived from the fixed-point
    /// growth countdown (notes §65 — rounding that period down to 10 ticks used to switch pictures
    /// ~6% early inside an otherwise correctly-timed growth).</remarks>
    internal int GrowFrameIndex => _growthRemaining > 0
        ? (GameplayConstants.EnforcerGrowUpRomFrames * 6 - _growthRemaining)
            / (GameplayConstants.EnforcerGrowStepRomFrames * 6)
        : -1;

    /// <summary>The interval until the next shot: 1..the wave's fire delay, in beats.</summary>
    /// <remarks>The timer re-arms to a random 1..this wave's fire delay BEFORE the cap check runs, so a
    /// shot swallowed by the 20-spark cap is simply lost. (ROM: `ENFSHT`.)</remarks>
    private int NextFireBeats(Random random) => random.Next(1, _fireDelayRomTicks + 1);

    /// <summary>Draws the grow-up picture while it is growing, and the full picture afterwards; an enforcer never flashes.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the enforcer frames.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        // The enforcer never flashes, alive or when hit (ENFKIL explodes it immediately,
        // so there is no Dying state at all).

        // Grow-up: plays the five arcade spawn pictures, 8 ticks each, then the full
        // picture (ROM: RRC11.ASM's `ENFDRP`).
        Microsoft.Xna.Framework.Graphics.Texture2D art = sprites.Enforcer;
        if (LifeState == EntityLifeState.Alive && _growthRemaining > 0)
        {
            int frame = Math.Clamp(GrowFrameIndex, 0, sprites.EnforcerFrames.Length - 2); // frames 2..6 (1-based) = ENGD1..5
            art = sprites.EnforcerFrames[1 + frame];
        }

        sprites.DrawSprite(spriteBatch, art, Bounds, Color.White);
    }

    /// <summary>The frame an explosion would copy: the grow-up picture while dropping, the full picture afterwards.</summary>
    /// <param name="sprites">The shared sprite set, which holds the enforcer frames.</param>
    /// <returns>The texture for the picture currently on screen.</returns>
    public Texture2D CurrentFrameArt(SpriteSet sprites)
    {
        Texture2D art = sprites.Enforcer;
        if (LifeState == EntityLifeState.Alive && _growthRemaining > 0)
        {
            int frame = Math.Clamp(GrowFrameIndex, 0, sprites.EnforcerFrames.Length - 2); // frames 2..6 (1-based) = ENGD1..5
            art = sprites.EnforcerFrames[1 + frame];
        }

        return art;
    }
}
