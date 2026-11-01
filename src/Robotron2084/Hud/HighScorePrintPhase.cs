namespace Robotron2084.Hud;

/// <summary>Which group of the high score page is being printed, in the order the page prints them.</summary>
internal enum HighScorePrintPhase
{
    /// <summary>Nothing is printed yet: the frame around the page is still being drawn.</summary>
    Waiting,

    /// <summary>The rows of today's list are being printed.</summary>
    Today,

    /// <summary>The top entry's line, printed between the two lists.</summary>
    Top,

    /// <summary>The rows of the all-time list are being printed.</summary>
    AllTime,

    /// <summary>The two heading lines, the last thing printed on the page.</summary>
    Headers,

    /// <summary>Everything on the page has been printed.</summary>
    Done
}
