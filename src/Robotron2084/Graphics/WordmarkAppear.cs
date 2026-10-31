using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Graphics;

/// <summary>
///     The big ROBOTRON letters coming into view one after another, each pulled together from strips, like an
///     explosion run backwards.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRLOG.ASM</c> <c>WDONE</c> to <c>WDONE1</c>, which stages the appear for each letter
///             eight fiftieths of a second after the last, then waits <c>$20</c> frames for them to finish before the
///             "2084" goes up
///         </item>
///         <item>Disassembly: <c>asm/robomame.asm</c>, the attract-mode appear that <c>AMAP</c> jumps to</item>
///     </list>
///     Each letter is cut into rows that start far apart and close in on the letter's middle row, the way a robot appears
///     at the start
///     of a wave (<see cref="StripEffect" />). The arcade appears the top and the bottom of each letter as two pieces that
///     close in on
///     the same seam, which is the same picture as one fan that closes in on the letter's middle.
/// </remarks>
public sealed class WordmarkAppear
{
    /// <summary>
    ///     fiftieths of a second the last letter is left to finish before whatever comes next (<c>NAP $20,LOGG2</c>). It
    ///     is added to the start time of the last letter to decide when <see cref="IsFinished" /> becomes true.
    /// </summary>
    private const int ArcadeFramesAfterLastLetter = 0x20;

    /// <summary>fiftieths of a second between one letter starting to appear and the next (<c>WDONE0 NAP 8</c>).</summary>
    private const int ArcadeFramesBetweenLetters = 8;

    private readonly Rectangle _bounds;
    private readonly Texture2D _coreTexture;

    private readonly StripEffect[] _effects;
    private readonly IReadOnlyList<LetterSpan> _letters;
    private readonly Texture2D _rimTexture;
    private readonly int _spriteHeight;
    private int _clockUnits;

    /// <summary>Makes the appear for a word whose letters have been found.</summary>
    /// <param name="rimTexture">The white mask of the letters' outline.</param>
    /// <param name="coreTexture">The white mask of the letters' body, the same size as the outline.</param>
    /// <param name="spriteSize">The size of each mask, in its own pixels.</param>
    /// <param name="letters">Where each letter sits across the masks.</param>
    /// <param name="origin">Where the whole picture is drawn, in port pixels.</param>
    public WordmarkAppear(Texture2D rimTexture, Texture2D coreTexture, Point spriteSize,
        IReadOnlyList<LetterSpan> letters, Point origin)
    {
        _rimTexture = rimTexture;
        _coreTexture = coreTexture;
        _letters = letters;
        _spriteHeight = spriteSize.Y;
        _bounds = new Rectangle(origin.X, origin.Y, spriteSize.X * ScreenSize.PortPixelsPerArcadePixel,
            spriteSize.Y * ScreenSize.PortPixelsPerArcadePixel);
        var wholeScreenClip = StripClip.CreateFromPortPixels(new Rectangle(0, 0, ScreenSize.Width, ScreenSize.Height));
        _effects =
        [
            .. letters.Select(letter =>
                StripEffect.CreateAppear(rimTexture, GetLetterBounds(letter), StripFanAxis.Rows, wholeScreenClip))
        ];
    }

    /// <summary>Makes the appear for a word drawn as two white masks, finding its letters from the outline mask.</summary>
    /// <param name="rimTexture">The white mask of the letters' outline.</param>
    /// <param name="coreTexture">The white mask of the letters' body, the same size as the outline.</param>
    /// <param name="origin">Where the whole picture is drawn, in port pixels.</param>
    public static WordmarkAppear CreateFromMasks(Texture2D rimTexture, Texture2D coreTexture, Point origin)
    {
        var pixels = new Color[rimTexture.Width * rimTexture.Height];
        rimTexture.GetData(pixels);
        var columnHasPixel = new bool[rimTexture.Width];
        for (var index = 0; index < pixels.Length; index++)
            columnHasPixel[index % rimTexture.Width] |= pixels[index].A > 0;

        return new WordmarkAppear(rimTexture, coreTexture, new Point(rimTexture.Width, rimTexture.Height),
            FindLetters(columnHasPixel), origin);
    }

    /// <summary>Finds where the letters are across a mask, which are the runs of columns that have something drawn in them.</summary>
    /// <param name="columnHasPixel">For each column of the mask, whether anything is drawn in it.</param>
    /// <returns>The letters, from left to right.</returns>
    public static IReadOnlyList<LetterSpan> FindLetters(IReadOnlyList<bool> columnHasPixel)
    {
        List<LetterSpan> letters = [];
        var start = -1;
        for (var column = 0; column <= columnHasPixel.Count; column++)
        {
            var isFilled = column < columnHasPixel.Count && columnHasPixel[column];
            if (isFilled && start < 0)
            {
                start = column;
            }
            else if (!isFilled && start >= 0)
            {
                letters.Add(new LetterSpan(start, column - start));
                start = -1;
            }
        }

        return letters;
    }

    /// <summary>Draws the letters that have started: the settled ones whole, and the one coming in as its strips.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="blitterDraw">The blitter that draws a mask in a palette colour.</param>
    /// <param name="rimPaletteIndex">The palette index the outline is drawn in.</param>
    /// <param name="corePaletteIndex">The palette index the body is drawn in.</param>
    public void Draw(SpriteBatch spriteBatch, BlitterDraw blitterDraw, int rimPaletteIndex, int corePaletteIndex)
    {
        var arcadeFrames = GetArcadeFrames();
        for (var index = 0; index < _effects.Length; index++)
        {
            if (arcadeFrames < GetStartFrame(index)) continue;

            if (_effects[index].IsAlive())
                DrawStrips(spriteBatch, blitterDraw, index, rimPaletteIndex, corePaletteIndex);
            else
                DrawWhole(spriteBatch, blitterDraw, _letters[index], rimPaletteIndex, corePaletteIndex);
        }
    }

    /// <summary>Says whether every letter has finished coming in and been left to settle, so the next thing can go up.</summary>
    public bool IsFinished()
    {
        return _letters.Count == 0 || GetArcadeFrames() >= GetStartFrame(_letters.Count - 1) + ArcadeFramesAfterLastLetter;
    }

    /// <summary>Moves on by one port tick.</summary>
    public void Update()
    {
        _clockUnits += ArcadeClock.UnitsPerPortTick;
        var arcadeFrames = GetArcadeFrames();
        for (var index = 0; index < _effects.Length; index++)
            if (arcadeFrames >= GetStartFrame(index))
                _effects[index].Update(new GameTime());
    }

    /// <summary>How many letters have started to appear (test hook).</summary>
    internal int GetStartedLetterCount()
    {
        return Enumerable.Range(0, _letters.Count).Count(index => GetArcadeFrames() >= GetStartFrame(index));
    }

    private static int GetStartFrame(int index)
    {
        return index * ArcadeFramesBetweenLetters;
    }

    private void DrawStrips(SpriteBatch spriteBatch, BlitterDraw blitterDraw, int index, int rimPaletteIndex, int corePaletteIndex)
    {
        var letter = _letters[index];
        foreach (var strip in _effects[index].GetStrips(letter.Width, _spriteHeight))
        {
            Rectangle source = new(letter.Start, strip.SourceIndex, letter.Width, 1);
            Rectangle destination = new(
                strip.X * ScreenSize.PortPixelsPerArcadePixel,
                strip.Y * ScreenSize.PortPixelsPerArcadePixel,
                letter.Width * ScreenSize.PortPixelsPerArcadePixel,
                ScreenSize.PortPixelsPerArcadePixel);
            blitterDraw.DrawSpriteSolidPiece(spriteBatch, _rimTexture, source, destination, blitterDraw.GetPaletteIndexColour(rimPaletteIndex));
            blitterDraw.DrawSpriteSolidPiece(spriteBatch, _coreTexture, source, destination,
                blitterDraw.GetPaletteIndexColour(corePaletteIndex));
        }
    }

    private void DrawWhole(SpriteBatch spriteBatch, BlitterDraw blitterDraw, LetterSpan letter, int rimPaletteIndex, int corePaletteIndex)
    {
        Rectangle source = new(letter.Start, 0, letter.Width, _spriteHeight);
        var destination = GetLetterBounds(letter);
        blitterDraw.DrawSpriteSolidPiece(spriteBatch, _rimTexture, source, destination, blitterDraw.GetPaletteIndexColour(rimPaletteIndex));
        blitterDraw.DrawSpriteSolidPiece(spriteBatch, _coreTexture, source, destination, blitterDraw.GetPaletteIndexColour(corePaletteIndex));
    }

    private int GetArcadeFrames()
    {
        return _clockUnits / ArcadeClock.UnitsPerArcadeFrame;
    }

    private Rectangle GetLetterBounds(LetterSpan letter)
    {
        return new Rectangle(_bounds.X + letter.Start * ScreenSize.PortPixelsPerArcadePixel, _bounds.Y,
            letter.Width * ScreenSize.PortPixelsPerArcadePixel, _bounds.Height);
    }
}
