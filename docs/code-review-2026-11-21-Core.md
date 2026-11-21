# Core Code Review

## Scope

Top-level C# implementation in `src/Robotron2084/Core`:

- `ScreenSize.cs`
- `ArcadeRandom.cs`
- `ArcadeFrameTimer.cs`
- `DirectionSigns.cs`
- `DisplayInfo.cs`
- `ArcadeClock.cs`

The `Core/Extensions` and `Core/Models` subfolders are reviewed separately.

## Findings

Findings are ordered by severity.

### Medium

1. **`ArcadeRandom.PickUpTo` does not enforce or define valid limits.**
   - File/lines: `src/Robotron2084/Core/ArcadeRandom.cs:25-30`
   - The loop can return `1` even when `limit <= 0`, violating the public contract “from 1 up to the limit.” A limit above 255 is also not meaningful to this implementation: it draws only 0–255, so it cannot cover the full requested range.
   - Fix: validate `random != null` and `limit` in an explicit supported range (the arcade byte range is 1–256). If 256 is accepted, preserve the current draw-and-halving behavior; otherwise reject values outside 1–255. Throw `ArgumentOutOfRangeException` for invalid limits.

2. **The Windows work-area P/Invoke passes the wrong `uiParam`.**
   - File/lines: `src/Robotron2084/Core/DisplayInfo.cs:28`, `src/Robotron2084/Core/DisplayInfo.cs:39`
   - `SystemParametersInfo(SPI_GETWORKAREA, ...)` requires `uiParam` to be zero. The call currently passes `SpiGetWorkArea` (48) as both the action and parameter. It may happen to be ignored, but it is outside the documented contract and may make the call fail or behave inconsistently.
   - Fix: change the call to `SystemParametersInfo(SpiGetWorkArea, 0, out var workArea, 0)`.

### Low

1. **`ToColumnAndRowDistance` can overflow before taking absolute values.**
   - File/lines: `src/Robotron2084/Core/ScreenSize.cs:98-101`
   - `to.X - from.X` and `to.Y - from.Y` are evaluated as `int`. Opposite extreme coordinates can wrap, so the resulting distance is wrong even though `Math.Abs` returns for the wrong signed value.
   - Fix: calculate each delta in `long`, take absolute value in `long`, divide and sum with overflow-safe final conversion, or otherwise check for range.

2. **Clock conversion methods do not handle negative or overflowing inputs explicitly.**
   - File/lines: `src/Robotron2084/Core/ArcadeClock.cs:66-70`, `src/Robotron2084/Core/ArcadeClock.cs:81-99`
   - `ToClockUnits` can overflow for large positive or negative frame counts. Negative waits have no documented contract and are not consistently suitable for the round-up formula.
   - Fix: validate frame counts are non-negative and/or use checked arithmetic. Document and test the boundary behavior.

## Summary

- Core geometry and clock utilities are mostly small and deterministic.
- The most relevant correctness risks are invalid random limits, an incorrect P/Invoke parameter, and integer overflow at utility boundaries.
- No source files were changed.

## Test gaps

- Add direct tests for `ArcadeRandom.PickUpTo` with limits `0`, `1`, `255`, `256`, and negative/oversized values.
- Add direct tests for `ArcadeClock.ToClockUnits`, `ToPortTicks`, and `ToPortTicksRoundedUp`, including zero, negative, and boundary/overflow values.
- Add direct tests for `ArcadeFrameTimer.CreateDue` and `Tick` with zero/negative intervals and varying intervals.
- Add a test for `ScreenSize.ToColumnAndRowDistance` using coordinates near `int.MinValue` and `int.MaxValue`.
- Existing tests cover `ScreenSize` scaling and `ArcadeRandom` distribution, but do not cover all public boundaries. `DisplayInfo` needs a test seam or platform-integration test because P/Invoke behavior cannot be reliably verified by unit tests alone.
