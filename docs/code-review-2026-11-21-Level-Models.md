# Code Review: Direct C# Level/Models

## Scope

Reviewed C# files directly under `src/Robotron2084/Level/Models`. Nested directories were not
reviewed.

## Findings

### Medium — Public wave parameter values have no cross-field validation

- **References:** `WaveParams.cs`.
- `WaveParams` is a public positional record and its constructor has no validation for negative
  counts, negative timing values, negative shell speed, or inconsistent drop limits.
- Its creation from `WaveTable` is normally valid, but custom tables and test callers can
  construct states with impossible spawn counts, negative random ranges, or zero drop limits.
- Some consumers call `Random.Next`, so invalid values can change from a harmless ignored count
  to an exception depending on the kind.

**Fix:** Centralize validation or a factory for validated values. At minimum reject negative
counts and timings and require drop-limit values to be positive. Define whether
`MaxEnforcersPerSpheroid` and `MaxTanksPerQuark` must be consistent with `MaxDropsX2`.

### Low — Public enum and mutable data contracts can admit unknown values

- **References:** `PlayfieldEdge.cs`, `GameMode.cs`, `GameStyle.cs`, `RobotKind.cs`,
  `WallDoorSide.cs`, `BozoModeRow.cs`, `CustomWaveTableRow.cs`, `RobotKindRow.cs`,
  `WaveTableRow.cs`.
- C# enums remain castable to values outside their declared cases. Several consumers use
  switches or map lists by enum value, so an unknown numeric enum can silently select a
  default or out-of-range record.
- Custom and table rows also expose mutable array-backed or public record state in parts of
  the level model graph, making reproducibility depend on callers not mutating shared data.

**Fix:** Reject unknown enum values at public boundaries, and use immutable collections or
defensive copies for table and row data where callers do not need mutation.

## Summary

The model definitions are compact and align with wave/spawn concepts. The highest-priority
hardening work is constructor validation for `WaveParams` and explicit custom-table bounds;
enum and mutability checks would prevent later deterministic-behavior drift.
