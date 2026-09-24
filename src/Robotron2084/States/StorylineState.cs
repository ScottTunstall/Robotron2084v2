using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Level.Attract;
using Robotron2084.Persistence;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
/// The arcade's attract MOVIE — the scripted storyline that plays after the title
/// screen sits idle (notes §95/§96). It is the ROM's own HISTO page script run by
/// <see cref="AttractMovie"/>: the intro screen's text crawl ("ROBOTRON 2084 /
/// INSPIRED BY HIS NEVER ENDING QUEST FOR PROGRESS…"), then the hero, the family,
/// the grunts, the hulk, the spheroid/tank/enforcer scene, the brain's
/// reprogramming and the score posts, all on the title's solid $CC wall with the
/// player's score and men in the HUD.
///
/// Draw order follows the screen's own layering: wall, HUD, the title string (the
/// ROM prints it in SPGSUB and the page script never clears above row 48), the
/// character objects, then the story text and the score-row name popups on top.
/// </summary>
public sealed class StorylineState : IGameState, IAttractState
{
    private static readonly int Margin = ScreenSize.Scaled(GameplayConstants.PlayfieldMarginSpecPixels);
    private static readonly Rectangle InnerBounds = new(Margin, Margin, ScreenSize.Width - 2 * Margin, ScreenSize.Height - 2 * Margin);
    private static readonly StripClip Clip = new(InnerBounds.Left, InnerBounds.Right, InnerBounds.Top, InnerBounds.Bottom);

    private readonly SpriteSet _sprites;
    private readonly HighScoreStore _highScores;
    private readonly IPlayerInputSource _humanInput;
    private readonly GameServices _services;
    private readonly PlayfieldWall _wall;
    private readonly GameSession _session;
    private readonly AttractMovie _movie;
    private readonly List<Explosion> _explosions = [];
    private bool _previousFire;
    private bool _previousStartOne;
    private bool _previousStartTwo;

    public StorylineState(GameServices services, Random random)
    {
        _services = services;
        _sprites = services.Sprites;
        _highScores = services.HighScores;
        _humanInput = services.Input;

        _wall = new PlayfieldWall(InnerBounds, new WallColorCycle(
            GameplayConstants.DefaultWallPalette,
            TimeSpan.FromMilliseconds(GameplayConstants.WallStepDurationMilliseconds)));
        _session = GameSession.NewGame(_humanInput, 1);
        _movie = new AttractMovie(AttractMovieData.Histo, random);
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        // A human at the coin door takes the machine back to the title, exactly as
        // the arcade's coin/start handler does while attract is running.
        PlayerInputState human = _humanInput.Poll();
        if ((human.FirePressed && !_previousFire) ||
            (human.StartOnePlayerPressed && !_previousStartOne) ||
            (human.StartTwoPlayersPressed && !_previousStartTwo))
        {
            manager.TransitionTo(new TitleScreenState(_services));
            return;
        }

        _previousFire = human.FirePressed;
        _previousStartOne = human.StartOnePlayerPressed;
        _previousStartTwo = human.StartTwoPlayersPressed;

        _movie.Update(gameTime);

        // F3 held (dev key, notes §97): run the movie's ROM frame clock extra
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

        foreach (MovieExplosion exploded in _movie.Objects.DrainExplosions())
        {
            // EXPP: the explosion takes the picture the object was showing and the
            // direction of a pure HORIZONTAL laser ($FF00), which the Gospel's
            // dispatch turns into the ROW-splitting fan.
            Texture2D? animationFrame = MovieAnimationFrames.Resolve(_sprites, exploded.Animation, exploded.ImageIndex);
            if (animationFrame is null)
            {
                continue;
            }

            _explosions.Add(Explosion.StartExplosion(
                new MovieExplosionSource(
                    animationFrame,
                    new Rectangle(
                        GameplayConstants.ArcadeX(exploded.Column * 2),
                        GameplayConstants.ArcadeY(exploded.Row),
                        ScreenSize.Scaled(animationFrame.Width),
                        ScreenSize.Scaled(animationFrame.Height))),
                Direction8.Left,
                Clip));
        }

        for (int i = _explosions.Count - 1; i >= 0; i--)
        {
            _explosions[i].Update(gameTime);
            if (_explosions[i].LifeState != EntityLifeState.Alive)
            {
                _explosions.RemoveAt(i);
            }
        }

        // HISTO ends with DONE2, which the ROM answers with RUNIT: the machine
        // starts its phony-player game.
        if (_movie.Finished)
        {
            manager.TransitionTo(new AttractState(_services));
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        _wall.Draw(spriteBatch, _sprites.WallPixel, _sprites.SlotColor(GameplayConstants.TitleWallSlot));
        ArcadeHud.DrawScoresAndMen(spriteBatch, _sprites, _session, InnerBounds);

        ArcadeHud.DrawCenteredLargeText(
            spriteBatch,
            _sprites,
            TitleText,
            GameplayConstants.ArcadeY(GameplayConstants.StoryTitleRow),
            GameplayConstants.HudScoreSlotCurrent);

        DrawObjects(spriteBatch);

        foreach (Explosion explosion in _explosions)
        {
            explosion.Draw(spriteBatch);
        }

        foreach (MovieTextCell cell in _movie.Page.Text)
        {
            int index = SpriteSet.GlyphIndex(cell.Character);
            if (index >= 0 && index < _sprites.FontLarge.Length)
            {
                _sprites.DrawGlyphSlot(
                    spriteBatch,
                    _sprites.FontLarge,
                    index,
                    GameplayConstants.ArcadeX(cell.X),
                    GameplayConstants.ArcadeY(cell.Y),
                    cell.Slot);
            }
        }

        if (_movie.Page.Message is { } message)
        {
            _sprites.DrawSmallFontText(
                spriteBatch,
                message.Text,
                GameplayConstants.ArcadeX(message.X),
                GameplayConstants.ArcadeY(message.Y),
                message.Slot);
        }
    }

    /// <summary>ROM string 128, printed by `SPGSUB` and never cleared by the page script.</summary>
    internal const string TitleText = "ROBOTRON 2084";

    private void DrawObjects(SpriteBatch spriteBatch)
    {
        foreach (MovieObject item in _movie.Objects.Objects)
        {
            if (item.Dead || (!item.OnList && !item.MonoActive))
            {
                continue;
            }

            if (item.IsLaser)
            {
                // LASPIC: the rotating laser table's horizontal bar.
                int x = GameplayConstants.ArcadeX(item.ArcadeX);
                int y = GameplayConstants.ArcadeY(item.ArcadeY);
                spriteBatch.Draw(
                    _sprites.LaserBar,
                    new Rectangle(x, y, ScreenSize.Scaled(_sprites.LaserBar.Width), ScreenSize.Scaled(_sprites.LaserBar.Height)),
                    Color.White);
                continue;
            }

            if (item.Descriptor is not { } descriptor)
            {
                continue;
            }

            Texture2D? animationFrame = MovieAnimationFrames.Resolve(_sprites, descriptor.Animation, item.ImageIndex);
            if (animationFrame is null)
            {
                continue;
            }

            var bounds = new Rectangle(
                GameplayConstants.ArcadeX(item.ArcadeX),
                GameplayConstants.ArcadeY(item.ArcadeY),
                ScreenSize.Scaled(animationFrame.Width),
                ScreenSize.Scaled(animationFrame.Height));

            if (item.MonoActive)
            {
                // ROM OPON: the box in the first colour, the object's SHAPE in the
                // second, and — for the brain's box — the brain's own picture over
                // the top (the ROM's second blit, `JMP $D018`, notes §72.1).
                if (item.MonoBoxSlot != 0)
                {
                    _sprites.DrawSolidRectangle(spriteBatch, bounds, _sprites.SlotColor(item.MonoBoxSlot));
                }

                _sprites.DrawSpriteSolid(spriteBatch, animationFrame, bounds, _sprites.SlotColor(item.MonoImageSlot));
                if (item.MonoBrain)
                {
                    _sprites.DrawSprite(spriteBatch, animationFrame, bounds, Color.White);
                }
            }
            else
            {
                _sprites.DrawSprite(spriteBatch, animationFrame, bounds, Color.White);
            }
        }
    }

    /// <summary>
    /// The EXP opcode's dead object: the ROM's `KILLOF` leaves the object's picture
    /// in place and `EXPP` sets the explosion's row to ACTHIT+6, so the fan's rect is
    /// that picture at (the object's column, row 166).
    /// </summary>
    private sealed class MovieExplosionSource : IExplodable
    {
        private readonly Texture2D _art;
        private readonly Rectangle _bounds;

        public MovieExplosionSource(Texture2D art, Rectangle bounds)
        {
            _art = art;
            _bounds = bounds;
        }

        public IntVector2 Position => new(_bounds.X, _bounds.Y);

        public Rectangle Bounds => _bounds;

        public Rectangle ExplosionBounds => _bounds;

        public EntityLifeState LifeState => EntityLifeState.Dead;

        public Texture2D CurrentAnimationFrame => _art;

        public void Update(GameTime gameTime, PlayField field)
        {
        }

        public void Draw(SpriteBatch spriteBatch)
        {
        }
    }
}