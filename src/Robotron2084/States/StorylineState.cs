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
///     The arcade's attract MOVIE, the history page (<c>SPAGE</c> running <c>HISTO</c> after <c>FAMPAG</c>'s title, notes
///     §123) —
///     the scripted storyline that plays after INTRO2
///     sits idle (notes §95/§96). It is the ROM's own HISTO page script run by
///     <see cref="AttractMovie" />: the intro screen's text crawl ("ROBOTRON 2084 /
///     INSPIRED BY HIS NEVER ENDING QUEST FOR PROGRESS…"), then the hero, the family,
///     the grunts, the hulk, the spheroid/tank/enforcer scene, the brain's
///     reprogramming and the score electrodes, all on the title's solid $CC wall with the
///     player's score and lives in the HUD.
///     It plays the port's copy of HISTO (<see cref="AttractStoryline" />), in which each page waits until the page
///     before it is done; in the arcade's own timing that changes nothing (notes §147).
///     Draw order follows the screen's own layering: wall, HUD, the title string (the
///     ROM prints it in SPGSUB and the page script never clears above row 48), the
///     character objects, then the story text and the score-row name popups on top.
/// </summary>
public sealed class StorylineState : IGameState, IAttractState
{
    /// <summary>ROM string 128, printed by `SPGSUB` and never cleared by the page script.</summary>
    internal const string TitleText = "ROBOTRON 2084";

    private readonly ButtonEdgeDetector _buttonEdgeDetector = new();
    private readonly List<StripEffect> _stripEffects = [];
    private readonly IPlayerInputSource _humanPlayerInputSource;
    private readonly AttractMovie _attractMovie;
    private readonly GameServices _gameServices;
    private readonly GameSession _gameSession;
    private readonly TextureSet _textureSet;
    private readonly PlayfieldWall _playfieldWall;

    /// <summary>Opens the story at its start, or at the start of a page of it.</summary>
    /// <param name="gameServices">The attract screens' shared dependencies.</param>
    /// <param name="startPage">The page to open at, counted from 1, or null to open at the very start.</param>
    public StorylineState(GameServices gameServices, int? startPage = null)
    {
        _gameServices = gameServices;
        _textureSet = gameServices.TextureSet;
        _humanPlayerInputSource = gameServices.PlayerInputSource;

        _playfieldWall = new PlayfieldWall(PlayfieldLayout.GetInnerBounds(), new WallColourCycle());
        _gameSession = GameSession.CreateNewGame(_humanPlayerInputSource, 1);
        var storylineScript = AttractStoryline.Create(AttractMovieData.Histo);
        _attractMovie = new AttractMovie(storylineScript, gameServices.Random);
        if (startPage is { } page) _attractMovie = AttractMovie.CreateAtPage(storylineScript, gameServices.Random, page);
    }

    /// <summary>Gets the page of the story the movie is on, counted from 1. The very start counts as the first page.</summary>
    internal int GetPage()
    {
        return Math.Max(1, _attractMovie.AttractPageMachine.PagesStarted);
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        _playfieldWall.Draw(spriteBatch, _textureSet.WallPixelTexture, _textureSet.BlitterDraw.GetPaletteIndexColour(AttractTuning.TitleWallPaletteIndex));
        ArcadeHud.DrawScoresAndLives(spriteBatch, _textureSet, _gameSession, PlayfieldLayout.GetInnerBounds(), false);

        ArcadeHud.DrawCentredLargeText(
            spriteBatch,
            _textureSet,
            TitleText,
            HudLayout.ToPortY(AttractTuning.StoryTitleRow),
            HudLayout.HudScorePaletteIndexCurrent);

        DrawObjects(spriteBatch);

        foreach (var explosion in _stripEffects) explosion.Draw(spriteBatch);

        foreach (var cell in _attractMovie.AttractPageMachine.TextCells)
        {
            var index = ArcadeText.GetGlyphIndex(cell.Character);
            if (index >= 0 && index < _textureSet.FontLargeTextures.Length)
                _textureSet.BlitterDraw.DrawGlyphPaletteIndex(
                    spriteBatch,
                    _textureSet.FontLargeTextures,
                    index,
                    HudLayout.ToPortX(cell.X),
                    HudLayout.ToPortY(cell.Y),
                    cell.PaletteIndex);
        }

        if (_attractMovie.AttractPageMachine.Message is { } message)
            _textureSet.ArcadeText.DrawSmallFontText(
                spriteBatch,
                message.Text,
                HudLayout.ToPortX(message.X),
                HudLayout.ToPortY(message.Y),
                message.PaletteIndex);
    }

    public void Update(GameTime gameTime, GameStateManager gameStateManager)
    {
        // A human at the coin door takes the machine back to the title, exactly as
        // the arcade's coin/start handler does while attract is running.
        var familyMember = _humanPlayerInputSource.Poll();
        if (_buttonEdgeDetector.Advance(familyMember).AnyPressed())
        {
            gameStateManager.TransitionTo(new TitleScreenState(_gameServices));
            return;
        }

        _attractMovie.Update(gameTime);

        // Page Up held (dev key, notes §97): run the movie's clock extra
        // times so a later scene can be reached without waiting out the text
        // crawl. The clock itself is untouched — this is the same accumulator,
        // just stepped more often.
        if (DeveloperKeys.AttractFastForward)
            for (var i = 1; i < DeveloperKeys.AttractFastForwardMultiplier; i++)
                _attractMovie.Update(gameTime);

        foreach (var exploded in _attractMovie.AttractObjectMachine.DrainExplosions())
        {
            // EXPP: the explosion takes the sprite the object was showing and the
            // direction of a pure HORIZONTAL laser ($FF00), which the Gospel's
            // dispatch turns into the ROW-splitting fan.
            var animationFrameTexture =
                MovieAnimationFrames.Resolve(_textureSet, exploded.Animation, exploded.AnimationFrameIndex);
            if (animationFrameTexture is null) continue;

            _stripEffects.Add(StripEffect.CreateExplosion(
                new MovieExplosionSource(
                    animationFrameTexture,
                    new Rectangle(
                        HudLayout.ToPortX(exploded.Column * 2),
                        HudLayout.ToPortY(exploded.Row),
                        ScreenSize.ToPortPixelsFromArcadePixels(animationFrameTexture.Width),
                        ScreenSize.ToPortPixelsFromArcadePixels(animationFrameTexture.Height))),
                Direction8.Left,
                GetClip()));
        }

        for (var i = _stripEffects.Count - 1; i >= 0; i--)
        {
            _stripEffects[i].Update(gameTime);
            if (!_stripEffects[i].IsAlive()) _stripEffects.RemoveAt(i);
        }

        // HISTO ends with DONE2, which the ROM answers with RUNIT: the machine
        // starts its phony-player game.
        if (_attractMovie.IsFinished()) gameStateManager.TransitionTo(new AttractState(_gameServices));
    }

    /// <summary>The movie interior, in canvas pixels: a strip that falls outside it is dropped (the ROM's clip).</summary>
    private static StripClip GetClip()
    {
        var bounds = PlayfieldLayout.GetInnerBounds();
        return new StripClip(bounds.Left, bounds.Right, bounds.Top, bounds.Bottom);
    }

    /// <summary>Draws a laser bolt: <c>LASPIC</c>, the rotating laser table's horizontal bar.</summary>
    private void DrawLaser(SpriteBatch spriteBatch, MovieObject movieObject)
    {
        var x = HudLayout.ToPortX(movieObject.GetArcadeX());
        var y = HudLayout.ToPortY(movieObject.GetArcadeY());
        var bolt = new Rectangle(x, y, ScreenSize.ToPortPixelsFromArcadePixels(_textureSet.LaserBarTexture.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(_textureSet.LaserBarTexture.Height));
        _textureSet.BlitterDraw.DrawSpriteSolid(spriteBatch, _textureSet.LaserBarTexture, bolt,
            _textureSet.BlitterDraw.GetPaletteIndexColour(PlayerTuning.LaserPaletteIndex));
    }

    /// <summary>
    ///     ROM <c>OPON</c>: the box in the first colour, the object's SHAPE in the second, and — for the
    ///     brain's box — the brain's own sprite over the top (the ROM's second blit, <c>JMP $D018</c>, notes §72.1).
    /// </summary>
    private void DrawMonoBox(SpriteBatch spriteBatch, MovieObject movieObject, Texture2D animationFrameTexture, Rectangle bounds)
    {
        if (movieObject.MonoBoxPaletteIndex != 0)
            _textureSet.BlitterDraw.DrawSolidRectangle(spriteBatch, bounds, _textureSet.BlitterDraw.GetPaletteIndexColour(movieObject.MonoBoxPaletteIndex));

        _textureSet.BlitterDraw.DrawSpriteSolid(spriteBatch, animationFrameTexture, bounds,
            _textureSet.BlitterDraw.GetPaletteIndexColour(movieObject.MonoSilhouettePaletteIndex));
        if (movieObject.BrainVisible) _textureSet.BlitterDraw.DrawSprite(spriteBatch, animationFrameTexture, bounds, Color.White);
    }

    private void DrawObjects(SpriteBatch spriteBatch)
    {
        foreach (var movieObject in _attractMovie.AttractObjectMachine.Objects)
        {
            if (movieObject.IsDead || (!movieObject.IsOnList && !movieObject.IsMonoActive)) continue;

            if (movieObject.IsLaser)
            {
                DrawLaser(spriteBatch, movieObject);
                continue;
            }

            if (movieObject.Descriptor is not { } descriptor) continue;

            var animationFrameTexture = MovieAnimationFrames.Resolve(_textureSet, descriptor.Animation, movieObject.AnimationFrameIndex);
            if (animationFrameTexture is null) continue;

            var bounds = new Rectangle(
                HudLayout.ToPortX(movieObject.GetArcadeX()),
                HudLayout.ToPortY(movieObject.GetArcadeY()),
                ScreenSize.ToPortPixelsFromArcadePixels(animationFrameTexture.Width),
                ScreenSize.ToPortPixelsFromArcadePixels(animationFrameTexture.Height));

            if (movieObject.IsMonoActive)
            {
                DrawMonoBox(spriteBatch, movieObject, animationFrameTexture, bounds);
                continue;
            }

            _textureSet.BlitterDraw.DrawSprite(spriteBatch, animationFrameTexture, bounds, Color.White);
        }
    }
}
