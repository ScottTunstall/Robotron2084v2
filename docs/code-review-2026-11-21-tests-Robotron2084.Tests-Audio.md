# Test review: Audio

## Scope

Reviewed C# test files in this folder only. Summary: 
BerzerkSpeechTests.cs, BerzerkVoiceSettingTests.cs, BerzerkVoiceTests.cs, BoardSoundsTests.cs, BoardVolumeDuringSpeechTests.cs, FakeSpeechVoice.cs, GorfVoiceTests.cs, RecordingSink.cs, SoundEngineTests.cs, SoundFacadeCollection.cs, SoundTablesTests.cs, SoundTests.cs, SoundWiringTests.cs, SpeechChannelTests.cs, StereoPannerTests.cs, StereoPlacementTests.cs, TransporterSoundTests.cs, XnbSoundEffectReaderTests.cs

## Findings

- 
Much of the audio suite uses deterministic fake voices/sinks and ROM comparison. Tests that depend on ROM bytes skip cleanly through RomFiles. Shared Speech state is reset in IDisposable suites, but collection isolation should remain explicit.
- Setup is generally local and deterministic; where real time is modeled, tests advance fixed `GameTime`/ticks rather than using `Task.Delay` or `Thread.Sleep`.
- Assertions are mostly exact or use theory/boundary checks. No obvious cross-test order dependency was found in this folder, though shared static fixtures should not be mutated.

## Flakiness and isolation

- **Flakiness:** No wall-clock waits or uncontrolled random sources were identified for these files; fixed seeds or deterministic scripts dominate.
- **Isolation:** The main risk is mutable static/shared production state or external files. Cleanup is only relevant where temporary files/directories are used.
- **Weak assertions:** A few boolean-only checks are paired with setup comments or adjacent state checks; improve diagnostics where failure is ambiguous.

## Missing edge coverage

- 
Add coverage for simultaneous channel priority, zero-length buffers, invalid/unknown sound numbers, and disposal/reset races.

## Overall assessment

The folder has useful targeted coverage, with exact state/count assertions and boundary theories. Remaining value should come from the edge cases above rather than broad smoke tests.
