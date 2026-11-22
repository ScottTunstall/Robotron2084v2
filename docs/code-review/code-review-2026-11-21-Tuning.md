# Tuning Review

## Scope

Reviewed all C# files under `src/Robotron2084/Tuning`. These types are primarily immutable-looking
constants plus public mutable `static readonly` arrays/tables used by gameplay and palette code.

## Findings

### 1. Wave lookup accepts invalid wave numbers

`WavePaletteTables.GetElectrodePaletteIndex`, `GetElectrodeVariant`,
`GetLaserWallPaletteIndex`, and `GetWallPaletteIndex` use `(wave - 1) % Length`.

- A wave of `0` produces index `-1` and throws `IndexOutOfRangeException`.
- Negative waves behave inconsistently and also select negative indexes.
- `int.MinValue` would overflow at `wave - 1` in a checked context.
- The methods' contract names wave as counting from 1, but do not enforce that precondition.

Recommended invariant: reject `wave <= 0`; do not silently wrap it. Also consider calculating modulo
as `(wave - 1) % Length` only after validation.

### 2. Static tuning tables are mutable

Examples include `DefaultWallPalette`, `ElectrodePaletteIndexByWaveMod10`,
`ElectrodeVariantByWaveMod10`, `GrowDeltas`, `GrowSizes`, `SparkleColours`,
`SparkleSetByPick`, and `Steps`. The reference elements are `static readonly` but their array
elements and the `Color[]` entries are mutable.

This allows a caller or test to corrupt global configuration after initialization. It is especially
unsafe for index tables, because one bad element can become an out-of-range palette or collision
index in a consumer. Prefer immutable representations (`IReadOnlyList<T>` backed by private arrays,
or frozen/read-only records) if runtime mutation is not required.

### 3. Cross-table and array-length invariants are not validated

Several tables depend on matching lengths or index domains:

- `ElectrodeVariantByWaveMod10` has 10 entries and variants 0-8.
- `GrowDeltas` and `GrowSizes` are both used by `_growStep` with `GrowSteps = 4`.
- `TransporterTuning.SparkleColours` must contain seven 20-byte sets, and
  `SparkleSetByPick` must select valid set indices.
- `TransporterTuning.Steps` and `SparkleSetByPick` encode fixed ROM state machines.
- `WallPaletteIndexByWaveMod10`, `ElectrodePaletteIndexByWaveMod10`,
  `LaserWallPaletteIndexByWaveMod10`, and `ElectrodeVariantByWaveMod10` are all per-ten-waves tables.

There is no startup validation or self-check. An element-count, extra element, or wrong variant index
would become a runtime index failure or inaccurate behavior. Keep documented lengths and test at least
the key aliases and per-table cardinalities.

### 4. Numeric units and ranges are not self-validating

Constants combine pixels, columns/rows, ticks, fiftieths, milliseconds, bytes, ROM subpixels, and
percentages. Callers receive no type-level distinction and there are few range checks. In particular,
timing values, negative offsets, zero distances, and exclusive/random ranges can be accidentally
misconfigured and only fail later.

Not every tuning member needs runtime validation, but paired units and invariants such as positive
periods, nonnegative counts, and dimensions should be checked at module/test boundaries.

## Positive observations

- The tuning files consistently encode the arcade or documented port rule beside the value.
- The palette low-nibble representation is handled explicitly with `& 0x0F` in `WavePaletteTables`.
- The common 10-wave tables and 4-step/4-size tank tables have clear cardinalities.
- Collision tuples and `GrowDeltas`/`GrowSizes` pair width/height or column/row data together.
