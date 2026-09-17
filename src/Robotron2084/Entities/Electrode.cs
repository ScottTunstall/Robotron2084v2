using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// Static hazard (spec: "Electrodes are static and never move"). Killed by a
/// player laser, a walking grunt (both die), a walking hulk (only the
/// electrode dies), or the player (both die) — collision resolution lives in
/// <see cref="PlayField"/>; this class knows nothing about who kills it.
///
/// Death = the ROM's POST KILL PROCESS (RRP8.ASM `PSTKIL` → `PKPROC`): the
/// post plays its own 3-frame **shrivel** sequence — NOT a blink, and NOT an
/// explosion. `PSTKIL` is explicit about the last part: it does `KILPST` (kill
/// the post), `DMAOFF` (image off) and `MAKP PKPROC` (the shrivel process) with
/// **no `EXST`** call, so nothing bursts. Only the PLAYER-contact path differs
/// (`BNE PSTKON` → `OPON` "turn him on"). The port used to spawn a shatter
/// explosion on laser hits; the author reported it (2026-09-16, "electrodes
/// shouldn't explode when hit, they shrivel"), which is why this class is
/// deliberately NOT <see cref="IExplodable"/>.
/// </summary>
public sealed class Electrode : IEntity
{
    /// <summary>Collision box = the ROM picture dimensions (10x9 arcade px), top-left anchored at <see cref="Position"/>.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.ElectrodeCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.ElectrodeCollisionSize.Height));
    /// <summary>
    /// The shrivel frames' hold times in ROM vblanks (RRP8.ASM: the picture
    /// entries' trailing sleep bytes — PSP1 = 6, PSP2 = 3, PSP3 = 2, then the
    /// sequence's terminating 0 turns the image off). 11 vblanks ≈ 0.22 s.
    /// </summary>
    private static readonly int[] ShrivelSleepRomTicks = [6, 3, 2];
    private readonly int _wave;
    private int _shrivelStep;
    private int _shrivelFifths;
    private IntVector2 _position;

    /// <param name="position">Top-left of the electrode.</param>
    /// <param name="wave">The wave/level number. BOTH the post's picture family
    /// and its colour are wave-dependent (RRG23 `GTWCOL`).</param>
    public Electrode(IntVector2 position, int wave = 1)
    {
        _position = position;
        _wave = wave;
    }

    /// <summary>The post picture family this electrode draws (wave-derived, RRG23 GTWCOL).</summary>
    internal int FamilyIndex => GameplayConstants.PostFamilyForWave(_wave);

    /// <summary>
    /// The post's PALETTE SLOT for this wave (RRG23 `GTWCOL` → `PSTCOL`). The
    /// ROM makes the post a solid silhouette in this slot's live colour
    /// (`PSTKON` → `OPON1` → `MPCTON`, blitter op $1A — notes §47), so the port
    /// draws the art through <see cref="SpriteSet.DrawSpriteSolid"/> rather than
    /// tinting it. Slots 10-15 are the cycling ones, so a wave whose post names
    /// one of those cycles — as the hardware does.
    /// </summary>
    internal int TintSlot => GameplayConstants.PostSlotForWave(_wave);

    public IntVector2 Position => _position;

    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>
    /// Starts the ROM's post-kill SHRIVEL (RRP8.ASM `PKPROC`): step through the
    /// post's 3 death pictures (ElectrodeFrames 0→1→2) holding each for 6/3/2
    /// vblanks, then remove it. No-op while dying/dead.
    ///
    /// NOTE: the ROM's posts come in 4 visual types (star / snowflake / square /
    /// triangle), each with its own 3-frame shrivel = `ElectrodeFrames[0..11]`
    /// (RRP8's PSP1A..PSP3D). This port only ever spawns type A, so the shrivel
    /// uses frames 0-2; assigning the other types is a tracked follow-up.
    /// </summary>
    public void Kill()
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        LifeState = EntityLifeState.Dying;
        _shrivelStep = 0;
        _shrivelFifths = 0; // the first sleep is a full period (notes §52 clock)
    }

    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState != EntityLifeState.Dying)
        {
            return;
        }

        // PKPROC: advance to the next death picture when this frame's sleep
        // expires; past the last one the image is turned off (DMAOFF).
        // The ROM's per-frame sleeps in exact 6ths (notes §52): 6/3/2 ROM frames
        // are 7.2/3.6/2.4 ticks, not the truncated 7/3/2.
        _shrivelFifths += 5;
        if (_shrivelFifths < ShrivelSleepRomTicks[_shrivelStep] * 6)
        {
            return;
        }

        _shrivelFifths -= ShrivelSleepRomTicks[_shrivelStep] * 6;

        _shrivelStep++;
        if (_shrivelStep >= ShrivelSleepRomTicks.Length)
        {
            LifeState = EntityLifeState.Dead;
            return;
        }
    }

    /// <summary>The picture currently on screen — this family's alive frame, or its shrivel frame while dying.</summary>
    private Texture2D CurrentArt(SpriteSet sprites)
    {
        int baseFrame = FamilyIndex * GameplayConstants.PostPicturesPerFamily;
        int frame = LifeState == EntityLifeState.Dying ? baseFrame + _shrivelStep : baseFrame;
        return sprites.ElectrodeFrames[frame];
    }

    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        sprites.DrawSpriteSolid(spriteBatch, CurrentArt(sprites), Bounds, sprites.SlotColor(TintSlot));
    }

    public Texture2D CurrentFrameArt(SpriteSet sprites) => CurrentArt(sprites);
}
