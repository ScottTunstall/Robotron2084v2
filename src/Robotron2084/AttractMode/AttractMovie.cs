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

    /// <summary>The most arcade frames <see cref="SkipToNextPage" /> will run, so a script that never reaches a page cannot hang the game.</summary>
    public const int SkipFrameLimit = 20_000;

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

    /// <summary>Makes a movie that has run on to the start of a page of the story.</summary>
    /// <param name="script">The page script.</param>
    /// <param name="random">Where the movie's random numbers come from.</param>
    /// <param name="page">The page to start at, counted from 1. The first page is the first the movie starts.</param>
    /// <returns>A movie at the start of that page, or at its end when the story has fewer pages.</returns>
    public static AttractMovie CreateAtPage(byte[] script, Random random, int page)
    {
        var attractMovie = new AttractMovie(script, random);
        for (var pagesSkipped = 0; pagesSkipped < page; pagesSkipped++) attractMovie.SkipToNextPage();

        return attractMovie;
    }

    /// <summary>Advances the movie by one port tick (0 or 1 fiftieths of a second of work).</summary>
    public void Update(GameTime gameTime)
    {
        _clockUnits += ArcadeClock.UnitsPerPortTick;
        if (_clockUnits < ArcadeClock.UnitsPerArcadeFrame) return;

        _clockUnits -= ArcadeClock.UnitsPerArcadeFrame;
        RunArcadeFrame();
    }

    /// <summary>
    ///     Runs the movie on, without waiting, until the next page of the story starts or the movie ends. Port only: a
    ///     development key uses it to look at each page in turn (notes §97).
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             The pages before the one it reaches are played at once, so the characters and text on the screen are the
    ///             ones the movie would have at that moment.
    ///         </item>
    ///         <item>It stops after <see cref="SkipFrameLimit" /> arcade frames, whatever happens.</item>
    ///         <item>
    ///             The explosions the skipped frames asked for are thrown away: they happened in pages that were not
    ///             watched.
    ///         </item>
    ///     </list>
    /// </remarks>
    public void SkipToNextPage()
    {
        var pageToReach = AttractPageMachine.PagesStarted + 1;
        var framesRun = 0;
        while (framesRun < SkipFrameLimit && !IsFinished() && AttractPageMachine.PagesStarted < pageToReach)
        {
            RunArcadeFrame();
            framesRun++;
        }

        AttractObjectMachine.DrainExplosions();
    }

    /// <summary>Runs one 50th of a second of the movie.</summary>
    private void RunArcadeFrame()
    {
        ArcadeFrames++;
        AttractPageMachine.StepFrame();
        AttractObjectMachine.StepFrame();
    }
}
