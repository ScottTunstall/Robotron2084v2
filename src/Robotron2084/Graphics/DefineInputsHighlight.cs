using Robotron2084.Core;
using Robotron2084.Palette;

namespace Robotron2084.Graphics;

/// <summary>
///     The DEFINE INPUTS page's selected-line colour cycle (notes §115): the label of the line the
///     cursor is on ("P1 MOVE UP", say) is drawn in palette index 8, and this process chases a WHITE
///     flash through it on the PRESENTATION PAGE'S OWN clock (notes §106) — a step every 3 fiftieths of a second,
///     the palette index white for one step in seven — so the label strobes green/white exactly the way the
///     title page's message strobes orange/white. The line's value (the bound key or joystick input)
///     stays in the page's static green and does not cycle.
/// </summary>
public sealed class DefineInputsHighlight
{
    /// <summary>
    ///     The palette index the selected line's label is drawn in. Palette index 8 is the one palette index in 0-9 no
    ///     other part of the machine draws text in: this page's own three (6, 7, 9) stay static,
    ///     10-15 are the in-game animator's, and the high score page's shift register passes through
    ///     palette index 8 but its exit restores every palette index to CRTAB (notes §98.6).
    /// </summary>
    public const int PaletteIndex = 8;

    /// <summary>
    ///     The chase takes a step every 3 fiftieths of a second — the presentation page's own rate (ROM
    ///     $8A4F/$8A68), the same clock the intro pages' colour processes run on.
    /// </summary>
    private const int ArcadeFramesPerStep = 3;

    /// <summary>
    ///     The presentation page chases through SEVEN entries (notes §106), so the text drawn in one
    ///     of them is white for one step in seven — the duty cycle this palette index copies.
    ///     <see cref="_stepIndex" /> counts up to this and then goes back to the first step.
    /// </summary>
    private const int StepsPerLap = 7;

    /// <summary>The chase's WHITE — the presentation page's own flash colour (ROM $8A64).</summary>
    private const byte White = 0xFF;

    /// <summary>
    ///     The page's GREEN — the palette's own CRTAB value for palette index 6, the label's colour
    ///     between flashes.
    /// </summary>
    private static readonly byte Green = GamePalette.DefaultPaletteValues[6];

    private int _chaseClockUnits;
    private int _stepIndex;

    /// <summary>
    ///     Puts the palette index on the page's GREEN with no white on it — the presentation page does the
    ///     same (its table copy at ROM $8A3A runs before its first chase step).
    /// </summary>
    public void Start(GamePalette palette)
    {
        _stepIndex = 0;
        _chaseClockUnits = 0;
        palette.SetPaletteIndexValue(PaletteIndex, Green);
    }

    /// <summary>Advances the chase by one port tick (call once per Update).</summary>
    public void Update(GamePalette palette)
    {
        _chaseClockUnits += ArcadeClock.UnitsPerPortTick;
        var stepClockUnits = ArcadeClock.ToClockUnits(ArcadeFramesPerStep);
        if (_chaseClockUnits < stepClockUnits) return;

        _chaseClockUnits -= stepClockUnits;
        _stepIndex = (_stepIndex + 1) % StepsPerLap;
        var colour = Green;
        if (_stepIndex == 0) colour = White;

        palette.SetPaletteIndexValue(PaletteIndex, colour);
    }
}
