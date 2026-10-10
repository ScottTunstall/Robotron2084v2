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
/// It has no beat. The <see cref="PlayField"/> calls <see cref="Update"/> on every tick, through
/// <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. The one time it does not is during the
/// short freeze just after the player is killed. <see cref="_shrivelTimer"/> times each stage of its shrivelling
/// (see <see cref="ArcadeClock"/>).
///
/// <list type="bullet">
/// <item>Original source: <c>RRP8.ASM</c>, routine <c>PSTKIL</c> (hands off to <c>PKPROC</c>)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$3AE0</c> (<c>ELECTRODE_DEATH</c>)</item>
/// </list>
/// </remarks>
public sealed class Electrode : IEntity, IAnimationFrameSource, IRemovable
{
    /// <summary>How big the electrode is, in port pixels. It is the size of the electrode's sprite, and it is used to tell what touches the electrode.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.ElectrodeCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.ElectrodeCollisionSize.Height));

    /// <summary>How long each shrivel animation frame is shown for, in 50ths of a second.</summary>
    private static readonly int[] ShrivelSleepRomFrames = [6, 3, 2];

    private readonly SpriteSet _sprites;
    private readonly int _wave;
    private IntVector2 _position;
    private int _shrivelStep;
    private int _shrivelTimer;

    /// <summary>Makes an electrode for the given wave.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where the electrode's top-left corner is.</param>
    /// <param name="wave">The wave number. It decides which shape of electrode is drawn, and in which palette slot.</param>
    public Electrode(SpriteSet sprites, IntVector2 position, int wave = 1)
    {
        _sprites = sprites;
        _position = position;
        _wave = wave;
    }

    /// <summary>The box the electrode takes up on the screen. It is used to tell what touches the electrode.</summary>
    public Rectangle GetBounds() => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>The animation frame the electrode is showing: its normal one, or one of the shrivel ones while it is dying.</summary>
    public Texture2D GetCurrentAnimationFrame()
    {
        int baseFrame = GetVariantIndex() * WavePaletteTables.ElectrodeAnimationFramesPerVariant;
        int frame = this.IsDying() ? baseFrame + _shrivelStep : baseFrame;
        return _sprites.ElectrodeAnimationFrames[frame];
    }

    /// <summary>Alive until something kills it, then Dying while it shrivels.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the electrode's top-left corner is. An electrode never moves.</summary>
    public IntVector2 Position => _position;

    /// <summary>Which shape of electrode is drawn. The wave number decides it.</summary>
    /// <remarks>The arcade gives names to only the first four shapes. Its own table goes through 9 shapes in every 10 waves.</remarks>
    internal int GetVariantIndex() => WavePaletteTables.GetElectrodeVariant(_wave);

    /// <summary>The palette slot the electrode is drawn in on this wave. It may be a slot whose colour keeps changing.</summary>
    internal int GetTintSlot() => WavePaletteTables.GetElectrodeSlot(_wave);

    /// <summary>Draws the electrode's animation frame as a shape filled with one colour, from this wave's palette slot.</summary>
    /// <param name="spriteBatch">What the electrode is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (this.IsDead())
        {
            return;
        }

        _sprites.Blitter.DrawSpriteSolid(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), _sprites.Blitter.GetSlotColour(GetTintSlot()));
    }

    /// <summary>Starts the electrode shrivelling. It does nothing if the electrode is not alive.</summary>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dying;
        _shrivelStep = 0;
        _shrivelTimer = 0; // The first shrivel animation frame is shown for its full time (notes §52).
    }

    /// <summary>Runs one tick of the shrivelling. Each shrivel animation frame is shown for its own length of time, and after the last one the electrode is gone.</summary>
    /// <param name="gameTime">Not used. The electrode counts ticks.</param>
    /// <param name="field">Not used.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsDying())
        {
            return;
        }

        // Wait until it is time for the next shrivel animation frame (see ArcadeClock).
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
