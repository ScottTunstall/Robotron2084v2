# Test review: Core

## Scope

Reviewed C# test files in this folder only. Summary: 
ArcadeRandomTests.cs, ScreenSizeTests.cs

## Findings

- 
ArcadeRandom uses fixed seeds and 100,000/20,000 samples, so it is deterministic rather than wall-clock flaky. Threshold assertions are broad enough to validate the distribution, but the statistical sample cost is high.
- Setup is generally local and deterministic; where real time is modeled, tests advance fixed `GameTime`/ticks rather than using `Task.Delay` or `Thread.Sleep`.
- Assertions are mostly exact or use theory/boundary checks. No obvious cross-test order dependency was found in this folder, though shared static fixtures should not be mutated.

## Flakiness and isolation

- **Flakiness:** No wall-clock waits or uncontrolled random sources were identified for these files; fixed seeds or deterministic scripts dominate.
- **Isolation:** The main risk is mutable static/shared production state or external files. Cleanup is only relevant where temporary files/directories are used.
- **Weak assertions:** A few boolean-only checks are paired with setup comments or adjacent state checks; improve diagnostics where failure is ambiguous.

## Missing edge coverage

- 
Cover limit=0, limit=1, negative limits, and integer overflow/boundary behavior if supported.

## Overall assessment

The folder has useful targeted coverage, with exact state/count assertions and boundary theories. Remaining value should come from the edge cases above rather than broad smoke tests.
