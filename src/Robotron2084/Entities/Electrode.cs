using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// An electrode (also called a "post") — one of the small stationary hazard obstacles scattered
/// around the playfield. It never moves (spec: "Electrodes are static and never move") and never
/// attacks by itself; it is simply dangerous to touch. Touching one is lethal to whoever touches it
/// (with one exception): a player laser destroys it outright, a walking grunt dies AND destroys it
/// (both die), a walking hulk destroys it but the hulk itself is indestructible and unaffected (only
/// the electrode dies), and the player touching it kills both the player and the electrode. Collision
/// resolution (i.e. deciding who touched what and what happens) lives in <see cref="PlayField"/>, so
/// this class itself knows nothing about who killed it — it only knows how to play its own death
/// animation once told to.
///
/// Dying is a three-picture "shrivel": a brief sequence of shrinking/wilting pictures playing in
/// place (not a blink/flicker, and not an explosion) before the electrode disappears — which is why
/// this class is deliberately not <see cref="IExplodable"/> (unlike most other destroyable entities,
/// which do burst apart when killed).
/// </summary>
/// <remarks>
/// Ported from the arcade's own post-kill behaviour (ROM: RRP8.ASM, `PSTKIL` handing off to
/// `PKPROC`, the shrivel process). The original is explicit that killing a post never triggers an
/// explosion — it just marks the post dead, switches its picture off, and starts the shrivel; only
/// the player-contact path does something extra (turning the player's own "hit" state on).
/// </remarks>
public sealed class Electrode : IEntity
{
    /// <summary>The collision box: the post picture's own size, 10x9 arcade px, in port pixels, at <see cref="Position"/>.</summary>
    /// <remarks>The ROM's post picture.</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.ElectrodeCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.ElectrodeCollisionSize.Height));

    /// <summary>How long each shrivel picture is held, in ROM ticks. Three pictures: 6, 3, 2.</summary>
    /// <remarks>Copied from the ROM's own three shrivel pictures and how long each one is held
    /// (ROM: RRP8.ASM); the whole shrivel takes 11 ROM frames, about 0.22 seconds.</remarks>
    private static readonly int[] ShrivelSleepRomTicks = [6, 3, 2];
    private readonly int _wave;
    private int _shrivelStep;
    private int _shrivelTimer;
    private IntVector2 _position;

    /// <summary>Creates an electrode for the given wave.</summary>
    /// <param name="position">Top-left of the electrode.</param>
    /// <param name="wave">The wave number; it decides both the post's picture family and its colour.</param>
    /// <remarks>Both the picture family and the colour are looked up per wave (ROM: RRG23.ASM `GTWCOL`).</remarks>
    public Electrode(IntVector2 position, int wave = 1)
    {
        _position = position;
        _wave = wave;
    }

    /// <summary>
    /// Which of the 9 electrode picture sets this electrode draws (each set is one alive picture
    /// plus its own 2-picture shrivel sequence), chosen by wave number. The arcade only names the
    /// shapes of the first four — STAR, SNOWFLAKE, SQUARE, TRIANGLE — but its own per-wave table
    /// (notes §45) cycles through 9 distinct sets over a 10-wave sequence before repeating.
    /// </summary>
    internal int FamilyIndex => GameplayConstants.PostFamilyForWave(_wave);

    /// <summary>
    /// The palette slot the post is drawn in for this wave. "Palette slot" here means one of the
    /// arcade hardware's shared colour-table entries; some slots are static and some continuously
    /// cycle through several colours over time ("colour cycling"), so a wave whose electrode uses a
    /// cycling slot will visibly shimmer even though nothing about the electrode itself is animating.
    /// The post is drawn as a solid silhouette filled entirely in that slot's current colour.
    /// </summary>
    /// <remarks>Looked up per wave the same way as the picture family (ROM: RRG23.ASM `GTWCOL`,
    /// notes §47).</remarks>
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
    /// Steps through this electrode's own picture set's 3-picture death sequence (the first picture
    /// is the same as the alive look, held briefly before the two genuinely shrinking pictures play),
    /// holding each for its own length of time, then removes it (ROM: RRP8.ASM `PKPROC`). Which
    /// picture set — see <see cref="FamilyIndex"/> — was decided once, per wave, when the electrode
    /// was created; the shrivel just plays that set's own pictures, whichever ones they are.
    /// </remarks>
    public void Kill()
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        LifeState = EntityLifeState.Dying;
        _shrivelStep = 0;
        _shrivelTimer = 0; // the first sleep is a full period (notes §52 clock)
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

        // Advances to the next death picture once this picture's hold time expires; past the
        // last picture, the electrode is removed entirely. Each hold's length in port ticks
        // isn't a whole number, so it's tracked with the fixed-point fifths trick rather than
        // rounded down, which kept the timing exact instead of running slightly fast (notes §52).
        _shrivelTimer += 5;
        if (_shrivelTimer < ShrivelSleepRomTicks[_shrivelStep] * 6)
        {
            return;
        }

        _shrivelTimer -= ShrivelSleepRomTicks[_shrivelStep] * 6;

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
    /// <remarks>The arcade draws it as a filled shape in the slot's colour, not as detailed sprite
    /// art (notes §47).</remarks>
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
