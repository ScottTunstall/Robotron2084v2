namespace Robotron2084.AttractMode;

/// <summary>
///     Makes the port's storyline: the arcade's page script, with each page held back until the page before it is done.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Each page waits (<see cref="PageScriptOpcodes.WaitUntilDone" />) however long the page before it takes, as
///             well as for the arcade's own pause.
///         </item>
///         <item>Port only (notes §147).</item>
///         <item>
///             The pages after the family, grunts, hulk and spheroid pages wait for those pages to be done
///             (<see cref="AttractSequenceDoneRules" />). In the arcade each is always done by the end of its pause,
///             so the waits change nothing there; they stop the next page starting early if a page ever takes longer.
///         </item>
///         <item>
///             The movie ends when the electrodes page is done, the hero having blown up, and the pause after it,
///             instead of after a fixed time. The pause makes the arcade's own gap between the hero blowing up and the
///             end.
///         </item>
///     </list>
///     Each wait is placed by the arcade's bytes around it. A wait whose bytes are not in the script is left out.
/// </remarks>
public static class AttractStoryline
{
    /// <summary>
    ///     SNOOZE's operand for the pause between the hero blowing up and the end of the movie, in 50ths of a second.
    ///     With the one 50th of a second the wait takes to see that the hero has gone, it makes the same gap as the
    ///     arcade's own fixed-time ending.
    /// </summary>
    public const byte EndPauseArcadeFrames = 30;

    /// <summary>
    ///     The start of the brain page: CLEARM row 88, COLOR palette index 15, then the "B" of "BEWARE". The
    ///     spheroid page's wait goes in front of it.
    /// </summary>
    private static readonly byte[] BrainPageStart = [PageScriptOpcodes.Clear, 0x58, 0x80, PageScriptOpcodes.Colour, 0xFF, 0x42];

    /// <summary>
    ///     The end of the electrodes page: a sleep of 208 50ths of a second, one of 173, then DONE. It is replaced by the
    ///     wait for the hero to blow up.
    /// </summary>
    private static readonly byte[] ElectrodesPageEnd = [0xD0, 0xAD, PageScriptOpcodes.Done];

    /// <summary>
    ///     The start of the grunts page: CLEARM row 88, COLOR palette index 3, then GRUNTS. The family page's wait goes
    ///     in front of it.
    /// </summary>
    private static readonly byte[] GruntsPageStart =
        [PageScriptOpcodes.Clear, 0x58, 0x80, PageScriptOpcodes.Colour, 0x33, PageScriptOpcodes.Grunts];

    /// <summary>The bytes that start the hulk page. The grunts page's wait goes in front of them.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>They are CLEARM row 88, COLOR palette index 5, then <c>SCRPT SCHULK</c> (<c>$85A1</c>).</item>
    ///     </list>
    /// </remarks>
    private static readonly byte[] HulkPageStart =
        [PageScriptOpcodes.Clear, 0x58, 0x80, PageScriptOpcodes.Colour, 0x55, PageScriptOpcodes.StartScript, 0x85, 0xA1];

    /// <summary>The bytes that start the spheroid page. The hulk page's wait goes in front of them.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>They are CLEARM row 112, COLOR palette index 13, then <c>SCRPT SCSC</c> (<c>$85BD</c>).</item>
    ///     </list>
    /// </remarks>
    private static readonly byte[] SpheroidPageStart =
        [PageScriptOpcodes.Clear, 0x70, 0x80, PageScriptOpcodes.Colour, 0xDD, PageScriptOpcodes.StartScript, 0x85, 0xBD];

    /// <summary>Creates the port's storyline from the arcade's page script.</summary>
    /// <param name="arcadePageScript">The arcade's page script, HISTO.</param>
    /// <returns>A new script. <paramref name="arcadePageScript" /> is not changed.</returns>
    public static byte[] Create(byte[] arcadePageScript)
    {
        var script = InsertBefore(arcadePageScript, GruntsPageStart, CreateWait(AttractSequence.Family));
        script = InsertBefore(script, HulkPageStart, CreateWait(AttractSequence.Grunts));
        script = InsertBefore(script, SpheroidPageStart, CreateWait(AttractSequence.Hulk));
        script = InsertBefore(script, BrainPageStart, CreateWait(AttractSequence.SpheroidsAndQuarks));
        return Replace(script, ElectrodesPageEnd,
        [
            ..CreateWait(AttractSequence.Electrodes),
            PageScriptOpcodes.Pause, EndPauseArcadeFrames,
            PageScriptOpcodes.Done
        ]);
    }

    /// <summary>Creates the page-script bytes that wait until a part of the movie is done.</summary>
    private static byte[] CreateWait(AttractSequence attractSequence)
    {
        return [PageScriptOpcodes.WaitUntilDone, (byte)attractSequence];
    }

    /// <summary>Creates a copy of a script with some bytes put in just before the first place the marker bytes are.</summary>
    private static byte[] InsertBefore(byte[] script, byte[] marker, byte[] inserted)
    {
        var index = script.AsSpan().IndexOf(marker);
        if (index < 0) return [..script];

        return [..script[..index], ..inserted, ..script[index..]];
    }

    /// <summary>Creates a copy of a script with the first place the marker bytes are replaced by other bytes.</summary>
    private static byte[] Replace(byte[] script, byte[] marker, byte[] replacement)
    {
        var index = script.AsSpan().IndexOf(marker);
        if (index < 0) return [..script];

        return [..script[..index], ..replacement, ..script[(index + marker.Length)..]];
    }
}
