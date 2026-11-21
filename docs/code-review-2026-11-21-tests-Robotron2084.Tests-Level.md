# Test review: Level

## Scope

Reviewed C# test files in this folder only. Summary: 
BozoModeTests.cs, DifficultyTuningTests.cs, FamilyMemberSpawnTests.cs, FamilySavedTests.cs, FieldStrategyTests.cs, GameSessionTests.cs, HulkTargetTests.cs, LivesCounterTests.cs, MaterialisationTests.cs, PixelCollisionTests.cs, PlayerAppearTests.cs, PlayFieldBrainProgMissileTests.cs, PlayFieldCollisionTests.cs, PlayFieldFamilyMemberTests.cs, PlayfieldLayoutTests.cs, PlayFieldMovementTests.cs, PlayFieldNearestRobotTests.cs, PlayFieldNextLifeTests.cs, PlayFieldRobotRegistryTests.cs, PlayFieldSpawnTests.cs, RobotFirstMoveTests.cs, RobotTauntSentencesTests.cs, RobotTauntsTests.cs, RobotTransporterTests.cs, WallDoorTests.cs, WaveParamsGeneratorTests.cs, WaveStartedEventTests.cs, WaveStartSequenceTests.cs, WaveStartSpeechTests.cs, WaveTableTests.cs

## Findings

- 
The largest suite is heavily timing-based but advances fixed GameTime/arcade ticks rather than wall clocks. Spawn and collision tests use seeded deterministic PlayFields. Temp CSV tests use GUID names and cleanup.
- Setup is generally local and deterministic; where real time is modeled, tests advance fixed `GameTime`/ticks rather than using `Task.Delay` or `Thread.Sleep`.
- Assertions are mostly exact or use theory/boundary checks. No obvious cross-test order dependency was found in this folder, though shared static fixtures should not be mutated.

## Flakiness and isolation

- **Flakiness:** No wall-clock waits or uncontrolled random sources were identified for these files; fixed seeds or deterministic scripts dominate.
- **Isolation:** The main risk is mutable static/shared production state or external files. Cleanup is only relevant where temporary files/directories are used.
- **Weak assertions:** A few boolean-only checks are paired with setup comments or adjacent state checks; improve diagnostics where failure is ambiguous.

## Missing edge coverage

- 
Prioritize wave-zero/negative, wraparound above 60, pool exhaustion, simultaneous collisions, malformed CSV columns/rows, and mid-wave state transitions.

## Overall assessment

The folder has useful targeted coverage, with exact state/count assertions and boundary theories. Remaining value should come from the edge cases above rather than broad smoke tests.
