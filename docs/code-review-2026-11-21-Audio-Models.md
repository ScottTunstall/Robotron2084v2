# Audio Models Review

Scope: `src/Robotron2084/Audio/Models/*.cs`. Content was not reviewed.

## Medium

### `PcmSound` is a public data state with no constructor validation
- **Where:** `PcmSound.cs:9-63`
- **Issue:** `Samples`, `SampleRate`, and `Channels` are not validated. The record can contain null samples, a non-positive sample rate, an unknown `AudioChannels` value, or a sample byte count not aligned to frames. `GetTrimmed` then divides and indexes using these values.
- **Risk:** Trimming can throw `ArgumentOutOfRangeException`/`DivideByZeroException`, silently ignore incomplete frames, or create an invalid MonoGame `SoundEffect`.
- **Suggested correction:** Add a primary-constructor guard or an explicit validated factory: non-null `Samples`, positive `SampleRate`, Mono/Stereo only, and a frame-aligned sample length.

### Trimming arithmetic and silence limits are not bounded
- **Where:** `PcmSound.cs:29-49`
- **Issue:** `marginMilliseconds` is only checked for negative values. `SampleRate * marginMilliseconds` is an overflowing `int` expression, and `silenceLevel` is only checked for negative values although the API documents `0..32767`.
- **Risk:** Very large but legal-by-validation inputs can calculate negative margins or an incorrect frame window.
- **Suggested correction:** Check silence level against `32767`, use `long` for multiplication, and ensure the final margin fits `int`.

## Low

### Other model types rely on caller validity
- **Where:** `BerzerkVoiceSetting.cs`, `GorfLine.cs`, `SoundSequence.cs`, `SoundTableRow.cs`, `SpeechChannelEntry.cs`
- **Issue:** These records/enums mainly provide data containers and currently have little or no invariant enforcement. Particularly, `SoundSequence.Entries` and pitch/volume fields can be null or out of range.
- **Risk:** Invalid tables or channel entries can produce no-op or out-of-range behavior later in `SoundEngine`/speech code.
- **Suggested correction:** Centralize validation at the façade that consumes these types, or add constructors that reject null entries and invalid timing/priority values.
