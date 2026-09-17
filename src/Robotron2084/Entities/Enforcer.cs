using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// Dropped by Spheroids (never present at level start). R5 behaviour
/// (notes §17): a short GROW-UP phase (5 spawn-animation steps x 8 ticks,
/// ~40 ROM ticks, immobile), then an AI pass every 3 ROM ticks that counts
/// down (a) a re-aim timer, RND(1..31) passes, and (b) a spark-fire timer,
/// RND(1..ENSTIM) passes. Each re-aim picks a destination in a 32x32 spec-pixel
/// zone to the player's DOWN-RIGHT (player + RND(0..31) on each axis,
/// clamped to the field) and the enforcer glides at ~1.0 spec px/tick toward
/// it until the timer runs out — so it loiter-circles near the player rather
/// than converging on it head-on. Fires SPARKs at the player when the fire
/// timer hits zero (the timer re-arms even when the 20-spark global cap
/// swallows the shot — R5 $1404). Flies over electrodes.
///
/// Deliberate simplifications (documented): the ROM's sub-pixel 16-bit
/// velocity (swoop ~1.1-1.9 px/tick away from the zone, crawl near it) is
/// approximated by a constant 8-way step. The 5-frame spawn animation IS in
/// the R5 content (ENGD1..5 = riddle-list enforcer2..6, verified byte-
/// identical in the ROM at $1921-$19FD) and is played 8 ticks per frame
/// over the 40-tick grow-up, ending on the full ENFD0 picture.
/// </summary>
public sealed class Enforcer : IEntity, IExplodable
{
    /// <summary>Collision box = the ROM picture dimensions (10x11 arcade px), top-left anchored at <see cref="Position"/>.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.EnforcerCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.EnforcerCollisionSize.Height));
    private readonly Random _random;
    private readonly int _fireDelayRomTicks;
    private IntVector2 _position;

    /// <summary>
    /// Velocity in 1/256 PORT units per TICK, carried by a remainder like the
    /// quark's, because the ROM's mover integrates it EVERY FRAME (notes §43 fact
    /// 2) while the enforcer's own logic only runs once per body.
    /// </summary>
    private IntVector2 _velocityFp;

    private IntVector2 _remainderFp;
    private int _bodyFifths;
    private int _reaimBodiesRemaining;
    private int _fireCooldownBodies;
    private int _growthFifthsRemaining;

    /// <param name="fireDelayRomTicks">ROM ENSTIM for this wave (notes §11.2/§17): fire interval = RND(1..ENSTIM) AI passes x 3 ticks.</param>
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
    }

    public IntVector2 Position => _position;

    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>
    /// Laser kill (ROM RRC11 `ENFKIL`): `JSR KILOFP` (kill the object and its
    /// process, image off) then `JSR EXST` — so the enforcer is gone
    /// IMMEDIATELY and the field bursts it. There is NO death animation: the
    /// port used to play a 2-second blink, which the author reported
    /// (2026-09-16, "enforcers shouldn't flash when hit") — same defect class as
    /// the grunt in §44.
    /// </summary>
    public void Kill() => LifeState = EntityLifeState.Dead;

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

        // Glide toward the current destination every TICK: the ROM's OS
        // integrates the velocity EVERY frame (notes §43 fact 2), and only the
        // 4-frame body re-aims.
        AdvancePosition(field);

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
    /// ROM `ENFNV`: the target is the PLAYER plus a random 0..31 per axis — in
    /// COLUMNS on X (HSEED&gt;&gt;3 added to PX's column byte) and ROWS on Y — and the
    /// velocity is set to TWICE that offset (`SUBB OX16,X / SBCA #0 / ASLB / ROLA`).
    /// The enforcer therefore advances <c>(target - pos)/128</c> per frame: quick
    /// when far, CRAWLING as it arrives. That is the "near-zone crawl" the port's
    /// notes named but could not reproduce with a constant step.
    /// </summary>
    private void RollVelocity(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        int targetX = field.Player.Position.X + ScreenSize.Scaled(2 * _random.Next(0, 32));
        int targetY = field.Player.Position.Y + ScreenSize.Scaled(_random.Next(0, 32));
        targetX = Math.Clamp(targetX, bounds.X, bounds.Right - CollisionSize.Width);
        targetY = Math.Clamp(targetY, bounds.Y, bounds.Bottom - CollisionSize.Height);

        IntVector2 delta = new(targetX - _position.X, targetY - _position.Y);

        // 2 x delta in 1/256 COLUMNS per FRAME, and a column is 4 port units, so
        // that is delta/128 units per frame = 2 x delta in 1/256 units per frame;
        // a tick is 5/6 of a frame, hence the 5/3.
        _velocityFp = new IntVector2(delta.X * 5 / 3, delta.Y * 5 / 3);
    }

    /// <summary>
    /// The ROM's mover (RRS22 OPB80, notes §43): the velocity is added every frame,
    /// and an axis whose step would leave the playfield is REJECTED — the object
    /// keeps that coordinate and slides along the wall, with no bounce.
    /// </summary>
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

    // R5 $13B5: destination = player + RND(0..31) on each axis (a 32x32
    // zone down-right of the player), clamped to the playfield.
    private IntVector2 PickDestination(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        int playerX = field.Player.Position.X;
        int playerY = field.Player.Position.Y;
        int x = Math.Clamp(playerX + _random.Next(0, ScreenSize.SpecScale * 32), bounds.X, bounds.Right - 1 - CollisionSize.Width);
        int y = Math.Clamp(playerY + _random.Next(0, ScreenSize.SpecScale * 32), bounds.Y, bounds.Bottom - 1 - CollisionSize.Height);
        return new IntVector2(x, y);
    }

    /// <summary>ROM ENFNV: `ANDA #$1F` — the re-aim countdown is 0..31 BODIES (0 re-aims again next body).</summary>
    private static int NextReaimBodies(Random random) => random.Next(0, 32);

    /// <summary>
    /// Which of the five grow pictures is showing (0..4), -1 once the grow-up has
    /// finished. The ROM changes pictures every 9 frames, so the index is derived
    /// from the exact-6ths countdown (notes §65 — the old `PortTicks(9)` = 10
    /// switched them ~6% early inside a correctly-timed growth).
    /// </summary>
    internal int GrowFrameIndex => _growthFifthsRemaining > 0
        ? (GameplayConstants.EnforcerGrowUpRomFrames * 6 - _growthFifthsRemaining)
            / (GameplayConstants.EnforcerGrowStepRomFrames * 6)
        : -1;

    /// <summary>
    /// ROM ENFSHT: `RMAX(ENSTIM)` — the shot countdown is RND(1..ENSTIM) BODIES, and
    /// it re-arms BEFORE the cap check, so a shot swallowed by the 20-spark cap is
    /// simply lost.
    /// </summary>
    private int NextFireBodies(Random random) => random.Next(1, _fireDelayRomTicks + 1);

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
