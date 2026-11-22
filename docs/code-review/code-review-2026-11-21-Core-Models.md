# Core Models Code Review

## Scope

C# model implementation in `src/Robotron2084/Core/Models`:

- `Win32Rect.cs`
- `ScaleMode.cs`
- `IntVector2.cs`
- `Direction8.cs`

## Findings

Findings are ordered by severity.

### Medium

1. **`ComputeDistanceSquared` does not fully avoid overflow for opposite extreme coordinates.**
   - File/lines: `src/Robotron2084/Core/Models/IntVector2.cs:43-48`
   - Widening `a.X - b.X` to `long` works for the subtraction, but the subsequent square can exceed `long.MaxValue` when two `int` coordinates are at opposite extremes. This contradicts the summary that distance calculations are overflow-safe.
   - Fix: document the supported coordinate range, perform saturating arithmetic, or use `BigInteger`/checked operations and a clear overflow policy. For this game's bounded 640×400 coordinates, a range check is also reasonable.

### Low

1. **`GetManhattanDistance` can overflow.**
   - File/lines: `src/Robotron2084/Core/Models/IntVector2.cs:55-58`
   - The difference for each axis is evaluated in `int`, and the final sum is `int`; coordinate combinations at opposite extremes can wrap even though a long result could represent the true distance.
   - Fix: widen each subtraction and sum to `long`, and consider changing the return type to `long`.

2. **`IsFartherThan` does not define behavior for negative distance thresholds.**
   - File/lines: `src/Robotron2084/Core/Models/IntVector2.cs:61-64`
   - Its public contract does not say that `distance` must be non-negative. Squaring a negative threshold causes the implementation to compare against a positive number, so the intuitive result for a negative threshold would be wrong.
   - Fix: treat negative thresholds as already satisfied and return `true`, or throw `ArgumentOutOfRangeException`.

3. **`Win32Rect.GetWidth` and `GetHeight` can return negative values.**
   - File/lines: `src/Robotron2084/Core/Models/Win32Rect.cs:29-38`
   - If a malformed or failed Win32 response is represented by reversed edges, the methods return negative geometry. `DisplayInfo` currently guards this case, but the model itself has no invariant.
   - Fix: document that the fields are not validated, or have the methods return `0` for `Right < Left` and `Bottom < Top`.

## Summary

- `IntVector2` arithmetic is useful and compact, but its overflow guarantees are incomplete for extreme `int` values.
- `Direction8` and `ScaleMode` are simple type definitions with no implementation defects found.
- No source files were changed.

## Test gaps

- Add `IntVector2` tests for `int.MinValue`, `int.MaxValue`, and opposite-extreme coordinate pairs.
- Test addition, subtraction, scalar multiplication, and squared distance for overflow behavior and document the chosen policy.
- Add tests for `GetManhattanDistance` and `IsFartherThan`, including negative thresholds.
- Test `Win32Rect.GetWidth`/`GetHeight` with normal and reversed edges.
- Existing `Direction8Tests` cover sign mapping, opposite directions, and unit vectors, but not invalid enum values.
