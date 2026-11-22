# Test review: Persistence

## Scope

Reviewed C# test files in this folder only. Summary: 
ControlSettingsStoreTests.cs, GameSettingsStoreTests.cs, GameSettingsTests.cs, HighScoreTableTests.cs

## Findings

- 
Persistence tests isolate disk I/O through GUID temp paths and mostly clean up. A locked-file test is platform-sensitive; round-trip tests cover common corruption. High-score tests assert list lengths and ordering.
- Setup is generally local and deterministic; where real time is modeled, tests advance fixed `GameTime`/ticks rather than using `Task.Delay` or `Thread.Sleep`.
- Assertions are mostly exact or use theory/boundary checks. No obvious cross-test order dependency was found in this folder, though shared static fixtures should not be mutated.

## Flakiness and isolation

- **Flakiness:** No wall-clock waits or uncontrolled random sources were identified for these files; fixed seeds or deterministic scripts dominate.
- **Isolation:** The main risk is mutable static/shared production state or external files. Cleanup is only relevant where temporary files/directories are used.
- **Weak assertions:** A few boolean-only checks are paired with setup comments or adjacent state checks; improve diagnostics where failure is ambiguous.

## Missing edge coverage

- 
Test corrupt JSON/INI edge forms, directory access, trailing bytes, names outside allowed width, exact lower-bound equality, and cleanup if setup throws.

## Overall assessment

The folder has useful targeted coverage, with exact state/count assertions and boundary theories. Remaining value should come from the edge cases above rather than broad smoke tests.
