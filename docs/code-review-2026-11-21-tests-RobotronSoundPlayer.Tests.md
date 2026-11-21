# Review: `tests/RobotronSoundPlayer.Tests`

## Scope and result

Reviewed the C# source recursively under this directory, excluding generated `obj/bin` output. The project builds, and `dotnet test` exited successfully. Native-board fidelity theories are skipped in this environment because `video_sound_rom_3_std_767.ic12` is absent.

## Findings

### Test helper may merge or misattribute board output

- `LevelRecorder.Record` captures `OutputLevel`, calls `Run(...)`, then assigns all of `ran` cycles to that captured level (`LevelRecorder.cs`, `TickedBoardRecorder.cs`). Likewise, `TickedBoardRecorder.Tick` attributes a complete native/emulated run interval to the level seen before the call.
- This is only reliable if the board is constant for every one of those intervals. An emulated board changes one instruction at a time, but a native `SoundBoard.Run(...)` can change output during the requested interval. The resulting runs can therefore be incorrect rather than merely less precise, especially for the fidelity comparison's boundaries.
- The helper should record transitions at the finest available cycle or instruction boundary, or run both boards one cycle/instruction at a time. The test should include a synthetic board that changes level midway through a `Run(...)` call to verify recording behavior.

### Fidelity assertions may hide the difference that matters

- `NativeBoardFidelityTests.EachSoundNumber_ChangesToTheSameLevels_AtTheSameMoments_AsTheRealRom` compares `expected.SkipLast(1)` with `actual.SkipLast(1)` (`NativeBoardFidelityTests.cs`). That explicitly removes the final, potentially instruction-overrun run.
- The sequencer test's `LongestCycles`/`Share` comparison also uses a three-percent threshold and a nine-cycle longest stretch. Those thresholds are appropriate to specified instruction lag, but they should be documented against an explicit maximum recording end and cycle precision. There is currently no assertion on `ComparedCycles`, despite that value determining `Share`.
- Add an assertion that both recordings cover the requested interval (after any allowed cut), and avoid relying on sequence equality that depends on coincident merge boundaries.

### `LevelComparison` depends on non-empty, same-length-covering runs

- `LevelComparison.Compare` immediately uses `expectedEnds[^1]` and `actualEnds[^1]`, and `LevelDisagreement.Share` divides by `ComparedCycles` (`LevelComparison.cs`, `LevelDisagreement.cs`). Silent or immediately stopped boards can produce an empty run list or `end == 0` under different timing, causing an `IndexOutOfRangeException` or divide-by-zero/infinite share.
- Add unit tests for empty recordings, zero-duration recordings, and recordings with different lengths. For normal data, return a disagreement of zero for an empty-vs-empty pair or reject malformed input explicitly.

### ECHO ROM test is timing-sensitive because it uses a fixed instruction count

- `EmulatedSoundBoardTests.RunInstructions` is named for instruction count but passes `1` as `Run`'s `maxCycles`; `EmulatedSoundBoard.Run` intentionally ignores the value and executes one instruction. This happens to model the board, but the test helper does not protect against changing the board contract.
- The interrupt tests wait by 20 calls and may pass even when interrupt setup has a one-instruction timing bug if there is enough margin. Assert the exact intermediate PIA/CPU state where possible rather than only 20-step convergence.

### CPU test coverage is focused but leaves important edge cases untested

- Existing tests cover arithmetic, memory, interrupts, WAI, RTI, JSR/RTS, and branches. Missing focused cases include:
  - direct, indexed, and extended addressing for all implemented instruction families;
  - stack wraparound and interrupts when `WAI` is active;
  - 16-bit index/address wraparound;
  - every branch condition and signed/unsigned comparisons;
  - DAA and decimal flags for values such as 0x99/0x9F;
  - illegal opcodes and instruction timing table completeness.
- Some tests assert the exact cycle count and are strong. Others, such as `WaitingForAnInterrupt_SavesTheRegistersOnlyOnce`, can be made stronger by asserting RTI restores all registers and PC rather than only that a later interrupt is accepted.

### PIA coverage misses control-register edge cases

- `Pia6821Tests` verifies rising/falling CB1, a mixed input/output port A, and reading port B. Missing coverage includes falling-edge mode, interrupt flag clearing, repeated edges without a read, direction-register selection, and the read/write behavior of control registers.
- The data-direction tests could catch a wrong input/output mask. They should also verify that writes to port B outputs and reads of port B honor direction bits independently.

## Positive observations

- Generated `obj` files were not treated as source and no source files were changed.
- The CPU tests use a deterministic flat memory bus and record writes, which makes memory and TST behavior testable.
- The native fidelity tests skip cleanly when proprietary ROM data is absent.
- The ECHO ROM test uses a small, understandable machine program instead of requiring a real ROM.
- The test helpers keep run representation compact and merge adjacent equal levels.
