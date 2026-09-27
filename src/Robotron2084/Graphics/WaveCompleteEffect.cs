using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Palette;

namespace Robotron2084.Graphics;

/// <summary>
/// The arcade's wave-complete effect — the marquee tunnel and its colour-cycling palette, running as a
/// TASK rather than as a screen: the wave clear starts it and the next level is set up at once, so it
/// keeps expanding and erasing itself over the new level while the wave-end music plays out (notes §79,
/// §128).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c>'s <c>GEXEC0</c> — <c>LDD #WVSND / JSR SNDLD</c>, then
/// <c>JSR SCRCLR</c>, <c>JSR RMST</c> (the marquee) and <c>JMP PLSTRT</c> (the next wave), with no wait
/// between them.</item>
/// <item>Disassembly: the tunnel is <c>DRAW_COLOUR_CYCLING_TUNNEL_EFFECT</c> (<c>$5703</c>) and
/// <c>$571E</c> re-allocates it through <c>ALLOCATE_TASK</c> (<c>$D1E3</c>), which is why the call
/// returns and the ROM carries straight on into the next level.</item>
/// <item>Palette: <c>RRG23</c>'s own palette task beside the walker (notes §86).</item>
/// </list>
/// The ramp owns every slot while it runs — the ROM's task writes all fifteen each pass — so the six
/// colour processes are suspended for its duration and the game colours are put back
/// (<c>LOAD_DA51_PALETTE</c>) when the tunnel finishes.
/// </remarks>
/// <seealso cref="TunnelEffect"/>
/// <seealso cref="TunnelPalette"/>
public sealed class WaveCompleteEffect
{
    private readonly TunnelPalette _ramp = new();
    private readonly TunnelEffect _tunnel = new();
    private GamePalette? _palette;
    private bool _rampStarted;
    private int _ringsRamped = -1;

    /// <summary>True once the tunnel has grown to the middle and is erasing itself in black.</summary>
    public bool Erasing => _tunnel.Erasing;

    /// <summary>True once the last ring has been drawn — the effect is over and must not be drawn again.</summary>
    public bool Finished => _tunnel.Finished;

    /// <summary>Starts the ramp beside the tunnel's own task.</summary>
    /// <param name="palette">The live palette the effect takes over, or null in a headless test.</param>
    public void Start(GamePalette? palette)
    {
        _palette = palette;
        AdvanceRamp();
    }

    /// <summary>One port tick: the ramp's window slides once per ring pass, and the tunnel redraws one pass.</summary>
    public void Update()
    {
        if (_tunnel.Finished)
        {
            return;
        }

        if (_ringsRamped != _tunnel.RingsDrawn)
        {
            AdvanceRamp();
        }

        _tunnel.Update();

        if (_tunnel.Finished)
        {
            RestoreGamePalette();
        }
    }

    /// <summary>Draws the rings gathered so far — nothing once the effect is over, when every ring is the black pass.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The sprite set the wall pixel comes from.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (!_tunnel.Finished)
        {
            _tunnel.Draw(spriteBatch, sprites);
        }
    }

    /// <summary>One pass of the ROM's palette task: pick the ramp once, then slide its window.</summary>
    private void AdvanceRamp()
    {
        _ringsRamped = _tunnel.RingsDrawn;

        if (_rampStarted)
        {
            _ramp.Advance();
        }
        else
        {
            _rampStarted = true;
            _ramp.Start();

            // The ramp owns every slot while it runs, so the six colour processes must not fight it
            // for slots 10-15 (the ROM's own palette task writes all fifteen every pass).
            if (_palette is { } live)
            {
                for (int slot = FontSlots.FirstCyclingSlot; slot <= 15; slot++)
                {
                    live.SuspendSlot(slot);
                }
            }
        }

        if (_palette is { } palette)
        {
            _ramp.Apply(palette);
        }
    }

    /// <summary>
    /// Puts the game palette back the way the next wave wants it — the ROM's per-wave
    /// <c>LOAD_DA51_PALETTE</c> (notes §86).
    /// </summary>
    private void RestoreGamePalette()
    {
        if (_palette is not { } palette)
        {
            return;
        }

        for (int slot = 1; slot <= 15; slot++)
        {
            palette.SetSlot(slot, GamePalette.DefaultSlots[slot]);
        }

        for (int slot = FontSlots.FirstCyclingSlot; slot <= 15; slot++)
        {
            palette.ResumeSlot(slot);
        }
    }
}
