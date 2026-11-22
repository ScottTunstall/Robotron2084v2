# Review: Entities/Models (non-recursive)

## Scope

Reviewed the C# files directly contained in
`src/Robotron2084/Entities\Models` and inspected in-repository references and tests.
Nested directories were not reviewed.

Relevant model tests under `tests/Robotron2084.Tests/Entities/Models` were checked,
along with consumers in entity, spawning, tuning, and strip-effect code.

## Findings

### High: `GorfDropParams(0, ...)` can reach a `Random.Next(0)` crash

- **References:** `GorfDropParams.cs:21-29` allows `maxDropsX2 == 0`;
  `Gorf.cs:318-323` calls `_random.Next(_maxDropsX2)` for a normal chance drop.
- `MaxDropsX2` is otherwise described as a limit, and zero is accepted by the constructor.
  The first time a Gorf reaches one of its three chance drops, `Random.Next(0)` throws
  `ArgumentOutOfRangeException`.
- Tests cover negative values but do not cover zero.

**Fix:** Either require `maxDropsX2 >= 1` with a clear exception (and update the model/tests),
or define zero as “drop nothing” and make `GetDropCount` honor it explicitly without calling
`Random.Next`.

### Medium: Several public enums silently accept invalid numeric values

- **References:** `FamilyMemberKindExtensions.cs:15-33` uses `_ => Daddy`/`Daddy collision size`;
  `StripEffectTuning.cs:119-125` maps every non-Horizontal/non-Diagonal engine to `null`;
  `Prog.cs:400-405` and `Brain.cs:458-480` derive animation indices by casting enum values;
  `WalkSequence.cs:18-34` defines valid values 0-3.
- Casting `(FamilyMemberKind)123`, `(StripEngine)123`, or `(WalkSequence)123` is legal in C#.
  It can therefore silently select Daddy, make a strip pool look “vertical/unlimited,” or compute
  an index outside the animation-frame array.
- The numeric values are currently implicit for all of these enums, so no explicit serialized
  or persisted value contract protects callers.

**Fix:** Add explicit enum cases and reject unknown values with
`ArgumentOutOfRangeException` (or `InvalidOperationException` at an internal invariant boundary).
Do not use `_` as a valid fallback for public enum parameters. Where serialization is possible,
use named/string values or `Enum.IsDefined` validation before switching.

### Medium: Strip and clip record structs do not enforce their documented invariants

- **References:** `Strip.cs:24` has a positional `readonly record struct` constructor with no
  validation for negative `SourceIndex` or unusable coordinates.
- `StripClip.cs:30-42` has no validation that `MinX <= MaxX`, `MinY <= MaxY`, or that the
  rectangle has area.
- `StripClip.CreateFromPortPixels` floors all four boundaries; it does not preserve the
  half-open `[Left, Right)`/`[Top, Bottom)` meaning described for clips.
- Tests in `EntityOwnRulesTests.cs:141-149` lock in plain integer truncation for aligned
  example bounds but do not test unaligned or reversed bounds.

**Fix:** Add explicit constructors/`ThrowIf...` validation to `StripClip` and `Strip`.
For port-pixel conversion, derive left/top with floor and right/bottom with ceiling (when
boundary semantics require an exact arcade-pixel clip), and reject negative/reversed results.
Update tests to cover unaligned and invalid bounds.

### Medium: `GorfDropParams` permits values that cannot be spaced or fulfilled sensibly

- **References:** `GorfDropParams.cs:21-29` imposes only “>= 0” checks;
  `Gorf.cs:110-116` derives `_totalSteps / (gruntDropCount + 1)`;
  `Gorf.cs:333-341` enqueues `drop * _dropIntervalSteps`.
- With a large `gruntDropCount`, the derived interval becomes zero or duplicates the same step;
  `gruntDropCount` can also extend beyond the Gorf's crossing, leaving owed drops to be scheduled
  on a return. This is not invalid by the constructor, but it has no upper-bound/capacity
  invariant and can change the state machine from gradual drops to simultaneous drops.
- `_dropIntervalSteps` is allowed independently of `gruntDropCount`, and no relationship is
  checked for explicit intervals.

**Fix:** Reject combinations that cannot meet the promised spacing within the crossing, or
clamp/derive with a `>= 1` interval and account for route capacity. Define and test the maximum
drop count and exact rules for converting omitted intervals.

### Low: Cross-parameter invariants are not enforced for playfield and aim models

- **References:** `GorfPlayfieldParams.cs:17-28` validates areas but does not require
  `playerBoxToAvoid` to lie within `Bounds`.
- `TankShellAimParams.cs:16-27` validates shell speed and playfield area but does not require
  `PlayerPosition` to lie within `PlayfieldBounds`.
- A caller can therefore construct a Gorf that is told to avoid an unrelated box or an aim whose
  target is outside the playfield. The current consumers may still have valid data, but the
  public constructors do not preserve the documented spatial relationship.

**Fix:** Decide whether out-of-bounds boxes/positions are intentionally supported (for example,
player position can be at an edge). If not, reject them by the appropriate rectangle-containment
rule and add tests; if they are supported, document the behavior explicitly.

## Serialization / Record Concerns

The sealed parameter records (`EntityParams`, `BrainWaveParams`, `QuarkWaveParams`,
`GorfDropParams`, `GorfPlayfieldParams`, and `TankShellAimParams`) use get-only properties and
constructor validation. That validation is lost if they are deserialized by a general-purpose
serializer without using these constructors or are mutated via reflection; no such serialization
usage was found for these types in the reviewed source.

`EntityParams` is a sealed record containing mutable/reference dependencies. Its constructor
blocks null dependencies and is the stronger invariant boundary; record equality remains
reference-based for `IGameEvents`, `Random`, and `TextureSet`.

`ProgGhost` is internal and is only created from valid live Prog state. `StripClip`, `Strip`, and
the public enum types are the more important invalid-state boundaries identified above.

## Summary

The most actionable defect is the accepted `GorfDropParams.MaxDropsX2 == 0` value reaching
`Random.Next(0)`. The public enums and public strip/clip record structs also lack explicit
invalid-value rejection, and Gorf drop planning has cross-field values that are not bounded.

Most constructor-backed model tests verify null/negative/empty invariants and cover their
documented happy paths. Missing coverage is concentrated around zero Gorf limits, unknown
enum values, unaligned/reversed clip bounds, and cross-field spatial/drop-count relationships.
