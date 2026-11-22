# Audio Review

Scope: `src/Robotron2084/Audio/*.cs` (root-level audio, speech, transport, and rendering code). Content was not reviewed.

## High

### Dynamic audio instance is never disposed
- **Where:** `SoundBoardAudioSink.cs:23-54,63-68`
- **Issue:** `SoundBoardAudioSink` owns a MonoGame `DynamicSoundEffectInstance` but implements only `IAudioSink`, not `IDisposable`. It calls `Play()` and `SubmitBuffer()` but has no shutdown/disposal path. `SoundEffectSpeechVoice` disposes its instances, while this sink has no equivalent.
- **Risk:** The native/audio resource remains live after the game or test host is torn down, and repeated game/test initialization can leak sound instances and queued buffers.
- **Suggested correction:** Make the sink `IDisposable`, stop/dispose `_dynamicSoundEffectInstance`, and ensure the game/app shell disposes it with the rest of the audio façade.

## Medium

### Public audio constructors and buffer APIs accept invalid timing parameters
- **Where:** `SoundBoardRenderer.cs:34-40,65-74`
- **Issue:** `sampleRate` is not validated. A zero or negative rate makes `_cyclesPerSample` zero/infinite/negative and can send invalid or enormous values to `Math.Ceiling`/`SoundBoard.Run`; `Render(Span<float>)` with non-empty data can therefore throw or behave incorrectly instead of rejecting setup.
- **Risk:** Startup or test configuration failures surface as obscure arithmetic/audio exceptions rather than invalid-state errors.
- **Suggested correction:** Validate `sampleRate > 0` and also validate that `soundBoard` is non-null.

### The speech and sequencing APIs accept null/negative state
- **Where:** `SpeechChannel.cs:42-46,63-74`; `Speech.cs:80-117`; `SoundEngine.cs:48-57,64-86`
- **Issue:** `Wait(int)` accepts negative ticks, `Say(string)` accepts null, and `Speech/SoundEngine` do not validate sequence or hold values before queuing them. `SpeechChannel.Tick()` also assumes the configured voice is non-null.
- **Risk:** Negative waits can make `IsBusy` report false or fail to delay the next line; null names reach dictionary/voice APIs; null sequences lead to dereferences. These are avoidable invalid states.
- **Suggested correction:** Add argument guards (`ticks >= 0`, non-null/empty name, non-null voice/sequence, hold ticks >= 0), and consistently reject malformed calls at the public façade.

### XNB reader does not fully validate format and payload lengths
- **Where:** `XnbSoundEffectReader.cs:73-91`
- **Issue:** `formatSize`, `sampleBytes`, sample rate, and payload alignment are not checked. A negative or impossibly large size can bypass `InvalidDataException`; a negative `sampleBytes` can cause `ArgumentOutOfRangeException`/allocation behavior rather than the reader's declared invalid-data exception; a payload that is not divisible by frame size can create a truncated `PcmSound`.
- **Risk:** Corrupted built content can cause process-hostile allocation attempts or inconsistent exceptions, and malformed sample-rate data can be stored for later playback.
- **Suggested correction:** Validate stream bounds and integer sizes before allocation, require `sampleBytes >= 0` and frame-aligned, and require `sampleRate > 0` (plus channels already checked).

### PCM trimming validates too little of the public record state
- **Where:** `Models/PcmSound.cs:24-63`
- **Issue:** `GetTrimmed` only rejects negative `silenceLevel` and `marginMilliseconds`. It permits a zero/negative sample rate, sample arrays that are not frame-aligned, a channel enum outside Mono/Stereo, and silence levels above 32767 despite its documented range. `SampleRate * marginMilliseconds` can overflow `int`.
- **Risk:** Invalid `PcmSound` values can produce negative spans, truncated frames, division/BitConverter errors, or incorrect margins.
- **Suggested correction:** Validate the record state on construction/use: samples not null, positive sample rate, channels Mono/Stereo, even frame alignment, and silence level in `0..32767`; use checked/long arithmetic for margin calculation.

### Sound-board send number is masked instead of range-checked
- **Where:** `Synthesis/SoundBoard.cs:186-198`
- **Issue:** `SendSoundNumber` applies `soundNumber & 0x3F` before validation. A value outside `1..63` can therefore wrap to a valid number (for example `65` becomes `1`) or be silently accepted as zero; this contradicts the method contract.
- **Risk:** Caller bugs and corrupted sequencing become different sounds instead of being diagnosed.
- **Suggested correction:** Reject values outside `1..63` before masking; keep only the normal internal line mask if required elsewhere.
