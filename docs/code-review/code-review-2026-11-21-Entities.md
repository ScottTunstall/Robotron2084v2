# Code Review: Direct C# Entities

## Scope

- Reviewed only C# files directly under `src/Robotron2084/Entities` (30 `.cs` files).
- Did not review `Entities/Collections`, `Entities/Models`, `Entities/Extensions`, or any other subfolder.
- Inspected the tagged/featured `CruiseMissile.cs`, its entity/list APIs, tuning values, brain/spawner integration, and entity tests.
- Build context:
  - `src/Robotron2084/Robotron2084.csproj`: `net10.0`, MonoGame DesktopGL.
  - Repository build treats warnings as errors through `Directory.Build.props`.
  - The normal test/build attempt is currently blocked by seven unrelated nullable warnings in `src/Robotron2084/Audio/Synthesis/WaveTableData.cs` lines 66-67.
  - With `TreatWarningsAsErrors=false`, the focused entity and missile suite ran 269 tests successfully (0 failures).

## Findings

### 1. Medium — `CruiseMissile.GetBounds()` can leave the playfield at the left or top wall

**Evidence**

- `CruiseMissile.cs:157-159` offsets the collision rectangle 1 column left and 1 arcade row up.
- `CruiseMissile.cs:264` clamps the missile's own X position to `bounds.X`, and `CruiseMissile.cs:276` clamps Y to `bounds.Y`.
- At a clamped left/top edge, `GetBounds()` therefore becomes `bounds.X - 4` horizontally or `bounds.Y - 2` vertically:
  - `ScreenSize.ToPortPixelsFromColumns(-1)` is `-4`.
  - `ScreenSize.ToPortPixelsFromArcadePixels(-1)` is `-2`.

The comments describe the `FAT PHONY GUY` box as a collision box and the existing wall-reflection test asserts that `GetBounds()` remains within the inner playfield bounds. The movement implementation does not preserve that invariant. A seed/direction that drives the missile exactly against the left or top wall can make the collision box overlap or extend outside the wall, changing collisions and potentially allowing collision checks against geometry beyond the field.

**Fix guidance**

Clamp the missile's position so the offset collision box remains inside the wall, or explicitly separate the ROM coordinate bounce from the collision-box bounds. For example, account for the negative offsets when selecting the X/Y minimum and maximum positions, and preserve the existing wall-bounce behavior:

```csharp
var left = bounds.X - offsetX;
var top = bounds.Y - offsetY;
```

Then keep the own-position wall bounce semantics for the missile mark as intended.

### 2. Medium — Gorf can throw `ArgumentOutOfRangeException` when `maxDropsX2` is zero

**Evidence**

- `Entities/Models/GorfDropParams.cs:21-28` allows `maxDropsX2` to be zero; only negative values are rejected.
- `Gorf.cs:321-323` calls `_random.Next(_maxDropsX2)` at a chance drop.
- `System.Random.Next(int maxValue)` throws `ArgumentOutOfRangeException` when `maxValue <= 0`.
- This occurs on a live drop stop only for a Gorf constructed with `GorfDropParams(0)` and `gruntDropCount == 0`, after which `_dropSteps` reaches `GetDropCount()`.

The public constructor therefore accepts a value that is guaranteed to crash later, rather than representing "drop zero" safely.

**Fix guidance**

Either reject zero in `GorfDropParams` (and make the contract consistently positive), or handle it in `Gorf.GetDropCount()` by returning zero for `_maxDropsX2 <= 0`. Also ensure the caller can reach all wave-clear transitions without a pending drop left behind.

## Summary

Found two correctness/game-behavior issues. The more important is Gorf's public-zero parameter crash at runtime; the second is CruiseMissile's collision box not staying within the wall at left/top edges even though the missile's own coordinate is clamped. No crash or concurrency issue was found in the remaining direct entity files or in the 269 focused tests. Validation remains constrained by the repository's unrelated nullable build warnings in `WaveTableData.cs`.
