using Microsoft.Xna.Framework;
using Robotron2084.AttractMode;
using Robotron2084.Level.Attract;
using Xunit;

namespace Robotron2084.Tests.AttractMode;

/// <summary>
///     The port's storyline: the arcade's page script, in which each page waits until the page before it is done. Port
///     only (notes §147). The waits must not change how the arcade's pages play.
/// </summary>
public sealed class AttractStorylineTests
{
    private const int TickLimit = 60_000;

    private static GameTime Tick()
    {
        return new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 60.0));
    }

    /// <summary>Everything the movie shows in one 50th of a second, as text, so two movies can be compared.</summary>
    private static string GetSnapshot(AttractMovie attractMovie)
    {
        var objects = attractMovie.AttractObjectMachine.Objects.Select(movieObject =>
            $"{movieObject.Descriptor?.Animation}:{movieObject.AnimationFrameIndex}@{movieObject.XSubpixels},{movieObject.YSubpixels}");
        var cells = attractMovie.AttractPageMachine.TextCells.Select(cell => $"{cell.Character}{cell.PaletteIndex}@{cell.X},{cell.Y}");
        return $"{string.Join(';', objects)}|{string.Join(';', cells)}|{attractMovie.AttractPageMachine.Message}";
    }

    [Fact]
    public void WithTheDoneWaits_TheStorylinePlaysExactlyAsTheArcadesDoes()
    {
        var arcadeMovie = new AttractMovie(AttractMovieData.Histo, new Random(5));
        var portMovie = new AttractMovie(AttractStoryline.Create(AttractMovieData.Histo), new Random(5));

        for (var tick = 0; tick < TickLimit && !arcadeMovie.IsFinished(); tick++)
        {
            arcadeMovie.Update(Tick());
            portMovie.Update(Tick());
            Assert.Equal(GetSnapshot(arcadeMovie), GetSnapshot(portMovie));
            Assert.Equal(arcadeMovie.IsFinished(), portMovie.IsFinished());
        }

        Assert.True(arcadeMovie.IsFinished());
        Assert.Equal(arcadeMovie.ArcadeFrames, portMovie.ArcadeFrames);
    }

    [Fact]
    public void TheDoneWaits_GoAtTheEndOfTheFamilyGruntsHulkAndSpheroidPages_AndTheEndOfTheMovie()
    {
        var script = AttractStoryline.Create(AttractMovieData.Histo);

        var waits = new List<AttractSequence>();
        for (var index = 0; index < script.Length - 1; index++)
            if (script[index] == PageScriptOpcodes.WaitUntilDone && script[index + 1] <= (byte)AttractSequence.Electrodes)
                waits.Add((AttractSequence)script[index + 1]);

        Assert.Equal(
            [
                AttractSequence.Family, AttractSequence.Grunts, AttractSequence.Hulk, AttractSequence.SpheroidsAndQuarks,
                AttractSequence.Electrodes
            ],
            waits);
    }

    [Fact]
    public void AWait_HoldsTheScriptUntilItsPartIsDone_HoweverLongThatTakes()
    {
        // Wait for the grunts, then print "A". GS1 ($84F5) is a grunt that walks for a while and is then shot (EXP).
        byte[] script = [PageScriptOpcodes.WaitUntilDone, (byte)AttractSequence.Grunts, 0x41];
        var attractObjectMachine = new AttractObjectMachine(new Random(11));
        var attractPageMachine = new AttractPageMachine(script, attractObjectMachine, new Random(11));
        attractObjectMachine.StartScript(0x84F5);
        attractObjectMachine.StepFrame(); // the script's first step makes its object a grunt (SETOB)
        Assert.True(attractObjectMachine.HasLiveObjectWith(MovieAnimation.Grunt));

        var gruntWasShot = false;
        for (var frame = 0; frame < 3000 && !gruntWasShot; frame++)
        {
            attractPageMachine.StepFrame();
            Assert.Empty(attractPageMachine.TextCells); // the grunt is still there, so "A" must wait
            attractObjectMachine.StepFrame();
            gruntWasShot = attractObjectMachine.DrainExplosions().Count > 0;
        }

        Assert.True(gruntWasShot, "the grunt was never shot");
        attractPageMachine.StepFrame();
        Assert.Equal(['A'], attractPageMachine.TextCells.Select(cell => cell.Character).ToArray());
    }

    [Fact]
    public void AScriptWithNoneOfTheArcadesMarkers_IsCopiedAsItIs()
    {
        byte[] script = [0x41, 0x5F, 0x42];

        var created = AttractStoryline.Create(script);

        Assert.Equal(script, created);
        Assert.NotSame(script, created);
    }
}
