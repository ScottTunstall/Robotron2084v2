using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// An enforcer — a spheroid's offspring, never present when a wave starts. It begins life immobile
/// while it GROWS through five spawn pictures (about 40 ticks, roughly 0.8 s), and then flies around
/// firing sparks.
///
/// Its movement is deliberately indirect: every so often it picks a point in a 32x32-pixel zone
/// down-right of the player and glides at it with a speed proportional to how far away it still is —
/// fast at a distance, crawling as it closes — so it loiter-circles near the player instead of running
/// straight into him. When its fire timer expires it shoots a spark at the player, even if the global
/// spark limit swallows the shot (the timer re-arms either way). It flies over electrodes.
///
/// A laser destroys it instantly — no flash, no death animation — and the field bursts it.
/// </summary>
/// <remarks>
/// R5 behaviour, notes §17: a short GROW-UP phase (5 spawn-animation steps x 8 ticks, ~40 ROM ticks,
/// immobile), then an AI pass every 3 ROM ticks that counts down (a) a re-aim timer, RND(1..31) passes,
/// and (b) a spark-fire timer, RND(1..ENSTIM) passes. Each re-aim picks a destination in a 32x32
/// spec-pixel zone to the player's DOWN-RIGHT (player + RND(0..31) on each axis, clamped to the field)
/// and the enforcer glides toward it at a velocity proportional to the remaining distance (delta/2 in
/// 1/256 column units per ROM frame, notes §93) until the timer runs out. Firing: `ENFSHT` re-arms the
/// countdown before the 20-spark global cap is checked (R5 $1404), so a swallowed shot is simply lost.
///
/// The grow-up is the ROM's 5-frame spawn animation (ENGD1..5 = the riddle-list enforcer2..6, verified
/// byte-identical in the ROM at $1921-$19FD), played 8 ticks per frame over the 40-tick grow-up, ending
/// on the full ENFD0 picture.
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
    /// only runs once per body (notes §43 fact 2, §93).</remarks>
    private IntVector2 _velocityFp;

    private IntVector2 _remainderFp;
    private int _bodyFifths;
    private int _moverSixths; // OPB80 cadence: one velocity integration per 6 sixths = 1 ROM frame
    private int _reaimBodiesRemaining;
    private int _fireCooldownBodies;
    private int _growthFifthsRemaining;

    /// <summary>Creates an enforcer at <paramref name="position"/>; it is immobile until its grow-up finishes.</summary>
    /// <param name="position">Top-left of the enforcer.</param>
    /// <param name="random">The random source for the re-aim destination and the fire timer.</param>
    /// <param name="fireDelayRomTicks">This wave's fire delay, in ROM frames: the interval is rolled as RND(1..this).</param>
    /// <param name="speedBonus">Unused by this entity (kept for the field's uniform spawn shape).</param>
    /// <remarks>ROM ENSTIM for this wave (notes §11.2/§17): fire interval = RND(1..ENSTIM) AI passes x 3 ticks.</remarks>
    public Enforcer(IntVector2 position, Random random, int fireDelayRomTicks = 24, int speedBonus = 0)
    {
        _position = position;
        _random = random;
        _fireDelayRomTicks = fireDelayRomTicks;
        _growthFifthsRemaining = GameplayConstants.EnforcerGrowUpRomFrames * 6;
        // ROM ENFR10 runs ENFNV immediately when the grow-up ends, and ENFDRP
        // seeds PD6 with RMAX(ENSTIM) — both countdowns are in BODIES.
        _reaimBodiesRemaining = 0;
        _fireCooldownBodies = 1 + random.Next(0, _fireDelayRomTicks);
        // The mover moves it from its first active frame (notes §93); the
        // accumulator does not advance during the immobile grow-up.
        _moverSixths = 6;
    }

    /// <summary>Top-left of the enforcer.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>The enforcer picture's own 10x11 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until shot or until it walks into an electrode; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Kills the enforcer: it is gone at once, and the field bursts it. There is no death animation.</summary>
    /// <remarks>
    /// ROM RRC11 `ENFKIL`: `JSR KILOFP` (kill the object and its process, image off) then `JSR EXST`. The
    /// port used to play a 2-second blink, which the author reported (2026-09-16, "enforcers shouldn't flash
    /// when hit") — the same defect class as the grunt in §44.
    /// </remarks>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>
    /// Runs one step of the enforcer's life when its clocks say so: the grow-up counts down (immobile and
    /// silent), the mover slides it toward the destination once per ROM frame, and each AI body re-aims and
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

        // Grow-up phase: immobile, no firing (R5 spawn animation, $1381). The
        // countdown is in exact 6ths so the five grow pictures change on the ROM's
        // 9-frame boundaries (10.8 ticks), not every truncated PortTicks(9) = 10 —
        // and the first active tick is the one the countdown reaches zero on (the
        // ROM's ENFR10 runs immediately when the grow-up ends).
        if (_growthFifthsRemaining > 0)
        {
            _growthFifthsRemaining -= 5;
            if (_growthFifthsRemaining > 0)
            {
                return;
            }
        }

        // Glide toward the current destination: the ROM's OS integrates the velocity
        // once per ROM frame (notes §43 fact 2) — a frame is 6/5 of a tick, so every
        // 6 sixths, not every tick (that ran the enforcer 20% fast, notes §93) — and
        // only the 4-frame body re-aims.
        _moverSixths += 5;
        if (_moverSixths >= 6)
        {
            _moverSixths -= 6;
            AdvancePosition(field);
        }

        _bodyFifths += 5;
        if (_bodyFifths < GameplayConstants.EnforcerBodyRomFrames * 6)
        {
            return;
        }

        _bodyFifths -= GameplayConstants.EnforcerBodyRomFrames * 6;

        // ROM RRC11 ENFR1: re-aim when PD7 hits 0, fire when PD6 hits 0 — both
        // countdowns tick once per BODY.
        if (--_reaimBodiesRemaining <= 0)
        {
            _reaimBodiesRemaining = NextReaimBodies(_random);
            RollVelocity(field);
        }

        if (--_fireCooldownBodies <= 0)
        {
            _fireCooldownBodies = NextFireBodies(_random);
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
    /// <remarks>
    /// ROM `ENFNV` (`RRC11.ASM`): the target is the PLAYER plus a random 0..31 per axis — in COLUMNS on X and
    /// ROWS on Y — and the velocity is set to the offset HALVED, signed: `SUBB OX16,X / SBCA #0 / ASLB / ROLA`
    /// is a signed divide-by-2, so <c>OXV = (target - pos)/2</c> in 1/256 column/frame =
    /// <c>(target - pos)/128</c> port px/frame. That is the "near-zone crawl" the port's notes named but
    /// could not reproduce with a constant step.
    /// </remarks>
    private void RollVelocity(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        int targetX = field.Player.Position.X + ScreenSize.Scaled(2 * _random.Next(0, 32));
        int targetY = field.Player.Position.Y + ScreenSize.Scaled(_random.Next(0, 32));
        targetX = Math.Clamp(targetX, bounds.X, bounds.Right - CollisionSize.Width);
        targetY = Math.Clamp(targetY, bounds.Y, bounds.Bottom - CollisionSize.Height);

        IntVector2 delta = new(targetX - _position.X, targetY - _position.Y);

        // OXV = delta/2 in 1/256 COLUMNS per FRAME (the signed halve above), and a
        // column is 4 port units, so that is delta/128 port px per frame = delta/2
        // in 1/256-px (fp) units per frame. The mover integrates once per ROM frame
        // (notes §93), so the velocity is the raw per-frame value — no 5/6 tick
        // scaling (that belonged to the old per-tick integration).
        _velocityFp = new IntVector2(delta.X / 2, delta.Y / 2);
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
        _remainderFp += _velocityFp;
        int stepX = _remainderFp.X / 256;
        int stepY = _remainderFp.Y / 256;
        _remainderFp -= new IntVector2(stepX * 256, stepY * 256);

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

    /// <summary>
    /// NOT CALLED — the destination roll in <see cref="RollVelocity"/> computes this same target inline. Kept
    /// pending a decision on whether to delete it; a reader should not treat it as live behaviour.
    /// </summary>
    /// <param name="field">The playfield: the player to aim past, and the wall to clamp to.</param>
    /// <returns>A point in the zone down-right of the player, inside the playfield.</returns>
    /// <remarks>R5 $13B5: destination = player + RND(0..31) on each axis (a 32x32 zone down-right of the
    /// player), clamped to the playfield.</remarks>
    private IntVector2 PickDestination(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        int playerX = field.Player.Position.X;
        int playerY = field.Player.Position.Y;
        int x = Math.Clamp(playerX + _random.Next(0, ScreenSize.SpecScale * 32), bounds.X, bounds.Right - 1 - CollisionSize.Width);
        int y = Math.Clamp(playerY + _random.Next(0, ScreenSize.SpecScale * 32), bounds.Y, bounds.Bottom - 1 - CollisionSize.Height);
        return new IntVector2(x, y);
    }

    /// <summary>Rolls the re-aim countdown, in bodies: 0..31 (0 means re-aim again next body).</summary>
    /// <remarks>ROM ENFNV: `ANDA #$1F` — the re-aim countdown is 0..31 BODIES (0 re-aims again next body).</remarks>
    private static int NextReaimBodies(Random random) => random.Next(0, 32);

    /// <summary>Which of the five grow-up pictures is showing (0..4), or -1 once the grow-up has finished (test hook).</summary>
    /// <remarks>The ROM changes pictures every 9 frames, so the index is derived from the exact-6ths countdown
    /// (notes §65 — the old `PortTicks(9)` = 10 switched them ~6% early inside a correctly-timed growth).</remarks>
    internal int GrowFrameIndex => _growthFifthsRemaining > 0
        ? (GameplayConstants.EnforcerGrowUpRomFrames * 6 - _growthFifthsRemaining)
            / (GameplayConstants.EnforcerGrowStepRomFrames * 6)
        : -1;

    /// <summary>The interval until the next shot: 1..the wave's fire delay, in bodies.</summary>
    /// <remarks>ROM `ENFSHT`: `RMAX(ENSTIM)` — `RND(1..ENSTIM)` BODIES, re-armed BEFORE the cap check, so a
    /// shot swallowed by the 20-spark cap is simply lost.</remarks>
    private int NextFireBodies(Random random) => random.Next(1, _fireDelayRomTicks + 1);

    /// <summary>Draws the grow-up picture while it is growing, and the full picture afterwards; an enforcer never flashes.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the enforcer frames.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        // The enforcer NEVER flashes: not while alive (round 7 playtest — the
        // old plan-8.5 toggle is gone) and not when hit (round 14 — the ROM's
        // ENFKIL explodes it immediately, so there is no Dying state at all).

        // Grow-up (R5 $1381 / RRC11 ENFDRP): the drop plays the FIVE ROM
        // spawn pictures — ENGD1..ENGD5, byte-identical to the riddle-list
        // enforcer2..6 ($1921/$1958/$198F/$19C6/$19FD), 8 ticks each —
        // then the full ENFD0 picture. (Round 8: the round-7 scale-up stood
        // in for art that was already in Content; this is the real sequence.)
        Microsoft.Xna.Framework.Graphics.Texture2D art = sprites.Enforcer;
        if (LifeState == EntityLifeState.Alive && _growthFifthsRemaining > 0)
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
        if (LifeState == EntityLifeState.Alive && _growthFifthsRemaining > 0)
        {
            int frame = Math.Clamp(GrowFrameIndex, 0, sprites.EnforcerFrames.Length - 2); // frames 2..6 (1-based) = ENGD1..5
            art = sprites.EnforcerFrames[1 + frame];
        }

        return art;
    }
}
