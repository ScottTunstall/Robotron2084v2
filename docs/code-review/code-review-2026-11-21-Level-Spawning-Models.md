# Review: Direct C# Level/Spawning/Models

## Scope

Reviewed C# files directly contained in `src/Robotron2084/Level/Spawning/Models`.

## Findings

### Medium — `WaveSpawnParams` preserves mutable dependencies and references

- **References:** `WaveSpawnParams.cs:23-53`.
- The record constructor validates nulls, but stores the live `PlayField`, `FieldEntities`,
  `SpawnPlacement`, `Random`, and an `IReadOnlyList<int>` as-is.
- `FamilySlotsLeftOver` is only compile-time read-only: its runtime implementation can be
  mutated from outside, and the `Random`/field dependencies are inherently mutable.
- This makes two spawns with equal record properties behave differently if shared state changed
  between calls.

**Fix:** Treat `WaveSpawnParams` as a live-operation context, not a value snapshot, or copy
the family-slot list and define record/equality semantics explicitly. Document that spawner
calls are intended to consume the current field and shared Random state.

### Low — Constructor validation can be bypassed only by reflection/serialization

- The record is positional with get-only properties and constructor dependencies. Normal
  construction uses its checks, but a general serializer or reflection could construct it
  without those checks.
- No serializer usage was found for this model in the reviewed level code.

**Fix:** Keep validation at the spawner boundary or use a plain immutable context object if
serialization is introduced later.

## Summary

`WaveSpawnParams` is a dependency bundle rather than a serialized model. It has appropriate
null guards for normal construction, but the shared and mutable reference dependencies should
be called out if replay/snapshot semantics are added.
