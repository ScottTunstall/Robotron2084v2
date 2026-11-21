# Review: `tools`

## Scope and validation

Reviewed the C# source recursively under `tools/RobotronSoundPlayer` and `tools/SpriteExtractor`, excluding generated `obj/bin` files. No source was changed.

Validation:
- `RobotronSoundPlayer` builds successfully.
- The test project builds and its test run exits successfully; proprietary-ROM fidelity theories are skipped.
- `SpriteExtractor` does not build. It reports a missing `FromColourValue` symbol plus three source-analysis complexity violations that are configured as build errors.

## High-priority findings

### SpriteExtractor is currently uncompilable

- `SpriteExtractor/Program.cs:467` calls `FromColourValue`, but only `CreateFromColourValue` is declared at line 472.
- The build also fails on `CA1502` cyclomatic-complexity violations in `Main`, `ExtractOne`, and `CollectData`. These are source-quality issues once compilation is restored.
- Because this tool cannot execute, all sprite extraction and PNG validation are blocked.

### Command-line numeric parsing accepts non-finite durations

- `CommandLine.ReadSeconds` uses `double.TryParse` and checks only `seconds <= 0` (`CommandLine.cs:62-69`).
- `NaN`, positive infinity, and potentially values too large for the port-tick conversion can pass validation.
- `PlaybackLength` casts `Ceiling(seconds * 60)` to `int` (`PlaybackLength.cs:23-28`). `NaN`, infinity, or overflow can produce `int.MinValue` or otherwise incorrect immediate/huge playback behavior.
- Require `double.IsFinite(seconds)`, a positive value, and a representable limit (or impose a documented maximum).

### SpriteExtractor reads required files before validating CLI arguments

- `Main` reads `robotron64k.bin` before parsing `--sprite`, `--quiet`, or unknown arguments (`SpriteExtractor/Program.cs:53-67`). Missing ROM/list files or a typo in the invocation can therefore produce a file exception instead of CLI usage.
- Parse and validate CLI arguments first, then locate/read inputs. Add a concise usage/ROM-not-found error and return nonzero instead of an unhandled exception.

## CLI and error-handling findings

### Most I/O failures become unhandled exceptions

- `Program.PlayOrShowMenu` finds the ROM and reads all bytes with `File.ReadAllBytes`, while WAV output is written directly (`Program.cs`, `WavFileWriter.cs`).
- IOException, UnauthorizedAccessException, DirectoryNotFoundException, malformed ROM size, and unwritable WAV paths are not converted to useful CLI errors or consistent exit codes. The only handled playback failure is `NoAudioHardwareException`.
- Catch and report input/output errors near the boundary, and keep exit codes 1 for bad CLI and 2 for missing ROM/no audio as already intended.

### Option parsing has ambiguous and inconsistent behavior

- `CommandLine` allows a value that begins with `--` to be consumed as an option value, for example `--rom --seconds`.
- Duplicate options silently overwrite earlier values.
- Options and words are intermixed in a two-word positional model, so `list extra`, `help extra`, or option placement can produce unexpected “too many words” behavior rather than command-specific validation.
- Empty/nonexistent choice handling is partly masked by catalog lookup, but command-specific help would improve this.
- Suggested behavior: reject duplicate/conflicting options, require option values not beginning with `--` when options are expected, and distinguish positional/option parsing per command.

### WAV output may leave a truncated file on failure

- `WavFileWriter.Write` renders into memory and then calls `File.Create(path)`, which truncates before writing the 44-byte header and sample payload (`WavFileWriter.cs:33-44`).
- A write or header failure leaves a partial file, and the method exposes raw I/O exceptions.
- Writing to a sibling temporary file and atomically replacing the destination would improve correctness, but even without that use a stream that does not truncate until validation succeeds and catch/report I/O failures.

### Interactive menu assumes a console

- `InteractiveMenu.Run` calls `Console.ReadLine`; `Console.KeyAvailable` is also used by speaker playback (`InteractiveMenu.cs`, `SpeakerOutput.cs`).
- Redirected stdin is detected for key polling, but redirected stdout and EOF behavior can still produce unusual menu loops or return cleanly with no output. Add explicit noninteractive behavior for menu invocation under redirected input.

## Resource and platform findings

### ROM discovery is partially platform-dependent

- `SoundRomLocator` is cross-platform in its use of `Path`, but it always searches fixed `Roms/<filename>` beside the executable and `ref/rom` above the app/cwd (`SoundRomLocator.cs`).
- The supplied ROM is deliberately outside git, so a missing-ROM diagnostic is useful. It does not validate the ROM length before `EmulatedSoundBoard` throws `ArgumentException`; that validation is good but should be presented as a CLI error rather than an unhandled exception.

### SpriteExtractor root and output assumptions

- `FindRepoRoot` depends on the current working directory and on sibling `ref`/`src` directories. Running the compiled program from outside the checkout, or passing a project directory as a working directory, can fail even if the executable is in the repository.
- `MapNameA` has Windows-specific filename handling (`largefont:` -> `Font_L_colon`) but then generally does not make output names safe across platforms. Reserved Windows names or colon-containing input could still collide or fail on Windows.
- Full-run output is not cleaned of stale PNGs, and a partial warning run exits 0 while claiming success. Missing/invalid source rows are accumulated as warnings but do not affect the exit code.
- The tool should derive the repository from `AppContext.BaseDirectory` or an explicit `--root`, validate required directories/ROM before writing, and use a nonzero exit for fatal validation failure.

### SpriteExtractor data parsing has correctness risk

- `CollectData` assumes source rows; `DataValues` converts `FDB` values by emitting low byte then high byte (`SpriteExtractor/Program.cs:531-596`). `6809 FDB` values are normally 16-bit words and may need to be emitted in source/high-byte order, depending on the exact syntax. This needs a ROM-verified regression test.
- Pass A range checks contain `w*h` and `off+w*h` as potentially overflowing `int` expressions. Malformed list values can bypass bounds checks.
- Inline extra data is checked for exact length, which is good. ROM offsets and all slices should also be checked with `long` arithmetic or bounds helpers.
- `FindBytes` is a correct literal search, but Pass B can find incidental ROM copies; it does not prove the semantic frame boundary. The ROM-verified search should reject ambiguous matches or require an exact unique match.

### Audio output has shutdown and queue concerns

- `SpeakerOutput.Play` disposes `DynamicSoundEffectInstance` immediately after a key press or when `PendingBufferCount` reaches zero. This is reasonable, but `WaitForQueueToEmpty` only checks key state and pending count; it also calls `Thread.Sleep(5)` continuously.
- `FrameworkDispatcher.Update()` must run on the expected thread; an unexpected hosted environment could affect MonoGame buffering or key/input behavior.
- `Console.KeyAvailable` is .NET/Linux/Windows console behavior and can throw or be unavailable in some hosting environments. It is guarded by `IsInputRedirected`, but not by no-console detection.

### WAV format and memory handling are otherwise sound

- The header is 44 bytes, mono, PCM, 16-bit, with little-endian fields written by `BinaryWriter`, and sample count is derived from memory-stream length.
- Maximum untimed playback is capped at 15 seconds and 44.1 kHz, so memory use is bounded to a few MB. `pcm.WriteTo` and the header/PCM layout do not otherwise show resource leaks.
- `PlayerAudio.ToPcm16` clamps and rounds samples and uses the same 44.1 kHz/60 Hz timing as the renderer.

## Emulator findings

### Public board input is not validated

- `EmulatedSoundBoard.SendSoundNumber` masks every number to six lines and does not reject values outside 0-63 (`EmulatedSoundBoard.cs:96-105`). Other player code only supplies valid values, but public callers can send invalid input silently.
- `EmulatedSoundBoard` also retains the caller's ROM byte array. Tests or callers can mutate it after construction and change emulation behavior. Copy the ROM in the constructor to make each board instance immutable.

### Emulator coverage should include memory-map edge cases

- `EmulatedSoundBoard.ReadByte` and `WriteByte` decode 128-byte RAM and PIA addresses by masks. Missing tests include RAM mirroring, ROM writes, unwired reads, PIA register selection, and ROM address bounds.
- `Pia6821` models only the features used by the sound ROM, which is appropriate. It should document and test that reading port B clears CB1 while leaving control bits other than the interrupt flags intact.

### CPU implementation is consistent in interrupt and stack order

- `Mc6800Cpu` saves PC, X, A, B, and CC on interrupt/WAI and restores in reverse order; `RTI` tests cover key parts of this path.
- `AnswerInterrupt` avoids saving twice when WAI already saved registers, and `Reset` sets the interrupt mask and reads the reset vector.
- Remaining emulator risk is instruction/table coverage rather than the core interrupt mechanism. A differential or opcode-table test would help catch omitted opcodes and cycle mismatches.

## Recommended test additions

1. Add a unit test that changes board level midway through `Run(...)` and verify `LevelRecorder`/`TickedBoardRecorder` split it correctly.
2. Add tests for empty, zero-duration, and unequal-length level recordings.
3. Add `CommandLine` tests for `NaN`, infinity, overflow, duplicates, missing option values, and extra words.
4. Add `SpriteExtractor` tests for ROM-not-found, malformed list rows, integer overflow, FCB/FDB parsing, inline data length, and PNG chunks/CRC.
5. Add emulated-board tests for RAM mirroring, ROM unwritable behavior, PIA address mirroring, invalid sound numbers, and ROM immutability.
