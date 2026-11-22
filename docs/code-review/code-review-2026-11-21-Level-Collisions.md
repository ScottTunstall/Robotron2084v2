# Code Review: Direct C# Collisions Code

## Scope

Reviewed C# files directly under `src/Robotron2084/Level/Collisions`. The nested
`Collisions/Models` folder was reviewed separately.

## Findings

### Low — Collision-result dispatch requires exact runtime types

- **References:** `CollisionResponder.cs:44-49`.
- The responder looks up actions with `collisionResult.GetType()`, not assignability or a
  polymorphic contract.
- `CollisionResult` types are currently concrete and only this module creates them, so the
  dispatch is complete today. However, a future derived result that has an `is a` relationship
  to a base result will be rejected instead of using the base behavior.

**Fix:** Either make result types sealed, document exact-type dispatch, or provide an explicit
registered interface/base-type lookup. Add an argument-null check before dereferencing the result.

### Low — Multiple lazy collision results can observe/respond to already-mutated state

- **References:** `LaserCollisionRule.cs:10-21`,
  `RobotVsElectrodeCollisionRule.cs:12-31`, `PlayField.cs:937-945`.
- Detection uses deferred `IEnumerable<CollisionResult>` enumeration while
  `ResolveCollisions` responds immediately after each `yield`.
- This ordering is deliberate and important for consuming a laser once, but it is implicit:
  later `MoveNext` calls in the same rule observe state changed by earlier responses. It can
  be confusing and fragile if a rule is changed to materialize or reorder results.

**Fix:** Keep this behavior documented and covered by tests, or snapshot each rule's input
first and apply responses in a second pass. If a second-pass design is chosen, preserve the
explicit arcade rule order and first-hit/laser-consumption semantics.

## Summary

No unresolved collision mutation or ordering defect was established beyond these dispatch and
lazy-enumeration maintenance risks. The collision order, single-player-hit checks, live-only
resolution, and player hit-stop bookkeeping match the stated arcade behavior.
