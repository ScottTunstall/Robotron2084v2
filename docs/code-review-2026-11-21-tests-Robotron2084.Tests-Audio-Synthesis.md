# Test review: Audio\Synthesis

## Scope

Reviewed C# test files in this folder only. Summary: 
SoundBoardTests.cs

## Findings

- The suite renders deterministic sample buffers and found a current setup
  failure: `WaveTableData` static initialization throws `NullReferenceException`
  in shell, coin, laser, and stop-board cases. This causes the synthesis suite
  to fail rather than report an assertion about the sound behavior.
- `Loudness` allocates a full sample array for each timing window and has no
  guard for a zero/negative duration. The thresholds are fixed and not wall-clock
  dependent.
- Setup is generally local and deterministic; where real time is modeled, tests advance fixed `GameTime`/ticks rather than using `Task.Delay` or `Thread.Sleep`.
- Assertions are mostly exact or use theory/boundary checks. No obvious cross-test order dependency was found in this folder, though shared static fixtures should not be mutated.

## Flakiness and isolation

- **Flakiness:** No wall-clock waits or uncontrolled random sources were identified for these files; fixed seeds or deterministic scripts dominate.
- **Isolation:** The main risk is mutable static/shared production state or external files. Cleanup is only relevant where temporary files/directories are used.
- **Weak assertions:** A few boolean-only checks are paired with setup comments or adjacent state checks; improve diagnostics where failure is ambiguous.

## Missing edge coverage

- Define whether synthesis tests require external ROM/setup data. Provide
  deterministic embedded setup or skip consistently before this suite can fail
  from static initialization.
- Cover replacement of every voice, buffer lengths, and zero-sample rendering.

## Overall assessment

The folder has useful targeted coverage, with exact state/count assertions and boundary theories. Remaining value should come from the edge cases above rather than broad smoke tests.
