using Microsoft.Xna.Framework;
using Robotron2084.Core;

namespace Robotron2084.AttractMode;

/// <summary>
///     The attract movie: the story that plays between the title screen and the demo game, as text and little
///     characters walking about.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>It owns the page-script reader and the object-script reader, and runs both of them.</item>
///         <item>
///             It runs them on the arcade's clock. An arcade frame is 6/5 of a port tick, so it adds clock units on
///             every tick (<see cref="ArcadeClock.UnitsPerPortTick" />) and runs one arcade frame each time it has
///             <see cref="ArcadeClock.UnitsPerArcadeFrame" /> of them.
///         </item>
///         <item>Original source: <c>RRSCRIPT.ASM</c> <c>HISTO</c> and its reader <c>SPWAKE</c>; see notes §95, §52 and §93</item>
///         <item>Disassembly: <c>STORYLINE_SCRIPT</c> ($7FA3), read by the unlabelled loop at $79DD</item>
///     </list>
/// </remarks>
public sealed class AttractMovie
{
    private int
        _clockUnits =
            ArcadeClock.UnitsPerArcadeFrame; // seeded so the first tick runs a frame, as the ROM's first cycle does

    public AttractMovie(byte[] script, Random random)
    {
        AttractObjectMachine = new AttractObjectMachine(random);
        AttractPageMachine = new AttractPageMachine(script, AttractObjectMachine, random);
    }

    /// <summary>Movie time played so far, in fiftieths of a second (a test hook).</summary>
    public int ArcadeFrames { get; private set; }

    /// <summary>The characters the movie has walking about.</summary>
    public AttractObjectMachine AttractObjectMachine { get; }

    /// <summary>The text/action interpreter that drives them.</summary>
    public AttractPageMachine AttractPageMachine { get; }

    /// <summary>The script reached DONE / DONE2.</summary>
    public bool IsFinished()
    {
        return AttractPageMachine.IsFinished;
    }

    /// <summary>Advances the movie by one port tick (0 or 1 fiftieths of a second of work).</summary>
    public void Update(GameTime gameTime)
    {
        _clockUnits += ArcadeClock.UnitsPerPortTick;
        if (_clockUnits < ArcadeClock.UnitsPerArcadeFrame) return;

        _clockUnits -= ArcadeClock.UnitsPerArcadeFrame;
        ArcadeFrames++;
        AttractPageMachine.StepFrame();
        AttractObjectMachine.StepFrame();
    }
}
