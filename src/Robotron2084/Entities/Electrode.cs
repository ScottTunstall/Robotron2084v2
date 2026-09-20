using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// An electrode — one of the static hazard posts. It never moves (spec: "Electrodes are static
/// and never move"). A player laser, a walking grunt (both die), a walking hulk (only the
/// electrode dies) or the player (both die) can kill it; collision resolution lives in
/// <see cref="PlayField"/>, so this class knows nothing about who killed it.
///
/// Dying is a three-picture SHRIVEL — not a blink, and not an explosion — which is why this
/// class is deliberately not <see cref="IExplodable"/>.
/// </summary>
/// <remarks>
/// The ROM's post kill process is RRP8.ASM `PSTKIL` → `PKPROC`. `PSTKIL` is explicit about the
/// "no explosion" part: it does `KILPST` (kill the post), `DMAOFF` (image off) and
/// `MAKP PKPROC` (the shrivel process) with NO `EXST` call, so nothing bursts. Only the
/// PLAYER-contact path differs (`BNE PSTKON` → `OPON`, "turn him on"). The port used to spawn a
/// shatter explosion on laser hits; the author reported it (2026-09-16, "electrodes shouldn't
/// explode when hit, they shrivel").
/// </remarks>
public sealed class Electrode : IEntity
{
    /// <summary>The collision box: the post picture's own size, 10x9 arcade px, in port pixels, at <see cref="Position"/>.</summary>
    /// <remarks>The ROM's post picture.</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.ElectrodeCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.ElectrodeCollisionSize.Height));

    /// <summary>How long each shrivel picture is held, in ROM ticks. Three pictures: 6, 3, 2.</summary>
    /// <remarks>RRP8.ASM: the picture entries' trailing sleep bytes — PSP1 = 6, PSP2 = 3, PSP3 = 2,
    /// then the sequence's terminating 0 turns the image off. 11 vblanks is about 0.22 s.</remarks>
    private static readonly int[] ShrivelSleepRomTicks = [6, 3, 2];
    private readonly int _wave;
    private int _shrivelStep;
    private int _shrivelFifths;
    private IntVector2 _position;

    /// <summary>Creates an electrode for the given wave.</summary>
    /// <param name="position">Top-left of the electrode.</param>
    /// <param name="wave">The wave number; it decides both the post's picture family and its colour.</param>
    /// <remarks>Both are wave-dependent, via RRG23's `GTWCOL`.</remarks>
    public Electrode(IntVector2 position, int wave = 1)
    {
        _position = position;
        _wave = wave;
    }

    /// <summary>The post picture family this electrode draws.</summary>
    internal int FamilyIndex => GameplayConstants.PostFamilyForWave(_wave);

    /// <summary>
    /// The palette slot the post is drawn in for this wave. The post is a solid silhouette in the
    /// slot's live colour, so a wave whose post names a cycling slot really cycles.
    /// </summary>
    /// <remarks>ROM RRG23 `GTWCOL` → `PSTCOL`; `PSTKON` → `OPON1` → `MPCTON`, the blitter's op
    /// `$1A` (notes §47).</remarks>
    internal int TintSlot => GameplayConstants.PostSlotForWave(_wave);

    /// <summary>Top-left of the electrode; the electrode itself never moves (spec).</summary>
    public IntVector2 Position => _position;

    /// <summary>The post picture's own 10x9 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until something kills it; Dying while the shrivel plays.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>
    /// Starts the shrivel. The electrode is removed when the last picture's hold expires, so it
    /// can no longer stop anything. Does nothing unless the electrode is alive.
    /// </summary>
    /// <remarks>
    /// ROM RRP8.ASM `PKPROC`: step through the post's 3 death pictures holding each for 6/3/2
    /// vblanks, then remove it.
    ///
    /// NOTE: the ROM's posts come in 4 visual types (star / snowflake / square / triangle), each
    /// with its own 3-frame shrivel = `ElectrodeFrames[0..11]` (`PSP1A`..`PSP3D`). This port only
    /// ever spawns type A, so the shrivel uses frames 0-2; assigning the other types is a tracked
    /// follow-up.
    /// </remarks>
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

    /// <summary>
    /// Plays the shrivel: one step per death picture, each held for its own sleep, then the
    /// electrode is removed. Does nothing unless it is dying.
    /// </summary>
    /// <param name="gameTime">Unused — the holds are counted in ROM frames (notes §52).</param>
    /// <param name="field">Unused — nothing about the shrivel depends on the playfield.</param>
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

    /// <summary>Draws the live or shrivel picture, as a solid silhouette in the wave's post colour.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the post pictures.</param>
    /// <remarks>The ROM blits it solid (`MPCTON`, op `$1A` — notes §47).</remarks>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        sprites.DrawSpriteSolid(spriteBatch, CurrentArt(sprites), Bounds, sprites.SlotColor(TintSlot));
    }

    /// <summary>This electrode's current picture, for the appear effect.</summary>
    /// <param name="sprites">The shared sprite set, which holds the post pictures.</param>
    /// <returns>The live frame, or the current shrivel frame while it is dying.</returns>
    /// <remarks>See <see cref="IArtSource"/>: the appear engine blits whatever picture an object
    /// is showing, so it can materialise an electrode too.</remarks>
    public Texture2D CurrentFrameArt(SpriteSet sprites) => CurrentArt(sprites);
}
