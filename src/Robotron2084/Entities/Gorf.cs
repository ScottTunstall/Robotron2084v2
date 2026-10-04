using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>Gorf hops across the screen in a string of jumps, from one side to the other, dropping grunts as it goes. It does not shoot. It has no beat. The <see cref="PlayField"/> calls <see cref="Update"/> on nearly every tick, through <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. <see cref="_stepTimer"/> times its steps and <see cref="_animationTimer"/> times its animation frames (see <see cref="ArcadeClock"/>).</summary>
/// <remarks>
/// A new kind of robot of the author's own with no arcade routine behind it (notes §138.2). It starts off the screen, on a random side and at a random
/// height, and hops to the far side (<see cref="GorfPath"/>), then goes. Every hop is the same height, 16 pixels for now (<see cref="GorfTuning.HopRows"/>).
/// It stops three times on the way (<see cref="GorfTuning.DropStops"/>) and drops a handful of grunts side by side from its body, which fall to the ground, up to six at a stop.
/// How many is rolled as a spheroid rolls its enforcers: from the wave's <c>ENFNUM</c> (<see cref="LevelParameters.MaxDropsX2"/>), so it grows with the wave and the
/// difficulty; the level can only hold so many grunts, and a stop drops fewer, or none, when it is full. A Gorf that gets across is gone for good and scores nothing; one that is shot scores as a grunt does.
/// </remarks>
public sealed class Gorf : IExplodable, IRemovable
{
    /// <summary>The robot's own box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.GorfCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.GorfCollisionSize.Height));

    /// <summary>How many clock units pass between one step and the next. <see cref="_stepTimer"/> goes up by one port tick's worth of clock units each tick, and when it reaches this, a step is taken and this is subtracted from it.</summary>
    private static readonly int StepClockUnits = ArcadeClock.ToClockUnits(GorfTuning.StepRomFrames);

    /// <summary>How many clock units each animation frame is shown for. <see cref="_animationTimer"/> goes up by one port tick's worth of clock units each tick, and when it reaches this, the next animation frame is shown and this is subtracted from it.</summary>
    private static readonly int AnimationFrameClockUnits = ArcadeClock.ToClockUnits(GorfTuning.AnimationFrameRomFrames);

    /// <summary>How far each step goes sideways, in port pixels. It is added to or subtracted from the X of <see cref="_position"/> at each step, and it sets how many steps cross the playfield, which is stored in <see cref="_totalSteps"/>.</summary>
    private static readonly int StepPixels = ScreenSize.ToPortPixelsFromColumns(GorfTuning.StepColumns);

    /// <summary>How high every hop goes, in port pixels.</summary>
    private static readonly int HopPixels = ScreenSize.ToPortPixels(GorfTuning.HopRows);

    private readonly int _directionSignX;
    private readonly Queue<int> _dropSteps = new();
    private readonly int _maxDropsX2;
    private readonly Random _random;
    private readonly int _groundY;
    private readonly Rectangle _playfieldBounds;
    private readonly SpriteSet _sprites;
    private readonly int _totalSteps;
    private int _animationFrameIndex;
    private int _animationTimer;
    private IntVector2 _position;
    private int _stepCount;
    private int _stepInHop;
    private int _stepTimer;

    /// <summary>Creates a Gorf just off the screen, on a random side and at a random height, with its drops already rolled.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="random">The random source: the side, the height and the drops.</param>
    /// <param name="playfieldBounds">The inside of the wall, in port pixels. Gorf starts and ends outside it.</param>
    /// <param name="maxDropsX2">This wave's drop bound, rolled as a spheroid's is.</param>
    public Gorf(SpriteSet sprites, Random random, Rectangle playfieldBounds, int maxDropsX2)
    {
        _sprites = sprites;
        _playfieldBounds = playfieldBounds;
        _random = random;
        _maxDropsX2 = maxDropsX2;
        _directionSignX = random.Next(2) == 0 ? 1 : -1;
        int highest = playfieldBounds.Y + HopPixels;
        int lowest = Math.Max(highest, playfieldBounds.Bottom - CollisionSize.Height);
        _groundY = highest + random.Next(lowest - highest + 1);
        _position = new IntVector2(_directionSignX > 0 ? playfieldBounds.X - CollisionSize.Width : playfieldBounds.Right, _groundY);
        _totalSteps = (playfieldBounds.Width + CollisionSize.Width) / StepPixels;

        for (int stop = 1; stop <= GorfTuning.DropStops; stop++)
        {
            _dropSteps.Enqueue(stop * _totalSteps / (GorfTuning.DropStops + 1));
        }
    }

    /// <summary>The robot's own box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until shot or across the screen; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Top-left of the Gorf.</summary>
    public IntVector2 Position => _position;

    /// <summary>How many stops it has still to make to drop grunts (test hook).</summary>
    internal int DropStopsRemaining => _dropSteps.Count;

    /// <summary>Which of the two animation frames is showing, 0 or 1 (test hook).</summary>
    internal int AnimationFrameIndex => _animationFrameIndex;

    /// <summary>Gets the animation frame the Gorf is showing, for drawing and for the appear and explosion effects.</summary>
    /// <returns>The texture for the current frame.</returns>
    public Texture2D GetCurrentAnimationFrame() => _sprites.GorfAnimationFrames[_animationFrameIndex];

    /// <summary>Draws the part of the current animation frame that is inside the wall, so Gorf comes in from off the screen.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        Texture2D animationFrame = GetCurrentAnimationFrame();
        Rectangle drawn = BlitterDraw.DrawnRect(Bounds, animationFrame);
        Rectangle visible = Rectangle.Intersect(drawn, _playfieldBounds);
        if (visible.IsEmpty)
        {
            return;
        }

        int pixel = ScreenSize.ToPortPixels(1);
        var source = new Rectangle((visible.X - drawn.X) / pixel, (visible.Y - drawn.Y) / pixel, visible.Width / pixel, visible.Height / pixel);
        _sprites.Blitter.UsePassThrough();
        spriteBatch.Draw(animationFrame, visible, source, Color.White);
    }

    /// <summary>Kills the Gorf outright: no flash, no death animation.</summary>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Runs one tick: animates, and on each step moves along its hop and drops a grunt when one is due.</summary>
    /// <param name="gameTime">Unused — the steps are counted in ticks.</param>
    /// <param name="field">The playfield, which the dropped grunts are put on.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive() || field.RobotsFrozen)
        {
            return;
        }

        Animate();
        _stepTimer += ArcadeClock.UnitsPerPortTick;
        if (_stepTimer < StepClockUnits)
        {
            return;
        }

        _stepTimer -= StepClockUnits;
        Step(field);
    }

    private void Animate()
    {
        _animationTimer += ArcadeClock.UnitsPerPortTick;
        if (_animationTimer < AnimationFrameClockUnits)
        {
            return;
        }

        _animationTimer -= AnimationFrameClockUnits;
        _animationFrameIndex = (_animationFrameIndex + 1) % _sprites.GorfAnimationFrames.Length;
    }

    private void Step(PlayField field)
    {
        _stepCount++;
        _stepInHop++;
        int height = GorfPath.GetHopHeight(_stepInHop, GorfTuning.HopSteps, HopPixels);
        _position = new IntVector2(_position.X + (_directionSignX * StepPixels), _groundY - height);
        if (_stepInHop == GorfTuning.HopSteps)
        {
            _stepInHop = 0;
        }

        if (_dropSteps.Count > 0 && _stepCount >= _dropSteps.Peek())
        {
            _dropSteps.Dequeue();
            DropGrunts(field);
        }

        if (_stepCount >= _totalSteps)
        {
            Kill();
        }
    }

    /// <summary>Drops a handful of grunts side by side from Gorf's body, rolled as a spheroid rolls its enforcers: a roll from 1 to the wave's bound, halved and rounded up, and no more than six.</summary>
    /// <param name="field">The playfield, which puts the grunts on the field and holds them back when the level is full.</param>
    private void DropGrunts(PlayField field)
    {
        int count = Math.Min(GorfTuning.MaxGruntsPerDrop, (_random.Next(_maxDropsX2) + 2) / 2);
        int gruntWidth = ScreenSize.ToPortPixels(CollisionSizes.GruntCollisionSize.Width);
        int gruntHeight = ScreenSize.ToPortPixels(CollisionSizes.GruntCollisionSize.Height);
        int spacing = gruntWidth + GorfTuning.DropGapPixels;
        int centreX = _position.X + ((CollisionSize.Width - gruntWidth) / 2);
        int bodyY = _position.Y + ((CollisionSize.Height - gruntHeight) / 2);
        int groundY = _groundY + CollisionSize.Height - gruntHeight;

        for (int index = 0; index < count; index++)
        {
            int sideways = ((2 * index) - (count - 1)) * spacing / 2;
            field.SpawnGrunt(new IntVector2(centreX + sideways, bodyY), new IntVector2(centreX + sideways, groundY));
        }
    }
}
