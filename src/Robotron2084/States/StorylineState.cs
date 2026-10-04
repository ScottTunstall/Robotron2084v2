using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.AttractMode;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Graphics;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Level.Attract;
using Robotron2084.Palette;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
/// The arcade's attract MOVIE, the history page (<c>SPAGE</c> running <c>HISTO</c> after <c>FAMPAG</c>'s title, notes §123) —
/// the scripted storyline that plays after INTRO2
/// sits idle (notes §95/§96). It is the ROM's own HISTO page script run by
/// <see cref="AttractMovie"/>: the intro screen's text crawl ("ROBOTRON 2084 /
/// INSPIRED BY HIS NEVER ENDING QUEST FOR PROGRESS…"), then the hero, the family,
/// the grunts, the hulk, the spheroid/tank/enforcer scene, the brain's
/// reprogramming and the score electrodes, all on the title's solid $CC wall with the
/// player's score and men in the HUD.
///
/// Draw order follows the screen's own layering: wall, HUD, the title string (the
/// ROM prints it in SPGSUB and the page script never clears above row 48), the
/// character objects, then the story text and the score-row name popups on top.
/// </summary>
public sealed class StorylineState : IGameState, IAttractState
{
    /// <summary>ROM string 128, printed by `SPGSUB` and never cleared by the page script.</summary>
    internal const string TitleText = "ROBOTRON 2084";

    /// <summary>The movie interior, in canvas pixels: a strip that falls outside it is dropped (the ROM's clip).</summary>
    private static StripClip GetClip()
    {
        Rectangle bounds = PlayfieldLayout.GetInnerBounds();
        return new StripClip(bounds.Left, bounds.Right, bounds.Top, bounds.Bottom);
    }

    private readonly ButtonEdgeDetector _buttons = new();
    private readonly List<StripEffect> _explosions = [];
    private readonly IPlayerInputSource _humanInput;
    private readonly AttractMovie _movie;
    private readonly GameServices _services;
    private readonly GameSession _session;
    private readonly SpriteSet _sprites;
    private readonly PlayfieldWall _wall;

    public StorylineState(GameServices services, Random random)
    {
        _services = services;
        _sprites = services.Sprites;
        _humanInput = services.Input;

        _wall = new PlayfieldWall(PlayfieldLayout.GetInnerBounds(), new WallColorCycle());
        _session = GameSession.CreateNewGame(_humanInput, 1);
        _movie = new AttractMovie(AttractMovieData.Histo, random);
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        _wall.Draw(spriteBatch, _sprites.WallPixelSprite, _sprites.Blitter.GetSlotColour(AttractTuning.TitleWallSlot));
        ArcadeHud.DrawScoresAndMen(spriteBatch, _sprites, _session, PlayfieldLayout.GetInnerBounds(), showSpareMen: false);

        ArcadeHud.DrawCenteredLargeText(
            spriteBatch,
            _sprites,
            TitleText,
            HudLayout.ToPortY(AttractTuning.StoryTitleRow),
            HudLayout.HudScoreSlotCurrent);

        DrawObjects(spriteBatch);

        foreach (StripEffect explosion in _explosions)
        {
            explosion.Draw(spriteBatch);
        }

        foreach (MovieTextCell cell in _movie.PageMachine.TextCells)
        {
            int index = ArcadeText.GetGlyphIndex(cell.Character);
            if (index >= 0 && index < _sprites.FontLarge.Length)
            {
                _sprites.Blitter.DrawGlyphSlot(
                    spriteBatch,
                    _sprites.FontLarge,
                    index,
                    HudLayout.ToPortX(cell.X),
                    HudLayout.ToPortY(cell.Y),
                    cell.Slot);
            }
        }

        if (_movie.PageMachine.Message is { } message)
        {
            _sprites.TextRenderer.DrawSmallFontText(
                spriteBatch,
                message.Text,
                HudLayout.ToPortX(message.X),
                HudLayout.ToPortY(message.Y),
                message.Slot);
        }
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        // A human at the coin door takes the machine back to the title, exactly as
        // the arcade's coin/start handler does while attract is running.
        PlayerInputState human = _humanInput.Poll();
        if (_buttons.Advance(human).AnyPressed)
        {
            manager.TransitionTo(new TitleScreenState(_services));
            return;
        }

        _movie.Update(gameTime);

        // Page Up held (dev key, notes §97): run the movie's ROM frame clock extra
        // times so a later scene can be reached without waiting out the text
        // crawl. The clock itself is untouched — this is the same accumulator,
        // just stepped more often.
        if (DevKeys.AttractFastForward)
        {
            for (int i = 1; i < DevKeys.AttractFastForwardMultiplier; i++)
            {
                _movie.Update(gameTime);
            }
        }

        foreach (MovieExplosion exploded in _movie.ObjectMachine.DrainExplosions())
        {
            // EXPP: the explosion takes the sprite the object was showing and the
            // direction of a pure HORIZONTAL laser ($FF00), which the Gospel's
            // dispatch turns into the ROW-splitting fan.
            Texture2D? animationFrame = MovieAnimationFrames.Resolve(_sprites, exploded.Animation, exploded.AnimationFrameIndex);
            if (animationFrame is null)
            {
                continue;
            }

            _explosions.Add(StripEffect.CreateExplosion(
                new MovieExplosionSource(
                    animationFrame,
                    new Rectangle(
                        HudLayout.ToPortX(exploded.Column * 2),
                        HudLayout.ToPortY(exploded.Row),
                        ScreenSize.ToPortPixels(animationFrame.Width),
                        ScreenSize.ToPortPixels(animationFrame.Height))),
                Direction8.Left,
                GetClip()));
        }

        for (int i = _explosions.Count - 1; i >= 0; i--)
        {
            _explosions[i].Update(gameTime);
            if (!_explosions[i].IsAlive())
            {
                _explosions.RemoveAt(i);
            }
        }

        // HISTO ends with DONE2, which the ROM answers with RUNIT: the machine
        // starts its phony-player game.
        if (_movie.IsFinished)
        {
            manager.TransitionTo(new AttractState(_services));
        }
    }

    private void DrawObjects(SpriteBatch spriteBatch)
    {
        foreach (MovieObject item in _movie.ObjectMachine.Objects)
        {
            if (item.IsDead || (!item.IsOnList && !item.IsMonoActive))
            {
                continue;
            }

            if (item.IsLaser)
            {
                DrawLaser(spriteBatch, item);
                continue;
            }

            if (item.Descriptor is not { } descriptor)
            {
                continue;
            }

            Texture2D? animationFrame = MovieAnimationFrames.Resolve(_sprites, descriptor.Animation, item.AnimationFrameIndex);
            if (animationFrame is null)
            {
                continue;
            }

            var bounds = new Rectangle(
                HudLayout.ToPortX(item.ArcadeX),
                HudLayout.ToPortY(item.ArcadeY),
                ScreenSize.ToPortPixels(animationFrame.Width),
                ScreenSize.ToPortPixels(animationFrame.Height));

            if (item.IsMonoActive)
            {
                DrawMonoBox(spriteBatch, item, animationFrame, bounds);
                continue;
            }

            _sprites.Blitter.DrawSprite(spriteBatch, animationFrame, bounds, Color.White);
        }
    }

    /// <summary>Draws a laser bolt: <c>LASPIC</c>, the rotating laser table's horizontal bar.</summary>
    private void DrawLaser(SpriteBatch spriteBatch, MovieObject item)
    {
        int x = HudLayout.ToPortX(item.ArcadeX);
        int y = HudLayout.ToPortY(item.ArcadeY);
        var bolt = new Rectangle(x, y, ScreenSize.ToPortPixels(_sprites.LaserBarSprite.Width), ScreenSize.ToPortPixels(_sprites.LaserBarSprite.Height));
        _sprites.Blitter.DrawSpriteSolid(spriteBatch, _sprites.LaserBarSprite, bolt, _sprites.Blitter.GetSlotColour(PlayerTuning.LaserSlot));
    }

    /// <summary>
    /// ROM <c>OPON</c>: the box in the first colour, the object's SHAPE in the second, and — for the
    /// brain's box — the brain's own sprite over the top (the ROM's second blit, <c>JMP $D018</c>, notes §72.1).
    /// </summary>
    private void DrawMonoBox(SpriteBatch spriteBatch, MovieObject item, Texture2D animationFrame, Rectangle bounds)
    {
        if (item.MonoBoxSlot != 0)
        {
            _sprites.Blitter.DrawSolidRectangle(spriteBatch, bounds, _sprites.Blitter.GetSlotColour(item.MonoBoxSlot));
        }

        _sprites.Blitter.DrawSpriteSolid(spriteBatch, animationFrame, bounds, _sprites.Blitter.GetSlotColour(item.MonoSilhouetteSlot));
        if (item.ShowsMonoBrain)
        {
            _sprites.Blitter.DrawSprite(spriteBatch, animationFrame, bounds, Color.White);
        }
    }

    /// <summary>
    /// The EXP opcode's dead object: the ROM's `KILLOF` leaves the object's sprite
    /// in place and `EXPP` sets the explosion's row to ACTHIT+6, so the fan's rect is
    /// that sprite at (the object's column, row 166).
    /// </summary>
    private sealed class MovieExplosionSource : IExplodable
    {
        private readonly Texture2D _animationFrame;
        private readonly Rectangle _bounds;

        public MovieExplosionSource(Texture2D animationFrame, Rectangle bounds)
        {
            _animationFrame = animationFrame;
            _bounds = bounds;
        }

        public Rectangle Bounds => _bounds;
        public Texture2D GetCurrentAnimationFrame() => _animationFrame;
        public Rectangle ExplosionBounds => _bounds;
        public EntityLifeState LifeState => EntityLifeState.Dead;
        public IntVector2 Position => new(_bounds.X, _bounds.Y);

        public void Draw(SpriteBatch spriteBatch)
        {
        }

        public void Update(GameTime gameTime, PlayField field)
        {
        }
    }
}
