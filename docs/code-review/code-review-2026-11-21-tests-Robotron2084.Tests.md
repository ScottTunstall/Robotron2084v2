# Test review: (root)

## Scope

Reviewed C# test files in this folder only. Summary: FakeInputSource.cs,
NoTextureSource.cs, PlayFieldBuilder.cs, RomFiles.cs, ScriptedRandom.cs,
TestEntityParams.cs, TestTextures.cs, and WaveStartTicks.cs.

## Findings

- The helpers generally provide deterministic seeds, fixed textures, fake input,
  and shared playfield builders.
- `TestTextures.SharedTextureSet` is a static shared fixture; tests that mutate
  the texture set or its contents would introduce cross-test coupling.
- `RomFiles.ReadOrSkip` safely skips ROM-dependent tests when external ROM
  images are absent. Its base-directory search is resilient to nested output
  folders.
- `ScriptedRandom` should remain consumed as a sequence; exhausted sequences
  could accidentally expose shared-state assumptions in new tests.
- Setup is generally local and deterministic; where real time is modeled, tests advance fixed `GameTime`/ticks rather than using `Task.Delay` or `Thread.Sleep`.
- Assertions are mostly exact or use theory/boundary checks. No obvious cross-test order dependency was found in this folder, though shared static fixtures should not be mutated.

## Flakiness and isolation

- **Flakiness:** No wall-clock waits or uncontrolled random sources were identified for these files; fixed seeds or deterministic scripts dominate.
- **Isolation:** The main risk is mutable static/shared production state or external files. Cleanup is only relevant where temporary files/directories are used.
- **Weak assertions:** A few boolean-only checks are paired with setup comments or adjacent state checks; improve diagnostics where failure is ambiguous.

## Missing edge coverage

- Add tests that prove the fake/random helpers do not share mutable state.
- Cover exhausted scripted-random sequences and out-of-range ROM lookups.
- Keep builder defaults as explicit snapshots rather than mutable static values.

## Overall assessment

The folder has useful targeted coverage, with exact state/count assertions and boundary theories. Remaining value should come from the edge cases above rather than broad smoke tests.
