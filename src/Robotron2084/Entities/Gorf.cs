using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
///     Gorf hops across the screen in a string of jumps, from one side to the other, dropping grunts as it goes. It
///     does not shoot.
/// </summary>
/// <remarks>
///     It has no beat. The <see cref="PlayField" /> calls <see cref="Update" /> on every tick, through
///     <see cref="FieldEntities" /> and <see cref="PlayField.UpdateEntity" />.
///     The one time it does not is during the short freeze just after the player is killed.
///     <see cref="_stepTimer" /> times its steps
///     and <see cref="_animationTimer" /> times its animation frames (see <see cref="ArcadeClock" />).
///     Gorf is the author's own robot. It is not in the arcade game (notes §138.2). It starts off the screen, on a random
///     side and
///     at a random height. It hops to the far side (<see cref="GorfPath" />) and then it is gone. Every hop is the same
///     height
///     (<see cref="GorfTuning.HopRows" />).
///     At a few points on the way (<see cref="GorfTuning.DropStops" />) it drops some grunts side by side, which fall to
///     the ground.
///     How many it drops is picked at random, the way a spheroid picks how many enforcers to drop, from the wave's
///     <c>ENFNUM</c>
///     (<see cref="LevelParameters.MaxDropsX2" />). So later waves and harder settings drop more. A level can only hold so
///     many
///     grunts, so Gorf drops fewer, or none, when the level is full.
///     A Gorf that gets across scores nothing. A Gorf that is shot scores the same as a grunt.
/// </remarks>
public sealed class Gorf : IExplodable, IRemovable
{
    /// <summary>How big Gorf is, in port pixels. This size is used to tell what Gorf touches.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GorfCollisionSize.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GorfCollisionSize.Height));

    /// <summary>
    ///     The time from one step to the next, in clock units (see <see cref="ArcadeClock" />). <see cref="_stepTimer" />
    ///     counts up to this. When it gets there, a step is taken and this is taken off it.
    /// </summary>
    private static readonly int StepClockUnits = ArcadeClock.ToClockUnits(GorfTuning.StepRomFrames);

    /// <summary>
    ///     How long each animation frame is shown for, in clock units (see <see cref="ArcadeClock" />).
    ///     <see cref="_animationTimer" /> counts up to this. When it gets there, the next animation frame is shown and this is
    ///     taken off it.
    /// </summary>
    private static readonly int AnimationFrameClockUnits = ArcadeClock.ToClockUnits(GorfTuning.AnimationFrameRomFrames);

    /// <summary>
    ///     How far Gorf goes sideways on each step, in port pixels. Each step moves <see cref="_position" /> this far
    ///     left or right. It also decides how many steps it takes to cross the playfield (<see cref="_totalSteps" />).
    /// </summary>
    private static readonly int StepPixels = ScreenSize.ToPortPixelsFromColumns(GorfTuning.StepColumns);

    /// <summary>How high every hop goes, in port pixels.</summary>
    private static readonly int HopPixels = ScreenSize.ToPortPixelsFromArcadePixels(GorfTuning.HopRows);

    private readonly int _directionSignX;
    private readonly Queue<int> _dropSteps = new();
    private readonly int _groundY;
    private readonly int _maxDropsX2;
    private readonly Rectangle _playfieldBounds;
    private readonly Random _random;
    private readonly SpriteSet _sprites;
    private readonly int _totalSteps;
    private int _animationTimer;
    private IntVector2 _position;
    private int _stepCount;
    private int _stepInHop;
    private int _stepTimer;

    /// <summary>
    ///     Makes a Gorf just off the screen, on a random side and at a random height, and works out where on its way it
    ///     will drop grunts.
    /// </summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="random">Where its random numbers come from. They pick the side, the height and how many grunts it drops.</param>
    /// <param name="playfieldBounds">The inside of the wall, in port pixels. Gorf starts and ends outside it.</param>
    /// <param name="maxDropsX2">
    ///     This wave's limit on how many grunts it drops at once. The number is picked the way a spheroid
    ///     picks its enforcers.
    /// </param>
    public Gorf(SpriteSet sprites, Random random, Rectangle playfieldBounds, int maxDropsX2)
    {
        _sprites = sprites;
        _playfieldBounds = playfieldBounds;
        _random = random;
        _maxDropsX2 = maxDropsX2;
        _directionSignX = random.Next(2) == 0 ? 1 : -1;
        var highest = playfieldBounds.Y + HopPixels;
        var lowest = Math.Max(highest, playfieldBounds.Bottom - CollisionSize.Height);
        _groundY = highest + random.Next(lowest - highest + 1);
        _position = new IntVector2(
            _directionSignX > 0 ? playfieldBounds.X - CollisionSize.Width : playfieldBounds.Right, _groundY);
        _totalSteps = (playfieldBounds.Width + CollisionSize.Width) / StepPixels;

        for (var stop = 1; stop <= GorfTuning.DropStops; stop++)
            _dropSteps.Enqueue(stop * _totalSteps / (GorfTuning.DropStops + 1));
    }

    /// <summary>Which of the two animation frames is showing, 0 or 1. Only tests use this.</summary>
    internal int AnimationFrameIndex { get; private set; }

    /// <summary>The box Gorf takes up on the screen. It is used to tell what Gorf touches.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);
    }

    /// <summary>
    ///     Alive until it is shot or gets across the screen. It is never Dying, because it has no death animation (see
    ///     <see cref="Kill" />).
    /// </summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where Gorf's top-left corner is.</summary>
    public IntVector2 Position => _position;

    /// <summary>Gets the animation frame the Gorf is showing, for drawing and for the appear and explosion effects.</summary>
    /// <returns>The animation frame that is showing.</returns>
    public Texture2D GetCurrentAnimationFrame()
    {
        return _sprites.GorfAnimationFrames[AnimationFrameIndex];
    }

    /// <summary>Draws the part of the current animation frame that is inside the wall, so Gorf comes in from off the screen.</summary>
    /// <param name="spriteBatch">What Gorf is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        var animationFrame = GetCurrentAnimationFrame();
        var drawn = BlitterDraw.DrawnRect(GetBounds(), animationFrame);
        var visible = Rectangle.Intersect(drawn, _playfieldBounds);
        if (visible.IsEmpty) return;

        var pixel = ScreenSize.ToPortPixelsFromArcadePixels(1);
        var source = new Rectangle((visible.X - drawn.X) / pixel, (visible.Y - drawn.Y) / pixel, visible.Width / pixel,
            visible.Height / pixel);
        _sprites.Blitter.UsePassThrough();
        spriteBatch.Draw(animationFrame, visible, source, Color.White);
    }

    /// <summary>
    ///     Runs one tick. Gorf animates. When a step is due, it moves along its hop, and drops grunts if it has reached a
    ///     drop point.
    /// </summary>
    /// <param name="gameTime">Not used. Gorf counts ticks.</param>
    /// <param name="field">The playfield, which the dropped grunts are put on.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive() || field.RobotsFrozen()) return;

        Animate();
        _stepTimer += ArcadeClock.UnitsPerPortTick;
        if (_stepTimer < StepClockUnits) return;

        _stepTimer -= StepClockUnits;
        Step(field);
    }

    /// <summary>Kills Gorf at once, with no flash and no death animation.</summary>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>How many more times it will drop grunts. Only tests use this.</summary>
    internal int GetDropStopsRemaining()
    {
        return _dropSteps.Count;
    }

    /// <summary>Shows the next animation frame when it is time to.</summary>
    private void Animate()
    {
        _animationTimer += ArcadeClock.UnitsPerPortTick;
        if (_animationTimer < AnimationFrameClockUnits) return;

        _animationTimer -= AnimationFrameClockUnits;
        AnimationFrameIndex = (AnimationFrameIndex + 1) % _sprites.GorfAnimationFrames.Length;
    }

    /// <summary>
    ///     Takes one step along the hop. Gorf drops grunts if a drop is due, and it is gone once it has crossed the
    ///     playfield.
    /// </summary>
    /// <param name="field">The playfield, which the dropped grunts are put on.</param>
    private void Step(PlayField field)
    {
        _stepCount++;
        _stepInHop++;
        var height = GorfPath.GetHopHeight(_stepInHop, GorfTuning.HopSteps, HopPixels);
        _position = new IntVector2(_position.X + _directionSignX * StepPixels, _groundY - height);
        if (_stepInHop == GorfTuning.HopSteps) _stepInHop = 0;

        if (_dropSteps.Count > 0 && _stepCount >= _dropSteps.Peek())
        {
            _dropSteps.Dequeue();
            DropGrunts(field);
        }

        if (_stepCount >= _totalSteps) Kill();
    }

    /// <summary>
    ///     Drops some grunts side by side from Gorf's body. How many is picked the way a spheroid picks its enforcers: a
    ///     random number from 1 up to the wave's limit, halved and rounded up. It is never more than
    ///     <see cref="GorfTuning.MaxGruntsPerDrop" />.
    /// </summary>
    /// <param name="field">The playfield, which puts the grunts on the field and holds them back when the level is full.</param>
    private void DropGrunts(PlayField field)
    {
        var count = Math.Min(GorfTuning.MaxGruntsPerDrop, (_random.Next(_maxDropsX2) + 2) / 2);
        var gruntWidth = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Width);
        var gruntHeight = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Height);
        var spacing = gruntWidth + GorfTuning.DropGapPixels;
        var centreX = _position.X + (CollisionSize.Width - gruntWidth) / 2;
        var bodyY = _position.Y + (CollisionSize.Height - gruntHeight) / 2;
        var groundY = _groundY + CollisionSize.Height - gruntHeight;

        for (var index = 0; index < count; index++)
        {
            var sideways = (2 * index - (count - 1)) * spacing / 2;
            field.SpawnGrunt(new IntVector2(centreX + sideways, bodyY), new IntVector2(centreX + sideways, groundY));
        }
    }
}
