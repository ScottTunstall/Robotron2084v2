# Test review: AttractMode

## Scope

Reviewed C# test files in this folder only. Summary: 
AttractMovieSkipTests.cs, AttractMovieTests.cs, AttractStorylineTests.cs, MovieLaserWallTests.cs

## Findings

- 
Deterministic seeds and explicit frame loops make the attract scripts reproducible. Long 60,000/8,000-tick loops are assertions-backed rather than wall-clock sleeps, but they rely on exact ROM/constant state.
- Setup is generally local and deterministic; where real time is modeled, tests advance fixed `GameTime`/ticks rather than using `Task.Delay` or `Thread.Sleep`.
- Assertions are mostly exact or use theory/boundary checks. No obvious cross-test order dependency was found in this folder, though shared static fixtures should not be mutated.

## Flakiness and isolation

- **Flakiness:** No wall-clock waits or uncontrolled random sources were identified for these files; fixed seeds or deterministic scripts dominate.
- **Isolation:** The main risk is mutable static/shared production state or external files. Cleanup is only relevant where temporary files/directories are used.
- **Weak assertions:** A few boolean-only checks are paired with setup comments or adjacent state checks; improve diagnostics where failure is ambiguous.

## Missing edge coverage

- 
Add repeated-run checks only if the seed strategy is expanded; cover end-of-story, repeated skips, and boundary object allocation.

## Overall assessment

The folder has useful targeted coverage, with exact state/count assertions and boundary theories. Remaining value should come from the edge cases above rather than broad smoke tests.
