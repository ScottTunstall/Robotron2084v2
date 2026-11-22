# Test review: Entities

## Scope

Reviewed C# test files in this folder only. Summary: 
BerzerkRobotTests.cs, ElectrodeSizeTests.cs, EntityGameEventTests.cs, EntityOwnRulesTests.cs, GorfTests.cs, GruntAnimationTests.cs, GruntSpeedProgressTests.cs, HulkAnimationTests.cs, HulkKnockbackTests.cs, LaserSlotsTests.cs, PlayerAnimationAndAimTests.cs, PlayerDeathTests.cs, PlayerInvincibilityTests.cs, ProgExplosionBoundsTests.cs, QuarkDropTimerTests.cs, QuarkTankBehaviourTests.cs, ScoreBurstTests.cs, SparkLimitTests.cs, SparkTests.cs, SpheroidAnimationTests.cs, SpheroidEnforcerTimingTests.cs, SpheroidEscapeTests.cs, StripEffectTests.cs, TankShellTests.cs

## Findings

- 
Entity suites use fixed ticks and seeded builders for timing-sensitive movement. Several animation tests assert exact phase intervals and state transitions. Static texture/game-time fixtures are immutable.
- Setup is generally local and deterministic; where real time is modeled, tests advance fixed `GameTime`/ticks rather than using `Task.Delay` or `Thread.Sleep`.
- Assertions are mostly exact or use theory/boundary checks. No obvious cross-test order dependency was found in this folder, though shared static fixtures should not be mutated.

## Flakiness and isolation

- **Flakiness:** No wall-clock waits or uncontrolled random sources were identified for these files; fixed seeds or deterministic scripts dominate.
- **Isolation:** The main risk is mutable static/shared production state or external files. Cleanup is only relevant where temporary files/directories are used.
- **Weak assertions:** A few boolean-only checks are paired with setup comments or adjacent state checks; improve diagnostics where failure is ambiguous.

## Missing edge coverage

- 
Check spawn, death, respawn, pool exhaustion, off-screen, and negative-speed boundaries for the remaining entity types.

## Overall assessment

The folder has useful targeted coverage, with exact state/count assertions and boundary theories. Remaining value should come from the edge cases above rather than broad smoke tests.
