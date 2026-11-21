# Test review: States

## Scope

Reviewed C# test files in this folder only. Summary: 
AttractNavigationTests.cs, AttractScreenTests.cs, FakeClosableGameState.cs, GameStateManagerTests.cs, MainMenuStateIdleSpeechTests.cs, MainMenuStateSoundTests.cs, MainMenuStateTests.cs, PlayingStateChickenTests.cs, PlayingStateStartSoundTests.cs, PlayingStateTurnMessageTests.cs, SpeechTestStateTests.cs, SpriteTestAnimatorTests.cs, SpriteTestEntriesTests.cs, SpriteTestWalkOnRunTests.cs

## Findings

- 
State suites use fixed game time and shared SoundFacade collection. IDisposable implementations reset global speech in key suites; fake input and textures reduce external dependencies.
- Setup is generally local and deterministic; where real time is modeled, tests advance fixed `GameTime`/ticks rather than using `Task.Delay` or `Thread.Sleep`.
- Assertions are mostly exact or use theory/boundary checks. No obvious cross-test order dependency was found in this folder, though shared static fixtures should not be mutated.

## Flakiness and isolation

- **Flakiness:** No wall-clock waits or uncontrolled random sources were identified for these files; fixed seeds or deterministic scripts dominate.
- **Isolation:** The main risk is mutable static/shared production state or external files. Cleanup is only relevant where temporary files/directories are used.
- **Weak assertions:** A few boolean-only checks are paired with setup comments or adjacent state checks; improve diagnostics where failure is ambiguous.

## Missing edge coverage

- 
Cover first/last menu items, simultaneous navigation actions, repeated speech ticks, dispose-before-update, and parallel state-manager teardown.

## Overall assessment

The folder has useful targeted coverage, with exact state/count assertions and boundary theories. Remaining value should come from the edge cases above rather than broad smoke tests.
