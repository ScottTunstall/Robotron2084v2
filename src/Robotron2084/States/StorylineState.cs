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
using Robotron2084.Persistence;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
/// The arcade's attract MOVIE, the history page (<c>SPAGE</c> running <c>HISTO</c> after <c>FAMPAG</c>'s title, notes §123) —
/// the scripted storyline that plays after INTRO2
/// sits idle (notes §95/§96). It is the ROM's own HISTO page script run by
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
    private static readonly Rectangle InnerBounds = PlayfieldLayout.InnerBounds;
    private static readonly StripClip Clip = new(InnerBounds.Left, InnerBounds.Right, InnerBounds.Top, InnerBounds.Bottom);

    private readonly SpriteSet _sprites;
    private readonly HighScoreStore _highScores;
    private readonly IPlayerInputSource _humanInput;
    private readonly GameServices _services;
    private readonly PlayfieldWall _wall;
    private readonly GameSession _session;
    private readonly AttractMovie _movie;
    private readonly List<StripEffect> _explosions = [];
    private readonly ButtonEdgeDetector _buttons = new();

    public StorylineState(GameServices services, Random random)
    {
        _services = services;
        _sprites = services.Sprites;
        _highScores = services.HighScores;
        _humanInput = services.Input;

        _wall = new PlayfieldWall(InnerBounds, new WallColorCycle());
        _session = GameSession.NewGame(_humanInput, 1);
        _movie = new AttractMovie(AttractMovieData.Histo, random);
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        // A human at the coin door takes the machine back to the title, exactly as
        // the arcade's coin/start handler does while attract is running.
        PlayerInputState human = _humanInput.Poll();
        if (_buttons.Advance(human).Any)
        {
            manager.TransitionTo(new TitleScreenState(_services));
            return;
        }


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
            Texture2D? animationFrame = MovieAnimationFrames.Resolve(_sprites, exploded.Animation, exploded.AnimationFrameIndex);
            if (animationFrame is null)
            {
                continue;
            }

            _explosions.Add(StripEffect.StartExplosion(
                new MovieExplosionSource(
                    animationFrame,
                    new Rectangle(
                        HudLayout.ArcadeX(exploded.Column * 2),
                        HudLayout.ArcadeY(exploded.Row),
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
        _wall.Draw(spriteBatch, _sprites.WallPixel, _sprites.Blitter.SlotColor(AttractTuning.TitleWallSlot));
        ArcadeHud.DrawScoresAndMen(spriteBatch, _sprites, _session, InnerBounds, showSpareMen: false);

        ArcadeHud.DrawCenteredLargeText(
            spriteBatch,
            _sprites,
            TitleText,
            HudLayout.ArcadeY(AttractTuning.StoryTitleRow),
            HudLayout.HudScoreSlotCurrent);

        DrawObjects(spriteBatch);

        foreach (StripEffect explosion in _explosions)
        {
            explosion.Draw(spriteBatch);
        }

        foreach (MovieTextCell cell in _movie.Page.Text)
        {
            int index = ArcadeText.GlyphIndex(cell.Character);
            if (index >= 0 && index < _sprites.FontLarge.Length)
            {
                _sprites.Blitter.DrawGlyphSlot(
                    spriteBatch,
                    _sprites.FontLarge,
                    index,
                    HudLayout.ArcadeX(cell.X),
                    HudLayout.ArcadeY(cell.Y),
                    cell.Slot);
            }
        }

        if (_movie.Page.Message is { } message)
        {
            _sprites.Text.DrawSmallFontText(
                spriteBatch,
                message.Text,
                HudLayout.ArcadeX(message.X),
                HudLayout.ArcadeY(message.Y),
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
                int x = HudLayout.ArcadeX(item.ArcadeX);
                int y = HudLayout.ArcadeY(item.ArcadeY);
                var bolt = new Rectangle(x, y, ScreenSize.Scaled(_sprites.LaserBar.Width), ScreenSize.Scaled(_sprites.LaserBar.Height));
                _sprites.Blitter.DrawSpriteSolid(spriteBatch, _sprites.LaserBar, bolt, _sprites.Blitter.SlotColor(PlayerTuning.LaserSlot));
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
                HudLayout.ArcadeX(item.ArcadeX),
                HudLayout.ArcadeY(item.ArcadeY),
                ScreenSize.Scaled(animationFrame.Width),
                ScreenSize.Scaled(animationFrame.Height));

            if (item.MonoActive)
            {
                // ROM OPON: the box in the first colour, the object's SHAPE in the
                // second, and — for the brain's box — the brain's own picture over
                // the top (the ROM's second blit, `JMP $D018`, notes §72.1).
                if (item.MonoBoxSlot != 0)
                {
                    _sprites.Blitter.DrawSolidRectangle(spriteBatch, bounds, _sprites.Blitter.SlotColor(item.MonoBoxSlot));
                }

                _sprites.Blitter.DrawSpriteSolid(spriteBatch, animationFrame, bounds, _sprites.Blitter.SlotColor(item.MonoSilhouetteSlot));
                if (item.MonoBrain)
                {
                    _sprites.Blitter.DrawSprite(spriteBatch, animationFrame, bounds, Color.White);
                }
            }
            else
            {
                _sprites.Blitter.DrawSprite(spriteBatch, animationFrame, bounds, Color.White);
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
        private readonly Texture2D _animationFrame;
        private readonly Rectangle _bounds;

        public MovieExplosionSource(Texture2D animationFrame, Rectangle bounds)
        {
            _animationFrame = animationFrame;
            _bounds = bounds;
        }

        public IntVector2 Position => new(_bounds.X, _bounds.Y);

        public Rectangle Bounds => _bounds;

        public Rectangle ExplosionBounds => _bounds;

        public EntityLifeState LifeState => EntityLifeState.Dead;

        public Texture2D CurrentAnimationFrame => _animationFrame;

        public void Update(GameTime gameTime, PlayField field)
        {
        }

        public void Draw(SpriteBatch spriteBatch)
        {
        }
    }
}
