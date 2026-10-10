using Microsoft.Xna.Framework;
using Robotron2084.Core;

namespace Robotron2084.AttractMode;

/// <summary>
/// The arcade's attract MOVIE (notes §95): the scripted storyline that plays
/// between the title screen and the machine's phony-player game. It owns the
/// page-script interpreter and the object-script interpreter and runs them on the
/// ROM's frame clock — a 50 Hz frame is 6/5 of a 60 Hz tick, so the movie is
/// driven by the same clock-unit accumulator the entities use (notes §52/§93):
/// five clock units a tick, a ROM frame every time the accumulator reaches six.
/// </summary>
public sealed class AttractMovie
{
    private int _clockUnits = ArcadeClock.UnitsPerRomFrame; // seeded so the first tick runs a frame, as the ROM's first cycle does

    public AttractMovie(byte[] script, Random random)
    {
        ObjectMachine = new AttractObjectMachine(random);
        PageMachine = new AttractPageMachine(script, ObjectMachine, random);
    }

    /// <summary>The script reached DONE / DONE2.</summary>
    public bool IsFinished() => PageMachine.IsFinished;

    /// <summary>The characters the movie has walking about.</summary>
    public AttractObjectMachine ObjectMachine { get; }

    /// <summary>The text/action interpreter that drives them.</summary>
    public AttractPageMachine PageMachine { get; }

    /// <summary>ROM frames played so far (a test hook).</summary>
    public int RomFrames { get; private set; }

    /// <summary>Advances the movie by one port tick (0 or 1 ROM frames of work).</summary>
    public void Update(GameTime gameTime)
    {
        _clockUnits += ArcadeClock.UnitsPerPortTick;
        if (_clockUnits < ArcadeClock.UnitsPerRomFrame)
        {
            return;
        }

        _clockUnits -= ArcadeClock.UnitsPerRomFrame;
        RomFrames++;
        PageMachine.StepFrame();
        ObjectMachine.StepFrame();
    }
}
