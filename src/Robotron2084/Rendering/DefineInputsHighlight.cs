using Robotron2084.Core;

namespace Robotron2084.Rendering;

/// <summary>
/// The DEFINE INPUTS page's selected-line colour cycle (notes §115): the label of the line the
/// cursor is on ("P1 MOVE UP", say) is drawn in palette slot 8, and this process chases a WHITE
/// flash through it on the PRESENTATION PAGE'S OWN clock (notes §106) — a step every 3 ROM frames,
/// the slot white for one step in seven — so the label strobes green/white exactly the way the
/// title page's message strobes orange/white. The line's value (the bound key or joystick input)
/// stays in the page's static green and does not cycle.
/// </summary>
public sealed class DefineInputsHighlight
{
    /// <summary>
    /// The palette slot the selected line's label is drawn in. Slot 8 is the one slot in 0-9 no
    /// other part of the machine draws text in: this page's own three (6, 7, 9) stay static,
    /// 10-15 are the in-game animator's, and the high score page's shift register passes through
    /// slot 8 but its exit restores every slot to CRTAB (notes §98.6).
    /// </summary>
    public const int Slot = 8;

    /// <summary>The page's GREEN (CRTAB slot 6) — the label's colour between flashes.</summary>
    private const byte Green = 0x38;

    /// <summary>The chase's WHITE — the presentation page's own flash colour (ROM $8A64).</summary>
    private const byte White = 0xFF;

    /// <summary>
    /// The chase takes a step every 3 ROM frames — the presentation page's own rate (ROM
    /// $8A4F/$8A68), the same clock the intro pages' colour processes run on.
    /// </summary>
    private const int RomFramesPerStep = 3;

    /// <summary>
    /// The presentation page chases through SEVEN entries (notes §106), so the text drawn in one
    /// of them is white for one step in seven — the duty cycle this slot copies.
    /// </summary>
    private const int StepsPerLap = 7;

    private int _step;
    private int _chaseSixths;

    /// <summary>
    /// Puts the slot on the page's GREEN with no white on it — the presentation page does the
    /// same (its table copy at ROM $8A3A runs before its first chase step).
    /// </summary>
    public void Start(GamePalette palette)
    {
        _step = 0;
        _chaseSixths = 0;
        palette.SetSlot(Slot, Green);
    }

    /// <summary>Advances the chase by one port tick (call once per Update).</summary>
    public void Update(GamePalette palette)
    {
        _chaseSixths += ArcadeClock.UnitsPerPortTick;
        int period = ArcadeClock.Units(RomFramesPerStep);
        if (_chaseSixths < period)
        {
            return;
        }

        _chaseSixths -= period;
        _step = (_step + 1) % StepsPerLap;
        palette.SetSlot(Slot, _step == 0 ? White : Green);
    }
}
