using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>An electrode is a spiky pillar standing in the playfield. It cannot move, but it shrivels up and disappears when something destroys it.</summary>
/// <seealso cref="PlayField"/>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRP8.ASM</c>, routine <c>PSTKIL</c> (hands off to <c>PKPROC</c>)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$3AE0</c> (<c>ELECTRODE_DEATH</c>)</item>
/// </list>
/// </remarks>
public sealed class Electrode : IEntity, IAnimationFrameSource, IRemovable
{
    /// <summary>The electrode sprite's own 10x9 arcade px box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.ElectrodeCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.ElectrodeCollisionSize.Height));

    /// <summary>How long each shrivel animation frame is held, in ROM frames.</summary>
    private static readonly int[] ShrivelSleepRomFrames = [6, 3, 2];

    private readonly SpriteSet _sprites;
    private readonly int _wave;
    private IntVector2 _position;
    private int _shrivelStep;
    private int _shrivelTimer;

    /// <summary>Creates an electrode for the given wave.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the electrode.</param>
    /// <param name="wave">The wave number; it decides the electrode's animation frame variant and its colour.</param>
    public Electrode(SpriteSet sprites, IntVector2 position, int wave = 1)
    {
        _sprites = sprites;
        _position = position;
        _wave = wave;
    }

    /// <summary>The electrode sprite's own 10x9 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>This electrode's animation frame: the live frame, or the current shrivel frame while it is dying.</summary>
    public Texture2D GetCurrentAnimationFrame()
    {
        int baseFrame = GetVariantIndex() * WavePaletteTables.ElectrodeAnimationFramesPerVariant;
        int frame = this.IsDying() ? baseFrame + _shrivelStep : baseFrame;
        return _sprites.ElectrodeAnimationFrames[frame];
    }

    /// <summary>Alive until something kills it; Dying while the shrivel plays.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Top-left of the electrode; it never moves.</summary>
    public IntVector2 Position => _position;

    /// <summary>Which of the 9 electrode animation-frame sets this electrode draws, chosen by wave number.</summary>
    /// <remarks>The arcade names only the first four shapes; its own table cycles 9 sets over 10 waves.</remarks>
    internal int GetVariantIndex() => WavePaletteTables.GetElectrodeVariant(_wave);

    /// <summary>The palette slot the electrode is drawn in for this wave; the slot's colour may cycle.</summary>
    internal int GetTintSlot() => WavePaletteTables.GetElectrodeSlot(_wave);

    /// <summary>Draws the live or shrivel animation frame, as a solid silhouette in the wave's slot colour.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (this.IsDead())
        {
            return;
        }

        _sprites.Blitter.DrawSpriteSolid(spriteBatch, GetCurrentAnimationFrame(), Bounds, _sprites.Blitter.GetSlotColour(GetTintSlot()));
    }

    /// <summary>Starts the shrivel; does nothing unless the electrode is alive.</summary>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dying;
        _shrivelStep = 0;
        _shrivelTimer = 0; // the first sleep is a full period (notes §52 clock)
    }

    /// <summary>Plays the shrivel: one animation frame per hold, then the electrode is removed.</summary>
    /// <param name="gameTime">Unused — the holds are counted in ticks.</param>
    /// <param name="field">Unused.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsDying())
        {
            return;
        }

        // Counts up to the next shrivel animation frame: 5 per tick, 6 per arcade frame.
        _shrivelTimer += ArcadeClock.UnitsPerPortTick;
        if (_shrivelTimer < ArcadeClock.ToClockUnits(ShrivelSleepRomFrames[_shrivelStep]))
        {
            return;
        }

        _shrivelTimer -= ArcadeClock.ToClockUnits(ShrivelSleepRomFrames[_shrivelStep]);

        _shrivelStep++;
        if (_shrivelStep >= ShrivelSleepRomFrames.Length)
        {
            LifeState = EntityLifeState.Dead;
        }
    }
}
