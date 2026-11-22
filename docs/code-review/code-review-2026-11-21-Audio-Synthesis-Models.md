# Audio Synthesis Models Review

Scope: `src/Robotron2084/Audio/Synthesis/Models/*.cs`. Content was not reviewed.

## Medium

### `OutputChange` does not enforce output-change invariants
- **Where:** `OutputChange.cs:5-15`
- **Issue:** The readonly record accepts negative `CyclesBefore`, negative `WriteCycles`, or an impossible write timing. There is no validation that `Level` has already been represented by `byte` (it is enforced by the type) or that write timing is within the cycle interval.
- **Risk:** A malformed synthesis routine can send the renderer negative cycle intervals or have `_nextWriteCycles` describe a write that does not match the interval.
- **Suggested correction:** Add constructor validation for non-negative `CyclesBefore` and `WriteCycles`, and require `WriteCycles <= CyclesBefore` unless the model intentionally supports another zero/write convention.

### `SquareWaveSoundRow` and `WaveTableSoundRow` appear to lack invariants
- **Where:** `SquareWaveSoundRow.cs`; `WaveTableSoundRow.cs`
- **Issue:** The rows are value containers with no explicit range checks for counters, wait values, and sweep lengths.
- **Risk:** Static table mistakes or future table edits can create zero/negative repeat paths or out-of-bounds pitch/wave calculations.
- **Suggested correction:** Add documented invariants and constructor validation for all fields consumed as durations/counters.

### Filtered noise setup validation is local and incomplete
- **Where:** `FilteredNoiseParams.cs:19-33`
- **Issue:** `SetUpCycles` is validated as non-negative, but `LevelsPerShrink`, `FirstLimit`, `SmallestSlope`, and the Shrinks/Distorted combinations are not checked for valid synthesis ranges.
- **Risk:** Extreme or inconsistent settings can drive unbounded loops or nonsensical filtered-noise levels.
- **Suggested correction:** Validate the complete parameter set against the synthesizer's 8-bit/16-bit domains and documented end-of-limit behavior.
