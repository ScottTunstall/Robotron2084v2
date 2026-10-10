namespace Robotron2084.Persistence;

/// <summary>What offering a finished score did — the ROM's `GODCHK`/`TODCHK`/`ALLCHK` answers.</summary>
/// <param name="BecomesTop">True when the score beat the top entry and took its place (`GODCHK`).</param>
/// <param name="EnteredToday">True when TODAY's list took the score (`TODCHK`).</param>
/// <param name="EnteredAllTime">True when the all-time list took the score (`ALLCHK`).</param>
/// <param name="ReachedEntriesMaximum">
///     True when the per-initials cap applied, which is what the ONLY5P page explains
///     (`SETBOT`/`SETBZZ`).
/// </param>
public readonly record struct SubmitResult(
    bool BecomesTop,
    bool EnteredToday,
    bool EnteredAllTime,
    bool ReachedEntriesMaximum);
