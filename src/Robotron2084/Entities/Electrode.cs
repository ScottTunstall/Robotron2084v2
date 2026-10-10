using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
///     An electrode is a spiky pillar standing in the playfield. It cannot move, but it shrivels up and disappears
///     when something destroys it.
/// </summary>
/// <seealso cref="PlayField" />
/// <remarks>
///     It has no beat. The <see cref="PlayField" /> calls <see cref="Update" /> on every tick, through
///     <see cref="FieldEntities" /> and <see cref="PlayField.UpdateEntity" />. The one time it does not is during the
///     short freeze just after the player is killed. <see cref="_shrivelTimer" /> times each stage of its shrivelling
///     (see <see cref="ArcadeClock" />).
///     <list type="bullet">
///         <item>Original source: <c>RRP8.ASM</c>, routine <c>PSTKIL</c> (hands off to <c>PKPROC</c>)</item>
///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>$3AE0</c> (<c>ELECTRODE_DEATH</c>)</item>
///     </list>
/// </remarks>
public sealed class Electrode : IEntity, IAnimationFrameSource, IRemovable
{
    /// <summary>How long each shrivel animation frame is shown for.</summary>
    private static readonly int[] ShrivelSleepRomFrames = [6, 3, 2];

    /// <summary>
    ///     How big the electrode is, in port pixels. It is the size of the sprite for this wave's shape of electrode, and
    ///     it is used to tell what touches the electrode.
    /// </summary>
    private readonly (int Width, int Height) _collisionSize;

    private readonly SpriteSet _sprites;
    private readonly int _wave;
    private readonly IntVector2 _position;
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
        _collisionSize = GetCollisionSize(wave);
    }

    /// <summary>The animation frame the electrode is showing: its normal one, or one of the shrivel ones while it is dying.</summary>
    public Texture2D GetCurrentAnimationFrame()
    {
        var baseFrame = GetVariantIndex() * WavePaletteTables.ElectrodeAnimationFramesPerVariant;
        var frame = this.IsDying() ? baseFrame + _shrivelStep : baseFrame;
        return _sprites.ElectrodeAnimationFrames[frame];
    }

    /// <summary>The box the electrode takes up on the screen. It is used to tell what touches the electrode.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, _collisionSize.Width, _collisionSize.Height);
    }

    /// <summary>Alive until something kills it, then Dying while it shrivels.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the electrode's top-left corner is. An electrode never moves.</summary>
    public IntVector2 Position => _position;

    /// <summary>Draws the electrode's animation frame as a shape filled with one colour, from this wave's palette slot.</summary>
    /// <param name="spriteBatch">What the electrode is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (this.IsDead()) return;

        _sprites.Blitter.DrawSpriteSolid(spriteBatch, GetCurrentAnimationFrame(), GetBounds(),
            _sprites.Blitter.GetSlotColour(GetTintSlot()));
    }

    /// <summary>
    ///     Runs one tick of the shrivelling. Each shrivel animation frame is shown for its own length of time, and after
    ///     the last one the electrode is gone.
    /// </summary>
    /// <param name="gameTime">Not used. The electrode counts ticks.</param>
    /// <param name="field">Not used.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsDying()) return;

        // Wait until it is time for the next shrivel animation frame (see ArcadeClock).
        _shrivelTimer += ArcadeClock.UnitsPerPortTick;
        if (_shrivelTimer < ArcadeClock.ToClockUnits(ShrivelSleepRomFrames[_shrivelStep])) return;

        _shrivelTimer -= ArcadeClock.ToClockUnits(ShrivelSleepRomFrames[_shrivelStep]);

        _shrivelStep++;
        if (_shrivelStep >= ShrivelSleepRomFrames.Length) LifeState = EntityLifeState.Dead;
    }

    /// <summary>Starts the electrode shrivelling. It does nothing if the electrode is not alive.</summary>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dying;
        _shrivelStep = 0;
        _shrivelTimer = 0; // The first shrivel animation frame is shown for its full time (notes §52).
    }

    /// <summary>Works out how big a wave's electrodes are, in port pixels. Each shape of electrode has its own size.</summary>
    /// <param name="wave">The wave number, which decides the shape.</param>
    /// <returns>The width and the height, in port pixels.</returns>
    /// <remarks>
    ///     Disassembly: the arcade takes the size from the shape's own animation frame (<c>$3B05</c> onwards), so the
    ///     "2084" electrode of every tenth wave is wider and shorter than the rest.
    /// </remarks>
    internal static (int Width, int Height) GetCollisionSize(int wave)
    {
        var (width, height) =
            CollisionSizes.ElectrodeCollisionSizeByVariant[WavePaletteTables.GetElectrodeVariant(wave)];
        return (ScreenSize.ToPortPixelsFromArcadePixels(width), ScreenSize.ToPortPixelsFromArcadePixels(height));
    }

    /// <summary>Which shape of electrode is drawn. The wave number decides it.</summary>
    /// <remarks>The arcade gives names to only the first four shapes. Its own table goes through 9 shapes in every 10 waves.</remarks>
    internal int GetVariantIndex()
    {
        return WavePaletteTables.GetElectrodeVariant(_wave);
    }

    /// <summary>The palette slot the electrode is drawn in on this wave. It may be a slot whose colour keeps changing.</summary>
    internal int GetTintSlot()
    {
        return WavePaletteTables.GetElectrodeSlot(_wave);
    }
}
