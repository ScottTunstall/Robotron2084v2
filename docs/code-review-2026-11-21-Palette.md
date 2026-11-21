# Palette Review

## Scope

Reviewed all C# files under `src/Robotron2084/Palette`, including the live palette, colour
processes, tunnel palette, presentation palette, and high-score palette.

## Findings

### 1. GamePalette does not enforce its 0-15 index invariant

`GetColour`, `GetPaletteIndexValue`, `IsPaletteIndexSuspended`, `SetPaletteIndexValue`,
`SuspendPaletteIndex`, and `ResumePaletteIndex` index arrays directly. Public callers are told to
use 0-15, but no method validates the index.

Invalid indexes currently leak implementation exceptions (`IndexOutOfRangeException`). Each palette
API should consistently validate `0 <= paletteIndex < 16` and throw a documented
`ArgumentOutOfRangeException`.

### 2. Public static palette tables can be mutated

`GamePalette.DefaultPaletteValues`, `GamePalette.CyclingPaletteIndexMarkers`,
`PaletteAnimator.LaserTab`, `TunnelPalette.Ramps`, `TunnelPalette.StartMasks`,
`HighScorePalette.CycleTable`, `RampTable`, `AccentTable`, and `PresentationPagePalette.PageColours`
are public/static mutable arrays.

They are configuration data but are not read-only. Mutation can alter CRTAB defaults, process
tables, or tunnel selections globally. Prefer immutable collections or private backing arrays.

### 3. WallColourCycle accepts invalid configuration

`WallColourCycle` does not validate:

- an empty `colours` list (`_colours[0]` throws);
- `stepDuration <= TimeSpan.Zero`;
- a zero or negative elapsed period;
- custom color lists outside the expected wall palette.

A zero or negative duration makes the while loop invalid (zero duration never advances elapsed
properly; negative duration is always satisfied). Empty input should fail at construction instead
of in the property initializer.

### 4. PaletteAnimator has one suspension consistency gap

The five table-driven processes stop advancing when suspended and resume by writing their table's
first entry at once. The entry-10 laser flash does not share that behavior:

- `_laserFlashClockUnits`, phase state, and `_isLaserFlashWhiteNext` continue changing while entry
  10 is suspended.
- The suspended entry can therefore miss or advance through portions of the flash timeline.

The table-driven process invariant is explicit; entry 10 should document and implement whether
suspension freezes the flash, only freezes writes, or restarts it. Existing tests cover process
suspension but not suspended laser-flash timing.

### 5. Constructor-time and update-time state should be validated

Several processes assume nonempty tables and valid palette indexes:

- table processes index `ColourTable[ColourIndex]`;
- `TunnelPalette.Apply` assumes `Ramps[RampIndex]` and 12 matching masks;
- `HighScorePalette.Advance` assumes a nonzero or recoverable table and guards only by table length;
- `WallColourCycle` assumes at least one color.

Current data passes tests, but an empty or malformed table can fail at construction or a later
update. Central invariants are not enforced.

## Palette-index invariants confirmed

- 16 entries exist in `GamePalette`.
- Cycling entries are 10-15.
- Tunnel ramp entries are 1-15, with black seams at valid entries 0/5/10 plus offsets 4,3,2,1,0.
- Presentation entries are 1-7, text is 6.
- High-score owned entries are 10,12,13.
- Existing tests cover CRTAB defaults, process wraparound, tunnel 12-ramp selection, and presentation/high-score cycles.

## Overflow and timing observations

- `_colourIndex`, `_windowStartIndex`, `_whiteFlashPaletteIndex`, and process indexes use modulo
  or subtraction and do not grow without bound.
- `_laserFlashClockUnits`, `_clockUnits`, and presentation/high-score timers are `int`; normal
  values are small, but no saturation or overflow strategy exists for a very long-running session.
- `PaletteAnimator.Update` and presentation/high-score updates subtract one period per tick; because
  each cycle is `>=` 6 clock units and units gained is 5, ordinary lag does not exceed one period.
