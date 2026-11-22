# Review: `tools/RobotronSoundPlayer/Emulation`

## Scope and validation

Reviewed the C# implementation in
`tools/RobotronSoundPlayer/Emulation`. No source was changed.

Validation:

- `dotnet build tools\RobotronSoundPlayer\RobotronSoundPlayer.csproj --no-restore`
  succeeded with 0 warnings and 0 errors.
- The existing emulation tests were not changed or used as the source of truth
  for this review.

## High-priority findings

### Sound-number zero and out-of-range values do not have a sound-handshake contract

- `EmulatedSoundBoard.SendSoundNumber` only masks the second PIA write and does
  not reject values outside 0-63.
- `SetSoundLines` is also used for the “get ready” write and a data write.
  Values above 63 alias to 0 through `~soundNumber & SoundLines`; negative
  values also alias to 0 or another low-six-bit pattern.
- For 0, the second write presents `0xFF` and therefore lowers CB1 instead of
  producing the rising edge used by the board's ROM setup. It is unclear from
  the implementation whether sound 0 is stop/no-request or an actual valid
  sound.
- Reject out-of-range input with `ArgumentOutOfRangeException` and add explicit
  tests for 0 and 63 as valid board values. If 0 is a stop request, model that
  behavior separately from arbitrary sound handshakes.

### The board retains and can be changed through the caller's ROM array

- `EmulatedSoundBoard` assigns `_rom = rom` without copying it.
  `ReadByte` later reads directly from this caller-owned array.
- A caller can modify the ROM after construction and change the running
  emulation, violating the apparent immutable-board/ROM model.
- Copy the ROM in the constructor and optionally reject `null` explicitly
  with `ArgumentNullException`.

### Decimal adjustment is missing a high-nibble overflow case

- `Mc6800Alu.DecimalAdjust` chooses high-nibble correction with
  `carry || highDigit > LargestDigit || (highDigit >= LargestDigit && lowDigit > LargestDigit)`.
- The second and third operands are effectively incomplete. The third expression
  should normally require `highDigit >= 9` together with `lowDigit > 9`, but
  the second expression (`highDigit > 9`) is missing as a separate positive
  check.
- For example, correcting `0xA0` after an appropriate binary operation should
  set correction `0x60`; the current code does not unless the carry or low
  digit happens to meet another condition.
- Replace this logic with the 6800 DAA rules and add boundary tests for
  `0x99`, `0x9F`, `0xA0`, carry input, and half-carry input.

## CPU correctness findings

### Reset state is only partially modeled

- `Reset` sets the interrupt mask and reads the reset vector, but it does not
  reset A, B, X, or the stack pointer. Those fields retain prior values and
  can make a second `Reset` on the same CPU differ from powered-on state.
- Real reset register contents and stack behavior should be defined
  consistently. At minimum, add reset tests that initialize all mutable fields
  and assert the documented reset state.

### Invalid opcodes are silently converted to two-cycle NOPs

- `Mc6800InstructionSet.Build` initializes every opcode to an empty action with
  two cycles.
- The 6800 may treat an undocumented or invalid opcode differently. The
  emulator does not expose an illegal-instruction result or bus fault, so a bad
  ROM/program will appear to execute as a benign NOP.
- If the emulated ROM is known to contain no invalid bytes, document that
  assumption. Otherwise, track/fail illegal opcode execution or add a targeted
  invalid-opcode policy.

### Interrupt timing still depends on the caller's cycle argument

- `EmulatedSoundBoard.Run(int maxCycles)` ignores `maxCycles` and always calls
  `Mc6800Cpu.Step`, because one 6800 instruction cannot be split.
- This contract is compatible with callers that pass the remaining cycle budget
  and add `RanCycles` to elapsed time, but it makes a negative or nonsensical
  caller budget invisible.
- Document that `Run` always consumes 1-12 cycles and reject negative
  `maxCycles` if negative values should be invalid. Do not attempt to return
  a value greater than the supplied budget; no instruction is preempted.

## PIA and memory-map findings

### PIA CB1 setup and the board-level handshake need end-to-end boundaries

- `Pia6821.SetCb1` correctly distinguishes rising and falling edges and stores
  the interrupt flag until a port-B read clears it.
- `SetCb1` and `SetPortBInput` are public enough that a caller can set levels
  independently of `SendSoundNumber`.
- The board first lowers CB1 and then raises it according to whether the
  low-six-bit complement equals `0x3F`. This is a board wiring observation,
  not a general PIA test: add tests for sound 0, 63, repeated sends, and
  repeated sends of the same number.

### Memory decoding has little direct edge coverage

- RAM is 128 bytes and is masked/repeated by `RamDecodeBits` and
  `RamAddressBits`; PIA decode ignores register bits and the top address bit;
  ROM occupies `0xF000-0xFFFF`.
- A read from an unwired address returns zero, and ROM writes are ignored.
- These properties should be regression-tested directly rather than only
  indirectly through an ECHO ROM. Include PIA alias addresses, RAM mirror
  addresses, ROM address boundaries, unwired reads, and writes to ROM/unwired
  addresses.

## Resource lifetime and platform findings

- This emulator is fully managed and has no streams, native handles, threads,
  timers, or external audio resources. The in-board `_ram` and `_rom` arrays
  live only as fields and need no explicit disposal.
- There are no OS calls or platform-specific path assumptions in the emulation
  files.
- Arrays and stack operations use `ushort` and `byte` arithmetic with intentional
  wraparound. Tests for address and stack wraparound would improve confidence
  in 16-bit edge behavior.

## Positive observations

- Interrupt entry, WAI, RTI, JSR/JSR/RTS stack ordering, and condition-code
  masking are implemented consistently with the 6800 state model.
- PIA control-register writes preserve interrupt flag bits rather than letting
  software clear them directly.
- The board constructor validates ROM size and reset initializes the processor
  from the vector.
- The implementation separates bus, CPU, PIA, ALU, instruction table, and
  opcode definitions clearly, reducing accidental coupling.

## Recommended tests

1. `SendSoundNumber` boundary tests: 0, 63, -1, 64, and large values.
2. ROM immutability after construction and an explicit null-guard test.
3. DAA tests for `0xA0`, `0x99`, `0x9F`, carry, and half-carry combinations.
4. Repeated CB1 sends and PIA falling/rising/interrupt-clear sequences.
5. RAM mirroring, PIA aliasing, ROM unwritable behavior, unwired reads, and
   reset/repeat-reset state.
