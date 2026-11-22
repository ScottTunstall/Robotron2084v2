# Code Review: Direct C# Level Code

## Scope

Reviewed C# files directly under `src/Robotron2084/Level` (23 files). Nested directories
under `Level/Collisions`, `Level/Models`, and `Level/Spawning` were not included in this review.

## Findings

### Medium — Degenerate playfield sizes can throw instead of reaching the documented fallback

- **References:** `SpawnPlacement.cs:119-123`, `SpawnPlacement.cs:63-74`.
- `PickRandomPointInside` passes `_random.Next(min, max)` directly, where `max` is
  `_playfieldBounds.Right - size` or `_playfieldBounds.Bottom - size`.
- If a caller supplies a field too narrow or short for the requested square, `max <= min`
  makes `Random.Next` throw `ArgumentOutOfRangeException`. Execution therefore never reaches
  the grid scan or the `PickRandomPointInside` last-resort fallback described in the API.
- This also affects the standard, electrode, family, and hulk spawn paths that use
  `SpawnPlacement`, because each ultimately reaches this method.

**Fix:** Validate `size <= bounds.Width` and `size <= bounds.Height` at the placement
boundary, or make `PickRandomPointInside` return a clearly valid clamped result.
The fallback contract should be implemented before attempting random ranges that cannot
satisfy `Random.Next`'s preconditions.

### Medium — Custom wave CSV values are not constrained to valid counts or limits

- **References:** `WaveParamsGenerator.cs:93-121`, `WaveParamsGenerator.cs:129-151`.
- `TryParseRow` accepts every successfully parsed integer, including negative counts and
  negative/zero `MaxEnforcersPerSpheroid` or `MaxTanksPerQuark`.
- Negative counts simply cause no entities to spawn for that kind, while zero/negative drop
  limits can reach entity constructors or `Random.Next(limit)` in live wave behavior.
- Malformed negative data is consequently not “damaged” for the whole-table fallback path;
  it silently replaces the arcade table.

**Fix:** Apply a non-negative lower bound to every custom count and positive bounds to drop
limits, and reject the whole row/table if invariants fail. Include tests for zero, negative,
and extreme values.

### Low — `WaveTable` exposes mutable backing arrays for its deterministic data

- **References:** `WaveTable.cs:313-338` and all static `int[]` table fields.
- The arcade tables are public static `readonly int[]` fields. The array references cannot be
  reassigned, but their elements remain writable to any caller.
- A mutation from test, tooling, or an external caller changes every later
  `WaveTable.GetParameters` result and therefore changes generated levels and Random consumption.

**Fix:** Expose immutable/read-only views or make the table internals private and provide a
single immutable `WaveTableRow` API. Keep a build-time table-length and range invariant.

## Summary

The main level-state concerns are the placement fallback on degenerate bounds and the lack of
custom-wave-range validation. The reviewed update order, wave-clear accounting, hit-stop,
wall/update, and next-life state capture are otherwise deliberately structured and consistent.
