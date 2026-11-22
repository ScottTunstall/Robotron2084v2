# Review: `tests/RobotronSoundPlayer.Tests/Emulation`

## Scope

Reviewed the four C# files in this folder: `Pia6821Tests.cs`,
`Mc6800CpuTests.cs`, `FlatMemoryBus.cs`, and `EmulatedSoundBoardTests.cs`.
The targeted emulation suite was run with:

```powershell
dotnet test tests\RobotronSoundPlayer.Tests\RobotronSoundPlayer.Tests.csproj --no-restore --filter "FullyQualifiedName~RobotronSoundPlayer.Tests.Emulation"
```

Result: 19 tests passed, 0 failed. No wall-clock waits, threads, timers, or
random inputs are used in these tests.

## Findings

### 1. ECHO board tests infer correctness from arbitrary instruction counts

`EmulatedSoundBoardTests.cs` warms the board with 100 calls to `Run(1)` and
allows 20 calls after each sound-number send. `Run(1)` executes exactly one
instruction by current implementation, but that is not an explicit
synchronization contract in the test. The fixed counts can change meaning if
either the ROM or `Run` semantics change.

The tests also check only convergence to an output value. They do not verify
that the WAI/interrupt sequence ran at the intended point, or that an extra
interrupt or delayed RTI cannot be masked by the large count.

Prefer synchronization by state or a fixed small CPU trace: expose or assert
that the processor has reached WAI, and after `SendSoundNumber` assert the
interrupt is accepted and the handler state/output is reached. Keep only a
bounded, documented number of steps as a guard against an infinite loop.

### 2. Board tests lack sound-number boundaries and invalid-input cases

The board API promises sound numbers 0 through 63, but
`EmulatedSoundBoardTests.cs` covers only `0x25`, `0x11`, and silence. It does
not cover low/high boundaries (`0`, `63`) or verify the six-line
complement/`0xC0` wiring expected for a send.

It also leaves invalid values undefined in tests. `SendSoundNumber` masks
values by complementing and applying `SoundLines`, so behavior for negative,
64, and 255 values may not match the caller's contract. Add tests for the
first/last valid values and decide/assert/document whether out-of-range values
are rejected rather than silently masked.

The wrong-ROM-size test checks only a larger, valid-RomLength-mismatched array.
There is no test for a small ROM or `null`. The constructor currently has no
explicit null guard and would throw `NullReferenceException`; if null is not
supported, test and produce an appropriate `ArgumentNullException`.

### 3. PIA interrupt and control-register edges are untested

The PIA tests cover rising/falling selection, a mixed port-A direction, and
port-B read clearing. They do not cover:

- falling-edge mode producing and not producing the interrupt;
- repeated rising or falling edges while the interrupt flag remains set;
- reading or writing the control register with the interrupt flag intact;
- direction-register reads, plus control/data register selection changes;
- port-B output pins mixed with input pins;
- reading port A clearing its interrupt flag;
- repeated reads clearing the first-interrupt flag consistently.

The rising/falling tests depend on default and one-time state. They would be
more robust as separate cases that establish initial edge state, switch
direction, and assert that neither false transitions nor level state alone
fires unexpectedly.

### 4. WAI test can miss duplicate register saves

`WaitingForAnInterrupt_SavesTheRegistersOnlyOnce` in `Mc6800CpuTests.cs` leaves
the stack pointer and other saved state largely unasserted before the
interrupt. It loops 5 times in WAI, but only asserts that PC remains unchanged.
A faulty implementation could save on every WAI step and still reach the
subsequent assertions depending on how the interrupt handler completes.

Assert `StackPointer == 0x01F8` after entering WAI and after each wait step,
and assert the complete register set (`A`, `B`, `X`, `CC`, `PC`, `SP`) after
RTI. This directly verifies the test name and would catch duplicate saves.

### 5. CPU test coverage does not exercise all addressing and flag edge cases

The CPU suite has useful exact-state and cycle-count tests, but it lacks
several important branches and boundaries:

- direct, indexed, and extended addressing in load/store/arithmetic families;
- all branch conditions, including signed comparisons and unsigned
  carry/zero combinations;
- 16-bit address/index wraparound and stack wraparound;
- DAA with cases such as `0x99 + 0x01`, `0x9F + 0x01`, carry input, and
  half-carry input;
- decrement/increment overflow boundaries, rotate/shift carry and overflow
  boundaries, and decimal/BCD flag combinations;
- SWI/RTI, software-interrupt vectors, reset after a previously used CPU,
  and mask-state interactions;
- illegal/unimplemented opcode behavior and completeness of the opcode table.

The exact-cycle countdown test is strong. The arithmetic tests are also useful,
but the suite currently asserts grouped flag masks in some cases and only
selected individual flags in others. One test may pass for the wrong reason if
an unasserted flag changes. Assert the complete relevant condition-code mask
where the behavior is specified.

### 6. Interrupt test does not assert every saved/restored register

`AnInterrupt_SavesEveryRegister_AndReturnFromInterruptPutsThemBack` explicitly
asserts A, B, PC, SP, and interrupt mask, but not X or condition codes. The
test comment says every register. It would still pass if X or a non-interrupt
flag were not restored.

Set X and a deliberate condition-code pattern before the interrupt, then assert
X and the complete condition-code value after RTI. The existing state setup
could easily include these values without changing the test machine.

### 7. Flat memory helper has little defensive behavior

`FlatMemoryBus.Load` copies bytes to `_memory` at the supplied address without
checking address or length bounds. A test that loads past 64K can wrap or fail
at the array-copy call rather than produce a clear test setup error.
`WriteByte` records every write but does not expose values, so tests that need
to verify address plus value pairs can only re-read memory or add helpers.

The helper is otherwise isolated and deterministic. Add bounds checks and
optionally record `(address, value)` writes if future tests need to distinguish
an address/value sequence. Keep the existing address-only history if another
test intentionally relies on it.

## Flakiness and isolation assessment

- **Flakiness:** Low. The suite uses fixed instructions and fixed step counts;
  it has no real-time or external-service waits.
- **Isolation:** Good. PIA and CPU instances are local, and the flat memory bus
  is recreated per test. The ECHO board creates a fresh ROM and board per test.
- **Hidden timing assumptions:** Medium. The 100/20-step ECHO counts are
  arbitrary and should be replaced or documented with state synchronization.
- **External dependencies:** None in this folder. ROM-fidelity tests outside
  this folder skip cleanly when the proprietary ROM is absent.

## Positive observations

- CPU tests use a deterministic in-memory bus and exact expected cycle counts.
- Memory writes and TST behavior are directly observable through
  `FlatMemoryBus.Writes`.
- The interrupt, WAI, JSR/RTS, branch-loop, DAA, overflow, and borrow tests
  cover meaningful baseline behavior rather than only object construction.
- PIA tests use explicit edge selection and register setup.
- The ECHO ROM is fully embedded in the test, making integration tests
  independent of external ROM files.
- Test names describe behavior and setup comments make the short machine
  programs understandable.
