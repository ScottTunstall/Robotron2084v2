using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Tuning;

namespace Robotron2084.Graphics;

/// <summary>
/// The border of 28 moving Williams "W" logos on the attract page (ROM <c>$87D6</c> onward, R5): a screen of arcade
/// pixels the W is blitted into and erased from exactly as the ROM does, so the overlaps between one lap and the next
/// come out as they do on the cabinet.
/// </summary>
/// <remarks>
/// <para>
/// The first phase (<c>$87D9</c>) draws the ring: one W every four ROM frames along <see cref="WilliamsLogoPath"/>,
/// 28 of them. The second (<c>$88CD</c>) keeps the ring moving: every ROM frame it takes six of the 28 W slots in
/// turn, erases that slot's last W and draws it again at the next place on the path, in the path's next colour.
/// It runs for <see cref="MovingRomFrames"/> ROM frames.
/// </para>
/// <para>
/// The ROM waits for the video beam to clear a W's rows before it redraws it (<c>$88EF</c>, to avoid tearing). The
/// port draws a whole frame at once, so it does not wait.
/// </para>
/// </remarks>
public sealed class WilliamsLogoBorder
{
    /// <summary>The screen's height in arcade pixels. It is multiplied by <see cref="Width"/> to give the size of <see cref="_pixels"/>.</summary>
    public const int Height = HudLayout.ArcadeScreenHeight;

    /// <summary>The screen the W is blitted into, in arcade pixels: the ROM's 304x256. It is multiplied by <see cref="Height"/> to give the size of <see cref="_pixels"/>.</summary>
    public const int Width = HudLayout.ArcadeScreenWidth;

    /// <summary>ROM <c>$87E9</c>: the first phase draws a W every this many ROM frames. One less than this is the value <see cref="_sleepRomFrames"/> is set to after each draw, and it then counts down.</summary>
    private const int DrawIntervalRomFrames = 4;

    /// <summary>ROM <c>$8960</c>: a slot's W is at this column until it has drawn one (nothing is there)... It is stored in <see cref="_logoPositions"/>, with <see cref="EmptySlotRow"/>, to mark a slot that has no logo in it.</summary>
    private const int EmptySlotColumn = 0x13;

    /// <summary>ROM <c>$8960</c>: ...and at this row. It is stored in <see cref="_logoPositions"/>, with <see cref="EmptySlotColumn"/>, to mark a slot that has no logo in it.</summary>
    private const int EmptySlotRow = 0xAF;

    /// <summary>ROM <c>$87D9</c>: the W's on the screen, and so the slots of the second phase. It is the size of <see cref="_logoPositions"/>.</summary>
    private const int LogoCount = 28;

    /// <summary>ROM <c>$88DB</c>: the second phase moves this many W's each ROM frame.</summary>
    private const int MovesPerRomFrame = 6;

    /// <summary>ROM <c>$88D6</c>: the second phase runs for this many ROM frames (704). It is the value <see cref="_movingFramesRemaining"/> is set to when the logos start to move, and it then counts down.</summary>
    private const int MovingRomFrames = 0x02C0;

    private readonly SpriteMask _logoMask;
    private readonly WilliamsLogoPath _path = new();
    private readonly byte[] _pixels = new byte[Width * Height];
    private readonly (int Column, int Row)[] _logoPositions = new (int, int)[LogoCount];
    private int _clockUnits;
    private int _drawnLogoCount;
    private bool _isFirstMove;
    private bool _isMoving;
    private int _movingFramesRemaining;
    private int _sleepRomFrames;
    private int _logoIndex;

    /// <summary>Starts the ring from nothing.</summary>
    /// <param name="logo">The W's shape: the mask of <c>WilliamsLogo.png</c>, its opaque pixels the colour-1 pixels.</param>
    public WilliamsLogoBorder(SpriteMask logoMask) => _logoMask = logoMask;

    /// <summary>True once the second phase has run its course; the ROM then goes on to the next page.</summary>
    public bool IsFinished() => _isMoving && _movingFramesRemaining <= 0;

    /// <summary>Which palette slot each arcade pixel of the screen holds; 0 is empty.</summary>
    public IReadOnlyList<byte> Pixels => _pixels;

    /// <summary>Draws the ring in each pixel's palette slot, the screen's arcade pixels mapped to the canvas as the HUD's are.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="blitter">The blitter that fills the runs and looks the slots' colours up.</param>
    public void Draw(SpriteBatch spriteBatch, BlitterDraw blitter)
    {
        for (int y = 0; y < Height; y++)
        {
            int top = HudLayout.ToPortY(y);
            int bottom = HudLayout.ToPortY(y + 1);
            int x = 0;
            while (x < Width)
            {
                byte slot = _pixels[(y * Width) + x];
                int runEnd = x + 1;
                while (runEnd < Width && _pixels[(y * Width) + runEnd] == slot)
                {
                    runEnd++;
                }

                if (slot != 0)
                {
                    int left = HudLayout.ToPortX(x);
                    Rectangle runBounds = new(left, top, HudLayout.ToPortX(runEnd) - left, bottom - top);
                    blitter.DrawSolidRectangle(spriteBatch, runBounds, blitter.GetSlotColour(slot));
                }

                x = runEnd;
            }
        }
    }

    /// <summary>Advances by one port tick (a ROM frame is 6/5 of one).</summary>
    public void Tick()
    {
        _clockUnits += ArcadeClock.UnitsPerPortTick;
        while (_clockUnits >= ArcadeClock.UnitsPerRomFrame)
        {
            _clockUnits -= ArcadeClock.UnitsPerRomFrame;
            AdvanceRomFrame();
        }
    }

    private void AdvanceRomFrame()
    {
        if (_sleepRomFrames > 0)
        {
            _sleepRomFrames--;
            return;
        }

        if (!_isMoving)
        {
            DrawNextOfTheRing();
        }
        else if (_movingFramesRemaining > 0)
        {
            MoveSomeLogos();
        }
    }

    /// <summary>ROM <c>$8A19</c>: the W's shape in one palette slot; slot 0 erases it.</summary>
    private void Blit(int column, int row, int slot)
    {
        int left = column * ScreenSize.ArcadePixelsPerColumn;
        for (int y = 0; y < _logoMask.Height; y++)
        {
            for (int x = 0; x < _logoMask.Width; x++)
            {
                int screenX = left + x;
                int screenY = row + y;
                if (_logoMask.IsOpaque(x, y) && screenX < Width && screenY < Height)
                {
                    _pixels[(screenY * Width) + screenX] = (byte)slot;
                }
            }
        }
    }

    /// <summary>ROM <c>$87DF</c>: draw a W, step the path, sleep; after the 28th the ring starts moving.</summary>
    private void DrawNextOfTheRing()
    {
        if (_drawnLogoCount == LogoCount)
        {
            StartMoving();
            return;
        }

        Blit(_path.Column, _path.Row, _path.GetSlot());
        _path.Step();
        _drawnLogoCount++;
        _sleepRomFrames = DrawIntervalRomFrames - 1;
    }

    /// <summary>ROM <c>$88E5</c>: one ROM frame of the second phase.</summary>
    private void MoveSomeLogos()
    {
        for (int move = 0; move < MovesPerRomFrame; move++)
        {
            if (_isFirstMove)
            {
                _isFirstMove = false;
            }
            else
            {
                _path.Step();
                _logoIndex = (_logoIndex + 1) % LogoCount;
            }

            (int column, int row) = _logoPositions[_logoIndex];
            Blit(column, row, 0);
            Blit(_path.Column, _path.Row, _path.GetSlot());
            _logoPositions[_logoIndex] = (_path.Column, _path.Row);
        }

        _movingFramesRemaining--;
    }

    /// <summary>ROM <c>$88CD</c>: the path starts again, every slot is empty, and the first move is made at once.</summary>
    private void StartMoving()
    {
        _path.Reset();
        Array.Fill(_logoPositions, (EmptySlotColumn, EmptySlotRow));
        _logoIndex = 0;
        _isMoving = true;
        _isFirstMove = true;
        _movingFramesRemaining = MovingRomFrames;
        MoveSomeLogos();
    }
}
