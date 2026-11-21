# Core Extensions Code Review

## Scope

C# extension implementation in `src/Robotron2084/Core/Extensions`:

- `Direction8Extensions.cs`

## Findings

Findings are ordered by severity.

### Medium

No medium-severity findings were identified.

### Low

1. **Unknown `Direction8` values produce inconsistent extension behavior.**
   - File/lines: `src/Robotron2084/Core/Extensions/Direction8Extensions.cs:50-55`, `src/Robotron2084/Core/Extensions/Direction8Extensions.cs:58-70`
   - `GetOpposite` accepts every `int` cast to `Direction8`, but arithmetic modulo can produce an apparently valid yet unrelated direction. `ToIntVector` defensively maps unknown values to `IntVector2.Zero`; `GetOpposite` has no corresponding validation.
   - Fix: add an explicit enum validation helper and throw `ArgumentOutOfRangeException` for unknown values, or document that callers may pass unknown enum values and ensure both methods share the same defensive behavior.

## Summary

- `CreateFromDelta` correctly maps all signs for cardinal and diagonal directions, and `ToIntVector` is a simple integer sign conversion.
- The only issue is defensive handling of invalid cast enum values.
- No source files were changed.

## Test gaps

- Add tests for invalid cast values such as `(Direction8)(-1)` and `(Direction8)8` in `GetOpposite` and `ToIntVector`.
- Add a test for one axis at `int.MinValue`/`int.MaxValue` in `CreateFromDelta`; `Math.Sign` itself handles these values and the result should remain the correct sign.
