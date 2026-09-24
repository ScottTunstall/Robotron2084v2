namespace Robotron2084.Persistence;

/// <summary>
/// The arcade's "GOD" entry (notes §98.2): the operator's own top score and the
/// name that goes with it (<c>GODSCR</c>/<c>GODINT</c> — up to <see
/// cref="HighScoreTable.TopNameLength"/> characters, entered from the service
/// mode's "ENTER GODS NAME"). The port seeds it with the ROM's factory value
/// ("WILLY ELKTRIX", 151782 — RRTESTC's <c>DEFHSR</c>/<c>DEFGOD</c>) because the
/// operator side is deliberately out of scope (D-019).
/// </summary>
public sealed record TopScoreEntry(string Name, int Score);

/// <summary>
/// The arcade's high score table (notes §98): the operator's "GOD" entry, the
/// ALL-TIME list (ROM <c>CMSCOR</c> — the 36 rows the screen shows, 12 per
/// column × 3 columns) and the "TODAY'S" list (ROM <c>TODAYS</c> — the 10 rows
/// the screen shows, 5 per column × 2 columns).
///
/// Both lists are seeded with the ROM's OWN factory tables, so the table is
/// never empty and no operator input is needed. The ALL-TIME list persists
/// (the arcade's battery-backed CMOS); TODAY'S is reloaded from the factory
/// table at every power-up — that is exactly what <c>CKHS</c> does
/// (`LDX #TODTAB / LDY #TODAYS / CMSMVV`).
///
/// Scores are offered by <see cref="Submit"/> with the initials their player typed on the CONG
/// screen (notes §116), which follows <c>EGSUB</c>: <c>GODCHK</c> (beat the top → the new top, the
/// old one drops into the all-time list) → else <c>TODCHK</c> AND <c>ALLCHK</c> (each list takes the
/// score when it beats that list's lowest entry). The all-time list additionally caps how many
/// entries may share one set of initials (<c>SETBOT</c>).
/// </summary>
public sealed class HighScoreTable
{
    /// <summary>ROM: three initials per entry (`NULSCR` is three spaces).</summary>
    public const int InitialsLength = 3;

    /// <summary>ROM `TODAYS`: the screen shows 10 (5 per column × 2 columns).</summary>
    public const int TodayCapacity = 10;

    /// <summary>ROM `CMSCOR`: the screen shows 36 (12 per column × 3 columns).</summary>
    public const int AllTimeCapacity = 36;

    /// <summary>ROM `LDA #23` — the operator's GOD name's length (`GODSCR`).</summary>
    public const int TopNameLength = 23;

    /// <summary>
    /// ROM `SETBOT`'s cap: the all-time list keeps at most this many entries sharing one set of
    /// initials, and a score that beats the lowest of a full set replaces it (`ONLY5P` explains the
    /// replacement). TODAY's list has no such cap — it keeps the day's ten best.
    /// </summary>
    public const int AllTimeInitialsCap = 5;

    /// <summary>ROM `SETBOT`'s `LDA #4` — the cap when the initials are the top entry's own.</summary>
    public const int AllTimeTopInitialsCap = 4;

    private readonly List<HighScoreEntry> _today;
    private readonly List<HighScoreEntry> _allTime;

    private HighScoreTable(TopScoreEntry top, IEnumerable<HighScoreEntry> today, IEnumerable<HighScoreEntry> allTime)
    {
        Top = top;
        // Both lists are the ROM's FULL tables: an unused row holds NULSCR (three
        // spaces and 0) rather than being absent, which is what the screen draws and
        // what "does this score qualify?" is measured against.
        _today = [.. today];
        _allTime = [.. allTime];
        Fill(_today, TodayCapacity);
        Fill(_allTime, AllTimeCapacity);
    }

    /// <summary>The operator's top entry (shown on its own line, in its own colours).</summary>
    public TopScoreEntry Top { get; private set; }

    /// <summary>ROM `TODAYS` — the large-font list, ranks 1-10.</summary>
    public IReadOnlyList<HighScoreEntry> Today => _today;

    /// <summary>ROM `CMSCOR` — the small-font list, ranks 2-37 (rank 1 is <see cref="Top"/>).</summary>
    public IReadOnlyList<HighScoreEntry> AllTime => _allTime;

    /// <summary>What offering a finished score did — the ROM's `GODCHK`/`TODCHK`/`ALLCHK` answers.</summary>
    /// <param name="BecomesTop">True when the score beat the top entry and took its place (`GODCHK`).</param>
    /// <param name="EnteredToday">True when TODAY's list took the score (`TODCHK`).</param>
    /// <param name="EnteredAllTime">True when the all-time list took the score (`ALLCHK`).</param>
    /// <param name="EntriesMaximum">True when the per-initials cap applied, which is what the ONLY5P page explains (`SETBOT`/`SETBZZ`).</param>
    public readonly record struct SubmitResult(bool BecomesTop, bool EnteredToday, bool EnteredAllTime, bool EntriesMaximum);

    /// <summary>ROM `DEFHSR`/`DEFGOD` — the factory "GOD" score: "WILLY ELKTRIX", 151782.</summary>
    public static TopScoreEntry FactoryTop { get; } = new("WILLY ELKTRIX", 151782);

    /// <summary>
    /// ROM `TODTAB` — today's list at power-up. Read out of the ROM byte for byte:
    /// DRJ 52127, LED 50218, EPJ 41255, JER 41250, KID 31920, MLG 31919, SSR 26645,
    /// UNA 26635, JRS 25250, CJM 24110.
    /// </summary>
    public static IReadOnlyList<HighScoreEntry> FactoryToday { get; } =
    [
        new("DRJ", 52127),
        new("LED", 50218),
        new("EPJ", 41255),
        new("JER", 41250),
        new("KID", 31920),
        new("MLG", 31919),
        new("SSR", 26645),
        new("UNA", 26635),
        new("JRS", 25250),
        new("CJM", 24110),
    ];

    /// <summary>
    /// ROM `DEFHSR` + `DEFSC2` — the factory all-time list, highest first (the
    /// operator's GOD score is the rank-1 entry, so the list starts at rank 2).
    /// </summary>
    public static IReadOnlyList<HighScoreEntry> FactoryAllTime { get; } =
    [
        new("VID", 122145),
        new("KID", 122135),
        new("DON", 18280),
        new("VIV", 18280),
        new("GWW", 18105),
        new("CRB", 18055),
        new("MDR", 17565),
        new("BAC", 17256),
        new("W R", 17070),
        new("MPT", 16060),
        new("SUE", 15520),
        new("MOM", 14480),
        new("DAD", 14479),
        new("SFD", 14478),
        new("AKD", 14477),
        new("CWK", 13330),
        new("TMH", 13270),
        new("EJS", 13120),
        new("RAY", 13065),
        new("GAY", 12965),
        new("RKM", 12855),
        new("CNS", 12755),
    ];

    /// <summary>The ROM's factory table: today's list, the all-time list and the top entry.</summary>
    public static HighScoreTable CreateFactory() =>
        new(FactoryTop, FactoryToday, FactoryAllTime);

    /// <summary>
    /// Builds a table from what the store had: TODAY'S always comes from the
    /// factory table (the ROM reloads it at power-up), the all-time list and the
    /// top entry come from the saved data when there is any.
    /// </summary>
    public static HighScoreTable FromSaved(TopScoreEntry? top, IReadOnlyList<HighScoreEntry>? allTime) =>
        new(top ?? FactoryTop, FactoryToday, Pad(allTime, AllTimeCapacity, FactoryAllTime));

    /// <summary>Whether a finished score beats any of TODAY's ten entries (ROM `TODCHK`).</summary>
    public bool QualifiesForToday(int score) => Beats(_today, score);

    /// <summary>Whether a finished score beats the top entry or any all-time entry (ROM `GODCHK` and `ALLCHK`).</summary>
    public bool QualifiesForAllTime(int score) => score > Top.Score || Beats(_allTime, score);

    /// <summary>Whether a finished score earns an initials screen at all (ROM `EGSUB1`: `TODCHK`, then `ALLCHK`).</summary>
    public bool Qualifies(int score) => QualifiesForToday(score) || QualifiesForAllTime(score);

    /// <summary>
    /// Offers a finished score under the initials its player entered (ROM `EGSUB`). A score that
    /// beats the top becomes the new top, the old top dropping into the all-time list's rank 1, and
    /// is then offered to TODAY's list as well; any other score is offered to BOTH lists, and each
    /// takes it when it beats that list's lowest entry.
    /// </summary>
    /// <param name="score">The score to offer.</param>
    /// <param name="initials">The name to enter it under — <see cref="InitialsLength"/> characters, as the CONG screen's entry produces.</param>
    public SubmitResult Submit(int score, string initials)
    {
        if (score > Top.Score)
        {
            TakeTopEntry(score, initials);
            bool today = Insert(_today, new HighScoreEntry(initials, score), TodayCapacity);
            return new SubmitResult(
                BecomesTop: true,
                EnteredToday: today,
                EnteredAllTime: false,
                EntriesMaximum: EnforceAllTimeInitialsCap(initials));
        }

        int cap = TopCarriesInitials(initials) ? AllTimeTopInitialsCap : AllTimeInitialsCap;
        bool intoToday = Insert(_today, new HighScoreEntry(initials, score), TodayCapacity);
        (bool intoAllTime, bool maximum) = InsertAllTime(score, initials, cap);
        return new SubmitResult(
            BecomesTop: false,
            EnteredToday: intoToday,
            EnteredAllTime: intoAllTime,
            EntriesMaximum: maximum);
    }

    /// <summary>
    /// ROM `GODCHK`: the old top's initials and score move into the all-time list's rank 1 (the list's
    /// last entry drops off) and the new score takes the top under the initials just entered.
    /// </summary>
    private void TakeTopEntry(int score, string initials)
    {
        _allTime.Insert(0, new HighScoreEntry(InitialsOf(Top.Name), Top.Score));
        Fill(_allTime, AllTimeCapacity);
        Top = new TopScoreEntry(initials, score);
    }

    /// <summary>
    /// ROM `ALLCHK` → `SETBOT` → `SCTRNS`: inserts when the score beats the list's lowest entry,
    /// having first applied the per-initials cap — a full set of initials can only be beaten from
    /// inside, by outscoring the lowest of them, which is the entry the score replaces.
    /// </summary>
    private (bool Entered, bool Maximum) InsertAllTime(int score, string initials, int cap)
    {
        if (!Beats(_allTime, score))
        {
            return (false, false);
        }

        bool atCap = CountInitials(_allTime, initials) >= cap;
        if (atCap)
        {
            int lowest = LowestInitialsIndex(_allTime, initials);
            if (score <= _allTime[lowest].Score)
            {
                return (false, true);
            }

            _allTime.RemoveAt(lowest);
        }

        return (Insert(_allTime, new HighScoreEntry(initials, score), AllTimeCapacity), atCap);
    }

    /// <summary>
    /// ROM `GETHM3`'s top-entry branch (`LDA #5 / BSR SETBZZ`): the initials a score has just put on
    /// the top entry are capped in the list beneath it, and the fifth match is removed.
    /// </summary>
    private bool EnforceAllTimeInitialsCap(string initials)
    {
        if (CountInitials(_allTime, initials) < AllTimeInitialsCap)
        {
            return false;
        }

        _allTime.RemoveAt(LowestInitialsIndex(_allTime, initials));
        return true;
    }

    /// <summary>ROM `SETBOT`'s first test: whether the entered initials are the top entry's own.</summary>
    private bool TopCarriesInitials(string initials) => InitialsOf(Top.Name) == initials;

    /// <summary>The three initials a name starts with (the ROM's `GODINT`, three CMOS characters).</summary>
    private static string InitialsOf(string name) =>
        name.Length >= InitialsLength ? name[..InitialsLength] : name.PadRight(InitialsLength);

    /// <summary>Whether a score beats a full list's lowest entry — the ROM's `TODCK1`/`ALCK1` walk to the bottom.</summary>
    private static bool Beats(List<HighScoreEntry> list, int score) => score > list[^1].Score;

    /// <summary>How many of a list's entries carry these initials.</summary>
    private static int CountInitials(List<HighScoreEntry> list, string initials)
    {
        int count = 0;
        foreach (HighScoreEntry entry in list)
        {
            if (entry.Initials == initials)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>The index of the list's lowest-scoring entry carrying these initials — the fifth `SETBOT` finds.</summary>
    private static int LowestInitialsIndex(List<HighScoreEntry> list, string initials) =>
        list.FindLastIndex(entry => entry.Initials == initials);

    /// <summary>
    /// Inserts in descending order when the score beats the list's lowest entry
    /// (or the list has room), then truncates — the ROM's "bubble down, drop the
    /// bottom" rule (`BUBDN` + `SCTRNS`).
    /// </summary>
    private static bool Insert(List<HighScoreEntry> list, HighScoreEntry entry, int capacity)
    {
        Fill(list, capacity);
        if (list.Count >= capacity && entry.Score <= list[^1].Score)
        {
            return false;
        }

        int at = list.FindIndex(e => entry.Score > e.Score);
        list.Insert(at < 0 ? list.Count : at, entry);
        if (list.Count > capacity)
        {
            list.RemoveAt(list.Count - 1);
        }

        return true;
    }

    private static List<HighScoreEntry> Pad(IReadOnlyList<HighScoreEntry>? entries, int capacity, IReadOnlyList<HighScoreEntry> fallback)
    {
        List<HighScoreEntry> source = entries is { Count: > 0 } ? [.. entries] : [.. fallback];
        Fill(source, capacity);
        return source;
    }

    /// <summary>Pads a list out to the ROM's full table with its blank entry (`NULSCR`).</summary>
    private static void Fill(List<HighScoreEntry> list, int capacity)
    {
        while (list.Count < capacity)
        {
            list.Add(HighScoreEntry.Blank);
        }

        if (list.Count > capacity)
        {
            list.RemoveRange(capacity, list.Count - capacity);
        }
    }
}
