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
public sealed class Electrode : IEntity, IAnimationFrameSource, IRemovable
{
    private readonly SpriteSet _sprites;
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
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the electrode.</param>
    /// <param name="wave">The wave number; it decides the post's picture family and its colour.</param>
    public Electrode(SpriteSet sprites, IntVector2 position, int wave = 1)
    {
        _sprites = sprites;
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
        _shrivelTimer += ArcadeClock.UnitsPerPortTick;
        if (_shrivelTimer < ArcadeClock.Units(ShrivelSleepRomTicks[_shrivelStep]))
        {
            return;
        }

        _shrivelTimer -= ArcadeClock.Units(ShrivelSleepRomTicks[_shrivelStep]);

        _shrivelStep++;
        if (_shrivelStep >= ShrivelSleepRomTicks.Length)
        {
            LifeState = EntityLifeState.Dead;
            return;
        }
    }

    /// <summary>This electrode's picture: the live frame, or the current shrivel frame while it is dying.</summary>
    public Texture2D CurrentAnimationFrame
    {
        get
        {
            int baseFrame = FamilyIndex * GameplayConstants.PostPicturesPerFamily;
            int frame = LifeState == EntityLifeState.Dying ? baseFrame + _shrivelStep : baseFrame;
            return _sprites.ElectrodeFrames[frame];
        }
    }

    /// <summary>Draws the live or shrivel picture, as a solid silhouette in the wave's slot colour.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        _sprites.DrawSpriteSolid(spriteBatch, CurrentAnimationFrame, Bounds, _sprites.SlotColor(TintSlot));
    }
}
