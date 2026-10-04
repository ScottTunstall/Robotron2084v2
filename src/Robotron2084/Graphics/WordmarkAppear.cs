using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Graphics;

/// <summary>The big ROBOTRON letters coming into view one after another, each pulled together from strips, like an explosion run backwards.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRLOG.ASM</c> <c>WDONE</c> to <c>WDONE1</c>, which stages the appear for each letter eight ROM frames after the last, then waits <c>$20</c> frames for them to finish before the "2084" goes up</item>
/// <item>Disassembly: <c>asm/robomame.asm</c>, the attract-mode appear that <c>AMAP</c> jumps to</item>
/// </list>
/// Each letter is cut into rows that start far apart and close in on the letter's middle row, the way a robot appears at the start
/// of a wave (<see cref="StripEffect"/>). The arcade appears the top and the bottom of each letter as two pieces that close in on
/// the same seam, which is the same picture as one fan that closes in on the letter's middle.
/// </remarks>
public sealed class WordmarkAppear
{
    /// <summary>ROM frames between one letter starting to appear and the next (<c>WDONE0 NAP 8</c>).</summary>
    private const int RomFramesBetweenLetters = 8;

    /// <summary>ROM frames the last letter is left to finish before whatever comes next (<c>NAP $20,LOGG2</c>). It is added to the start time of the last letter to decide when <see cref="IsFinished"/> becomes true.</summary>
    private const int RomFramesAfterLastLetter = 0x20;

    private readonly StripEffect[] _effects;
    private readonly Rectangle _bounds;
    private readonly IReadOnlyList<LetterSpan> _letters;
    private readonly Texture2D _coreSprite;
    private readonly int _spriteHeight;
    private readonly Texture2D _rimSprite;
    private int _clockUnits;

    /// <summary>Makes the appear for a word whose letters have been found.</summary>
    /// <param name="rimSprite">The white mask of the letters' outline.</param>
    /// <param name="coreSprite">The white mask of the letters' body, the same size as the outline.</param>
    /// <param name="spriteSize">The size of each mask, in its own pixels.</param>
    /// <param name="letters">Where each letter sits across the masks.</param>
    /// <param name="origin">Where the whole picture is drawn, in port pixels.</param>
    public WordmarkAppear(Texture2D rimSprite, Texture2D coreSprite, Point spriteSize, IReadOnlyList<LetterSpan> letters, Point origin)
    {
        _rimSprite = rimSprite;
        _coreSprite = coreSprite;
        _letters = letters;
        _spriteHeight = spriteSize.Y;
        _bounds = new Rectangle(origin.X, origin.Y, spriteSize.X * ScreenSize.SpecScale, spriteSize.Y * ScreenSize.SpecScale);
        StripClip wholeScreenClip = StripClip.CreateFromPortPixels(new Rectangle(0, 0, ScreenSize.Width, ScreenSize.Height));
        _effects = [.. letters.Select(letter => StripEffect.CreateAppear(rimSprite, GetLetterBounds(letter), StripFanAxis.Rows, wholeScreenClip))];
    }

    /// <summary>Says whether every letter has finished coming in and been left to settle, so the next thing can go up.</summary>
    public bool IsFinished => _letters.Count == 0 || GetRomFrames() >= GetStartFrame(_letters.Count - 1) + RomFramesAfterLastLetter;

    /// <summary>Makes the appear for a word drawn as two white masks, finding its letters from the outline mask.</summary>
    /// <param name="rimSprite">The white mask of the letters' outline.</param>
    /// <param name="coreSprite">The white mask of the letters' body, the same size as the outline.</param>
    /// <param name="origin">Where the whole picture is drawn, in port pixels.</param>
    public static WordmarkAppear CreateFromMasks(Texture2D rimSprite, Texture2D coreSprite, Point origin)
    {
        var pixels = new Color[rimSprite.Width * rimSprite.Height];
        rimSprite.GetData(pixels);
        bool[] columnHasPixel = new bool[rimSprite.Width];
        for (int index = 0; index < pixels.Length; index++)
        {
            columnHasPixel[index % rimSprite.Width] |= pixels[index].A > 0;
        }

        return new WordmarkAppear(rimSprite, coreSprite, new Point(rimSprite.Width, rimSprite.Height), FindLetters(columnHasPixel), origin);
    }

    /// <summary>How many letters have started to appear (test hook).</summary>
    internal int StartedLetterCount => Enumerable.Range(0, _letters.Count).Count(index => GetRomFrames() >= GetStartFrame(index));

    /// <summary>Finds where the letters are across a mask, which are the runs of columns that have something drawn in them.</summary>
    /// <param name="columnHasPixel">For each column of the mask, whether anything is drawn in it.</param>
    /// <returns>The letters, from left to right.</returns>
    public static IReadOnlyList<LetterSpan> FindLetters(IReadOnlyList<bool> columnHasPixel)
    {
        List<LetterSpan> letters = [];
        int start = -1;
        for (int column = 0; column <= columnHasPixel.Count; column++)
        {
            bool isFilled = column < columnHasPixel.Count && columnHasPixel[column];
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

    /// <summary>Moves on by one port tick.</summary>
    public void Update()
    {
        _clockUnits += ArcadeClock.UnitsPerPortTick;
        int romFrames = GetRomFrames();
        for (int index = 0; index < _effects.Length; index++)
        {
            if (romFrames >= GetStartFrame(index))
            {
                _effects[index].Update(new GameTime());
            }
        }
    }

    /// <summary>Draws the letters that have started: the settled ones whole, and the one coming in as its strips.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="blitter">The blitter that draws a mask in a palette colour.</param>
    /// <param name="rimSlot">The palette slot the outline is drawn in.</param>
    /// <param name="coreSlot">The palette slot the body is drawn in.</param>
    public void Draw(SpriteBatch spriteBatch, BlitterDraw blitter, int rimSlot, int coreSlot)
    {
        int romFrames = GetRomFrames();
        for (int index = 0; index < _effects.Length; index++)
        {
            if (romFrames < GetStartFrame(index))
            {
                continue;
            }

            if (_effects[index].IsAlive())
            {
                DrawStrips(spriteBatch, blitter, index, rimSlot, coreSlot);
            }
            else
            {
                DrawWhole(spriteBatch, blitter, _letters[index], rimSlot, coreSlot);
            }
        }
    }

    private static int GetStartFrame(int index) => index * RomFramesBetweenLetters;

    private int GetRomFrames() => _clockUnits / ArcadeClock.UnitsPerRomFrame;

    private Rectangle GetLetterBounds(LetterSpan letter) =>
        new(_bounds.X + (letter.Start * ScreenSize.SpecScale), _bounds.Y, letter.Width * ScreenSize.SpecScale, _bounds.Height);

    private void DrawWhole(SpriteBatch spriteBatch, BlitterDraw blitter, LetterSpan letter, int rimSlot, int coreSlot)
    {
        Rectangle source = new(letter.Start, 0, letter.Width, _spriteHeight);
        Rectangle destination = GetLetterBounds(letter);
        blitter.DrawSpriteSolidPiece(spriteBatch, _rimSprite, source, destination, blitter.GetSlotColour(rimSlot));
        blitter.DrawSpriteSolidPiece(spriteBatch, _coreSprite, source, destination, blitter.GetSlotColour(coreSlot));
    }

    private void DrawStrips(SpriteBatch spriteBatch, BlitterDraw blitter, int index, int rimSlot, int coreSlot)
    {
        LetterSpan letter = _letters[index];
        foreach (Strip strip in _effects[index].LayOutStrips(letter.Width, _spriteHeight))
        {
            Rectangle source = new(letter.Start, strip.SourceIndex, letter.Width, 1);
            Rectangle destination = new(
                strip.X * ScreenSize.SpecScale,
                strip.Y * ScreenSize.SpecScale,
                letter.Width * ScreenSize.SpecScale,
                ScreenSize.SpecScale);
            blitter.DrawSpriteSolidPiece(spriteBatch, _rimSprite, source, destination, blitter.GetSlotColour(rimSlot));
            blitter.DrawSpriteSolidPiece(spriteBatch, _coreSprite, source, destination, blitter.GetSlotColour(coreSlot));
        }
    }
}
