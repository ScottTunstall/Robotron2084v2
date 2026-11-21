# Code Review: Direct C# Level/Spawning Code

## Scope

Reviewed C# files directly under `src/Robotron2084/Level/Spawning`. The nested
`Spawning/Models` folder was reviewed separately.

## Findings

### Medium — Placement can throw before fallback on small or degenerate fields

- **References:** `SpawnPlacement.cs` (used by all wave spawners),
  `GruntWaveSpawner.cs:15-24`, `ElectrodeWaveSpawner.cs:16-31`,
  `FamilyWaveSpawner.cs:22-34`, `TankWaveSpawner.cs:15-25`.
- Random placement calls `Random.Next` even when the requested entity size does not fit the
  playfield. This bypasses the grid fallback and can throw instead of producing a clamped or
  rejected placement.
- Quark and spheroid placement have additional direct/random expressions that assume the
  playfield is at least a normal entity size.

**Fix:** Validate field and entity dimensions before choosing candidates. Implement one
explicit placement contract: throw for impossible configuration at construction, or clamp the
playfield edge and return a documented fallback point.

### Low — Random placement does not include the exact right/bottom edge

- **References:** `SpawnPlacement.cs:119-123`.
- `Random.Next(min, max)` has an exclusive upper bound. Passing
  `_playfieldBounds.Right - size` excludes the top-left position that places a square exactly
  at the right/bottom edge.
- The grid fallback can return edge-aligned positions, so random placement and fallback occupy
  different legal ranges.

**Fix:** If the right/bottom edge is legal, use an inclusive-to-exclusive range of
`Right - size + 1` for X and `Bottom - size + 1` for Y.

### Low — Shared mutable Random consumption makes spawn order observable

- **References:** `MidWaveSpawner.cs:24-30`, `HulkWaveSpawner.cs:59-72`,
  `QuarkWaveSpawner.cs:18-25`, `BrainWaveSpawner.cs:15-35`.
- SpawnPlacement, hulk targeting, quarks, mid-wave effects, and other entity code share the
  field's `Random` instance. Adding or changing one spawner changes the seeds consumed by later
  spawners and hence reproducible level layouts.
- The implementation has no independent seed or per-kind stream to isolate ordering changes.

**Fix:** If deterministic replay or stable scenario snapshots are required, document this
shared-stream contract and seed all field randomness explicitly. Consider per-subsystem streams
only if replay stability across content changes is more important than preserving the current
random sequence.

## Summary

The spawning code is consistent about keeping many entities inside the playfield and adds family
members after robots. The most actionable correctness issue is defensive placement for fields
too small to contain a requested entity; custom negative drop values should also be rejected
before reaching spawner and entity constructors.
