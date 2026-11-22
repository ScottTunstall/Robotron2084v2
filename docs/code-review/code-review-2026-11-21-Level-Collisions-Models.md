# Review: Direct C# Level/Collisions/Models

## Scope

Reviewed C# files directly contained in
`src/Robotron2084/Level/Collisions/Models`. Nested directories were not present.

## Findings

### Low — Collision result records do not enforce their own non-null invariants

- The result types are small data records carrying entity, laser, electrode, robot-kind, or
  family-member references. Constructors generally expose only positional properties and do not
  check for null.
- The internal rules are the producer and currently pass live model objects, so normal runtime
  paths remain valid. Public/internal callers can nevertheless construct a result with null
  dependencies and pass it to `CollisionResponder.Respond`, delaying failure until response code
  dereferences it.

**Fix:** Add constructor validation for all entity dependencies, or make these result types
sealed and constructors internal if external construction is not an intended API. This is
especially important for `LaserHitResult`, brain-victim results, and player rescue results.

### Informational — State changes are encoded by reference

- Several records retain mutable entity objects rather than immutable snapshots. A brain,
  family member, laser, or robot can change state between result creation and response.
- This is useful for lazy collision resolution, but it means a result name alone does not fully
  describe the state at response time.

**Fix:** Document whether results are live references or point-in-time reports, and keep
dispatch tests focused on the mutation order they depend on.

## Summary

The model classes are appropriately small and map collision cases without extra mutation.
Their main hardening gap is constructor validation and an explicit reference-versus-snapshot
contract.
