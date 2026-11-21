# Audio Synthesis Review

Scope: `src/Robotron2084/Audio/Synthesis/*.cs` (excluding `Models/` subfolder). Content was not reviewed.

## High

### `SoundBoard.Run` does not enforce its public cycle contract
- **Where:** `Synthesis/SoundBoard.cs:165-174`
- **Issue:** `Run(int maxCycles)` is documented to require at least 1, but accepts zero or negative values. Negative cycles decrease `_cyclesLeftInChange` further and can leave the board in a persistent timing distortion.
- **Risk:** Incorrect renderer/caller integration or direct test use can corrupt output timing and make subsequent `Run` calls produce the wrong level.
- **Suggested correction:** Throw `ArgumentOutOfRangeException` for `maxCycles <= 0` before changing board state.

### Sound number dispatch masks invalid values
- **Where:** `Synthesis/SoundBoard.cs:186-198`
- **Issue:** Invalid numbers are reduced modulo 64 instead of rejected. This can turn `65` into routine 1 and `-1` into routine 63.
- **Risk:** API misuse changes board behavior instead of failing, making debugging and invalid-state handling unreliable.
- **Suggested correction:** Reject `<1 || >63` explicitly before dispatch.

## Medium

### Interrupt/interpreter state is not protected from reentrant or concurrent calls
- **Where:** `Synthesis/SoundBoard.cs:257-270,458-469`; `Synthesis/BoardOutput.cs`
- **Issue:** `_waitingSoundNumber`, `_outputChanges`, `_cyclesLeftInChange`, `_boardMemory`, and `_boardOutput` are mutable and are not synchronized or confined to a documented owner thread.
- **Risk:** If `SendSoundNumber`/`Run` are called concurrently or reentrantly (for example from audio callback and game update), output state can be interleaved or an enumerator can be advanced/disposed while being used.
- **Suggested correction:** Document and enforce single-thread ownership, or add synchronization/lock the public `Run`/`SendSoundNumber` pair. The renderer and sink currently appear to rely on tick-thread confinement.

### Several synthesis routines contain unbounded loops driven by mutable state
- **Where:** `FilteredNoise.cs:197-208,246-271`; `SingSound.cs:305-351`; `ScreamSound.cs:96-104`; `SquareWaveSound.cs:112-169`; `SirenSound.cs`; `PlaneSound.cs`
- **Issue:** Finishing depends on byte/word counters and runtime state that can wrap or remain unchanged. A table-loading mistake or corrupted board RAM can keep enumerators in infinite `MoveNext()` loops rather than producing an end or invalid-state exception.
- **Risk:** `SoundBoardRenderer.Render` calls `Run` and can hang the game thread if a routine fails to terminate.
- **Suggested correction:** Preserve exact arc timing, but add bounded safety limits or explicit invalid-state checks for endless settings/voice state, especially around table/vector loads.

### Table/vector indexes are not validated before arrays are read
- **Where:** `SquareWaveSound.cs:101-104`; `WaveTableSound.cs:282-284,384-386`; `CrowdRoarSound.cs:287-305`
- **Issue:** Internal routines calculate indexes into static arrays, but do not validate them against array bounds. A bad sound number or corrupt vector/table data will throw `IndexOutOfRangeException` or copy inconsistent lengths.
- **Risk:** Runtime dispatch can crash instead of reporting a malformed synthesis setting.
- **Suggested correction:** Centralize bounds checks and range-validate every vector/pattern/wave index before access.

### Numeric synthesis arithmetic relies on unchecked 8/16-bit wrap without overflow guards
- **Where:** `BoardMemory.cs:76-97`; `FilteredNoise.cs:245-271`; `OscillatorSound.cs:294-301`; `ScreamSound.cs:151-160`
- **Issue:** The emulator intentionally uses byte wrap in many places, but public state and table-derived values can overflow intermediate `int`/`ushort` calculations in different ways. A bad setting can alter wrap behavior or leave counters at an unexpected value.
- **Risk:** Corrupt or future table values can yield wrong pitches/levels or hang loops.
- **Suggested correction:** Keep explicit byte/word wrap helpers at all arithmetic boundaries and validate table values during construction/loading.
