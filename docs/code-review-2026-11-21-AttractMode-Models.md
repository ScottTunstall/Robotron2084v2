# AttractMode Models Review

## Scope

C# model/data files under `src/Robotron2084/AttractMode/Models` (11 files): `AttractMovieData.cs`, `AttractSequence.cs`, `MovieAction.cs`, `MovieAnimation.cs`, `MovieDescriptor.cs`, `MovieExplosion.cs`, `MovieMessage.cs`, `MovieObject.cs`, `MovieProcess.cs`, `MovieTextCell.cs`, and `MovieWalk.cs`.

## Findings

### Medium

#### 1. Reprogram shakes can last about twice their declared countdown
- **Evidence:** `RPROG` initializes `ReprogramShakesLeft = 0x40` (64) and stores it as 64 countdown steps (`MovieProcess.cs:322-325`). `StepReprogramShake` only decrements that countdown when `IsShakingUp` is true (`MovieProcess.cs:466-490`). The alternation therefore decrements once every two calls, so the operation takes roughly 128 frames rather than the 64 advertised in the comments and test setup.
- **Impact:** A brain reprogramming/PSHAKE script holds the process and moves the character for longer than the ROM countdown description implies. It can delay later movie scripts and completion waits.
- **Fix:** Decrement the countdown on every shake step (or for every pair according to the ROM routine, with an exact 64-iteration invariant), and clear `ShakeRowOffset`/end the process when the first 64 iterations have completed. Add a named frame-count constant rather than repeating `0x40`.
- **Test gap:** The existing test runs 300 frames but does not assert the process's end frame or exact 64-step duration (`AttractMovieTests.cs:61-91`).

### Medium

#### 2. Script and table state has no bounds or consistency guard
- **Evidence:** `MovieProcess.Step`/`ReadOp` read script bytes and word operands through `ScriptIndex` (`MovieProcess.cs:97-108`). `ScriptIndex`, loop labels, `WalkIndex`, and `WalkCycleIndex` are mutable public/internal state. `TableStep` indexes `WalkIndex`, `WalkIndex + 1`, `WalkIndex + 2`, and `WalkIndex + 3` (`MovieProcess.cs:495-509`), while `BrainStep` indexes `AnimationFrameCycleTable[WalkCycleIndex]` (`MovieProcess.cs:525-536`) without bounds checks.
- **Impact:** Invalid ROM data or a corrupted/mismatched `AttractMovieData` can throw or make an unknown script loop instead of safely stopping. Several public/internal fields make these failures reachable through tests or future callers.
- **Fix:** Validate `ScriptIndex` before every read, validate loop/walk labels, and ensure walk-cycle tables are non-empty and indices are reset in range. Add an explicit halt path for malformed data.
- **Test gap:** No tests cover invalid script addresses, mismatched walk-table lengths, empty animation-cycle tables, or a loop label outside `Scripts`.

### Low

#### 3. ROM model collections are mutable and shareable
- **Evidence:** `AttractMovieData.Rom` exposes its public record arrays directly (`AttractMovieData.cs:21-29`, `Rom` at approximately the end of the file), and the `Rom` instance is static. The record constructor also accepts caller-owned arrays and strings.
- **Impact:** Caller mutation of `Rom.Histo`, `Rom.Scripts`, walk tables, font widths, or messages can alter every later movie and make test/replay behavior order-dependent.
- **Fix:** Prefer immutable representations (`ReadOnlySpan` cannot be public record state easily) or defensively copy data in the constructor and expose read-only arrays/collections. Keep the generated ROM data as the single immutable source.
- **Test gap:** Add tests proving `Rom` data remains unchanged after storyline creation and that caller mutations do not affect `Rom` or a movie that already received data.

## Summary

The models are compact and mostly immutable by value, except that the ROM data record exposes mutable arrays and `MovieProcess` state has no strict bounds. The clearest behavioral issue is the RPROG countdown’s effective 128-frame duration. The ROM replay itself has good targeted process tests, but exact state-transition and malformed-data coverage is incomplete.

## Test Gaps

- Assert RPROG ends after exactly 64 ROM countdown steps and never resumes at bytes following `PSHAKE`.
- Test table-step boundaries, empty/corrupt walk and animation-cycle tables, and out-of-range script indexes.
- Test GHOST sharing one `MovieObject`, FORK process creation, LOOPER counts, CYCLE counts, and MONO movement.
- Verify the static ROM record and embedded arrays cannot be mutated through public API or by `AttractStoryline.Create`.
