# AttractMode Review

## Scope

Direct C# files under `src/Robotron2084/AttractMode` (8 files): `AttractMovie.cs`, `AttractObjectMachine.cs`, `AttractPageMachine.cs`, `AttractSequenceDoneRules.cs`, `AttractStoryline.cs`, `MovieAnimationFrames.cs`, `MovieDescriptors.cs`, and `PageScriptOpcodes.cs`. Subfolder `Models` is reviewed separately.

## Findings

### Medium

#### 1. Truncated or hostile script data can escape array bounds instead of terminating cleanly
- **Evidence:** `AttractObjectMachine.StartScript` converts an address with `scriptAddress - MovieData.ScriptBase` and adds a process without validating the resulting index (`AttractObjectMachine.cs:119-126`). `Read` directly indexes `_scriptBytes[movieProcess.ScriptIndex++]` (`AttractObjectMachine.cs:195-197`). The page interpreter likewise has `NextByte()` and `NextWord()` that increment and index `scriptBytes` directly (`AttractPageMachine.cs:234-241`).
- **Impact:** A custom/partially extracted ROM script, a bad address operand, or a truncated script ending in the middle of an operand can throw `IndexOutOfRangeException` and stop the game or development page-skip flow. The ROM byte streams already contain terminators, but the public constructors accept arbitrary arrays.
- **Fix:** Validate script addresses and operand availability before reading. Add bounds-checked `TryRead/TryReadWord` helpers (or explicit checks) that mark the process/page machine finished/halted on malformed input. Preserve existing ROM behavior while preventing out-of-range access.
- **Test gap:** Add truncated page scripts and object scripts, out-of-range `SETOB/JUMP/FORK/GHOST/StartScript` targets, and an operand missing its final byte.

#### 2. Page waits are sensitive to script-index rollback, though the ROM-normal path is covered
- **Evidence:** `WaitUntilDone()` consumes the operand with `NextByte()`, rewinds by exactly two bytes, and sets a one-frame wait while the condition is false (`AttractPageMachine.cs:368-374`). It then retries the opcode on later frames.
- **Impact:** This is logically correct for the normal two-byte opcode, but any change to operand width or repeated entry could desynchronize `_scriptIndex`. It is especially fragile when an action is patched or the script array is modified.
- **Fix:** Make the wait retry explicit with dedicated state (record the wait opcode/operand or use a loop rather than mutating `_scriptIndex`), or at minimum centralize and assert the operand length.
- **Test gap:** Existing tests cover one grunt wait (`AttractStorylineTests.cs:28-49`), but not every sequence, malformed operands, or a wait that becomes true at a frame boundary.

### Low

#### 3. Public page/font data is not validated
- **Evidence:** `PrintCharacter` assumes `_movieData.FontWidths` contains an entry for every code `0x30..0x5E` and directly indexes it (`AttractPageMachine.cs:254-270`). `AttractMovieData` also accepts caller-supplied arrays.
- **Impact:** A mismatched or trimmed font table raises `IndexOutOfRangeException` for valid character codes.
- **Fix:** Validate font-table length and data bounds in `AttractMovieData` construction or print defensively when a width is absent.
- **Test gap:** Add non-ROM `FontWidths`-length cases and verify one-character, code-`0x5E`, and unknown-code behavior.

## Summary

The main attract flow and ROM replay are internally consistent; the attract and skip tests pass. The principal robustness gaps are unchecked script/font indexes when supplied custom data. The implementation would benefit from explicit bounds validation and dedicated malformed-data tests.

## Test Gaps

- Exact one-page and beyond-last-page skip behavior, plus negative page arguments (`AttractMovie.cs:57-60`, `AttractMovie.cs:88-98`).
- Truncated object/page scripts and invalid addresses/operands.
- All `WaitUntilDone` sequences and waits that become satisfied after different frame counts.
- CYCLE, MONO, LOOPER/GHOST/FORK, and unknown-opcode behavior beyond the small existing smoke coverage.
- Font-width arrays shorter than `0x30..0x5E`, plus no-marker storyline input.

