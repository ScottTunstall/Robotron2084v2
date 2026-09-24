namespace Robotron2084.Rendering;

/// <summary>
/// The Williams presentation page's OWN colour set (notes §106) — the thing notes §103.3 could
/// not decode and covered with the high score page's process set. Every call site here is the
/// ROM's, on the page that prints the welcome message (`$87A6` onward):
///
/// | ROM | what it does |
/// |-----|--------------|
/// | `$8A3A` | copies the seven bytes at `$8A70` into the RAM palette's entries **1..7**, leaving the other nine on their CRTAB defaults |
/// | `$8A4F` | re-copies that table and sets ONE entry to `$FF` — WHITE — every **3 ROM frames**, stepping 2,3,…,7,1 and round: a white flash running through the seven |
/// | `$884E` | the text colour operand `$66` — the welcome message and the page's credit strings are drawn in entry **6** |
/// | `$89DA` | the border "W" logos step their colour operand `$77 → $66 → … → $11 → $77`, i.e. one entry per step down through 7…1 |
///
/// The seven entries are red, blue, red-orange, green, magenta, ORANGE and yellow, so the page's
/// text is ORANGE with a white flash sweeping through it, and the art
/// around it cycles through the seven.
///
/// **Two clocks, both on notes §52's exact-6ths accumulator** (a ROM frame is 6/5 of a port tick):
/// the WHITE CHASE steps every 3 ROM frames, and the ART'S colour operand steps every 28 frames —
/// the border ring's own rate, which comes out of the animation loop at `$88EF` waiting on
/// <c>vidctrs</c> and moving six logos a frame over a ring of 28 (`$87D9`'s `LDA #$1C`), so a given
/// logo is re-coloured once every 28 frames.
/// </summary>
public sealed class PresentationPagePalette
{
    /// <summary>A port tick advances a ROM-frame clock by 5 sixths (notes §52).</summary>
    private const int SixthsPerPortTick = 5;

    /// <summary>One ROM frame in sixths of a port tick.</summary>
    private const int SixthsPerRomFrame = 6;

    /// <summary>
    /// The seven entries the page's own code writes (ROM `$8A70`), in slot order: red, blue,
    /// red-orange, green, magenta, orange, yellow. Slots 1..7 are the page's; 8..15 keep CRTAB.
    /// </summary>
    public static readonly byte[] PageColors = [0x07, 0xC0, 0x17, 0x30, 0xC7, 0x1F, 0x3F];

    /// <summary>The first entry the page writes.</summary>
    public const int FirstSlot = 1;

    /// <summary>The last entry the page writes (seven of them).</summary>
    public const int LastSlot = 7;

    /// <summary>
    /// The entry the page's own text is drawn in — the ROM's text colour operand `$66`: entry 6,
    /// which the page's table makes ORANGE (`$1F`).
    /// </summary>
    public const int TextSlot = 6;

    /// <summary>The colour the chase writes (`$8A64`'s `LDA #$FF`).</summary>
    private const byte ChaseColor = 0xFF;

    /// <summary>`$8A68`'s `LDA #$03` — the chase takes a step every three ROM frames.</summary>
    private const int ChaseRomFramesPerStep = 3;

    /// <summary>One step per logo-handling, and the ring is 28 logos of one frame each.</summary>
    private const int ArtRomFramesPerStep = 28;

    private readonly int _slotCount = LastSlot - FirstSlot + 1;

    private int _chaseSlot = FirstSlot;
    private int _artStep;
    private int _chaseSixths;
    private int _artSixths;

    /// <summary>
    /// The palette entry the page's art is drawn in on this step: slot 7, 6, 5, …, 1 and round
    /// (the ROM's `$77 → $66 → … → $11 → $77`).
    /// </summary>
    public int ArtColorSlot => LastSlot - _artStep;

    /// <summary>
    /// The entry one step BEHIND <see cref="ArtColorSlot"/> (slot 1 wraps to 7). The port's traced
    /// wordmark draws its rim here so the reference screenshot's two-tone survives the cycle — and
    /// at the wrap the pair is the reference's own red body on a yellow rim.
    /// </summary>
    public int ArtRimSlot => ArtColorSlot == FirstSlot ? LastSlot : ArtColorSlot - 1;

    /// <summary>
    /// The page's seven colours, and NO white yet: the ROM copies the table at `$8A3A` before its
    /// chase task exists, and that task (`$8A4F`) advances to the next entry before whitening it —
    /// so the first white lands on entry 2.
    /// </summary>
    public void Start(GamePalette palette)
    {
        for (int slot = FirstSlot; slot <= LastSlot; slot++)
        {
            palette.SetSlot(slot, PageColors[slot - FirstSlot]);
        }
    }

    /// <summary>Advances both clocks by one port tick (call once per Update).</summary>
    public void Update(GamePalette palette)
    {
        _chaseSixths += SixthsPerPortTick;
        if (StepDue(ref _chaseSixths, ChaseRomFramesPerStep))
        {
            _chaseSlot = _chaseSlot >= LastSlot ? FirstSlot : _chaseSlot + 1;
            Start(palette); // the ROM re-copies the table on every step...
            palette.SetSlot(_chaseSlot, ChaseColor); // ...and then whitens the current entry
        }

        _artSixths += SixthsPerPortTick;
        if (StepDue(ref _artSixths, ArtRomFramesPerStep))
        {
            _artStep = (_artStep + 1) % _slotCount;
        }
    }

    /// <summary>
    /// The page's slots go back to the ROM's CRTAB defaults as it leaves — the colour processes die
    /// with the page and the page after it (<c>FAMPAG</c>) sets its own (notes §103.3).
    /// </summary>
    public void Stop(GamePalette palette)
    {
        for (int slot = FirstSlot; slot <= LastSlot; slot++)
        {
            palette.SetSlot(slot, GamePalette.DefaultSlots[slot]);
        }
    }

    /// <summary>True when a clock's accumulator has reached <paramref name="romFrames"/> frames.</summary>
    private static bool StepDue(ref int sixths, int romFrames)
    {
        int period = romFrames * SixthsPerRomFrame;
        if (sixths < period)
        {
            return false;
        }

        sixths -= period;
        return true;
    }
}
