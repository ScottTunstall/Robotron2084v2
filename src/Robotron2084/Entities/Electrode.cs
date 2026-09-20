using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>An electrode — a static hazard post that shrivels away when something kills it.</summary>
/// <seealso cref="PlayField"/>
/// <remarks>ROM: RRP8.ASM (<c>PSTKIL</c> hands off to <c>PKPROC</c>). A post never explodes — its
/// picture switches off and it plays a 3-picture shrivel held 6, 3 and 2 frames — so this class is
/// deliberately not <see cref="IExplodable"/>. Its picture family and colour are looked up per wave
/// by RRG23.ASM's <c>GTWCOL</c> (notes §45). Timers count 5 per tick and 6 per arcade frame, so an
/// interval of N frames is due at 6 x N.</remarks>
public sealed class Electrode : IEntity
{
    /// <summary>The post picture's own 10x9 arcade px box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.ElectrodeCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.ElectrodeCollisionSize.Height));

    /// <summary>How long each shrivel picture is held, in ROM frames.</summary>
    private static readonly int[] ShrivelSleepRomTicks = [6, 3, 2];
    private readonly int _wave;
    private int _shrivelStep;
    private int _shrivelTimer;
    private IntVector2 _position;

    /// <summary>Creates an electrode for the given wave.</summary>
    /// <param name="position">Top-left of the electrode.</param>
    /// <param name="wave">The wave number; it decides the post's picture family and its colour.</param>
    public Electrode(IntVector2 position, int wave = 1)
    {
        _position = position;
        _wave = wave;
    }

    /// <summary>Which of the 9 electrode picture sets this electrode draws, chosen by wave number.</summary>
    /// <remarks>The arcade names only the first four shapes; its own table cycles 9 sets over 10 waves.</remarks>
    internal int FamilyIndex => GameplayConstants.PostFamilyForWave(_wave);

    /// <summary>The palette slot the post is drawn in for this wave; the slot's colour may cycle.</summary>
    internal int TintSlot => GameplayConstants.PostSlotForWave(_wave);

    /// <summary>Top-left of the electrode; it never moves.</summary>
    public IntVector2 Position => _position;

    /// <summary>The post picture's own 10x9 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until something kills it; Dying while the shrivel plays.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Starts the shrivel; does nothing unless the electrode is alive.</summary>
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

    /// <summary>Plays the shrivel: one picture per hold, then the electrode is removed.</summary>
    /// <param name="gameTime">Unused — the holds are counted in ticks.</param>
    /// <param name="field">Unused.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState != EntityLifeState.Dying)
        {
            return;
        }

        // Counts up to the next shrivel picture: 5 per tick, 6 per arcade frame.
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

    /// <summary>Draws the live or shrivel picture, as a solid silhouette in the wave's slot colour.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        sprites.DrawSpriteSolid(spriteBatch, CurrentArt(sprites), Bounds, sprites.SlotColor(TintSlot));
    }

    /// <summary>This electrode's current picture, for the appear effect.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <returns>The live frame, or the current shrivel frame while it is dying.</returns>
    public Texture2D CurrentFrameArt(SpriteSet sprites) => CurrentArt(sprites);
}
