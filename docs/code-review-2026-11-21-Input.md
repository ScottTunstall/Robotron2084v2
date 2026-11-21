# Review: Input (non-recursive)

## Scope

Reviewed the C# files directly contained in `src/Robotron2084/Input`:
`PlayerControls`, `IPlayerInputSource`, `InputActions`, `GamePadSticks`,
`ControlCapture`, `ControlSettings`, `ButtonEdgeDetector`, `BoundPlayerInputSource`,
`DemoPlayerInputSource`, and `DeveloperKeys`. The `Extensions` and `Models`
subfolders are documented separately.

## Edge-trigger behavior

`ButtonEdgeDetector.Advance` is a 0-to-1 held-state detector. It reports fire,
START 1, and START 2 only when the corresponding `PlayerInputState` held flag is
true now and false on the immediately previous call. It has no state reset API;
a detector is therefore a small, persistent state machine that must be kept with
the attract state that owns it. On first use, it assumes `_previousInputState`
is the default state (all flags false).

The consumers use the result only to open the menu, so repeated held input does
not repeatedly transition. This behavior depends on polling every update frame;
omitting a poll or polling twice at different moments can lose or create a
transition. The edge detector has no direct test; adding a focused test should
cover first held state, hold-through-many-polls, release-and-repress, and
independent edges for fire/START 1/START 2.

## Polling and sources

`IPlayerInputSource.Poll` is intentionally the single runtime seam. A live
source is `BoundPlayerInputSource`, which first takes a live `InputSnapshot`
and delegates to `ControlSettings.ReadPlayer`; the snapshot overload is a
test seam for deterministic hardware-state tests. `DemoPlayerInputSource` also
implements the interface, but requires `Bind(playField)` before polling.
`Poll` safely returns zero movement and no fire for an unbound/dead field.

The demo source stores held move/hysteresis state in addition to the playfield
binding. It does not advance unless `Poll` is called, and it adopts the first
move immediately, retains a held direction for its hold interval, and only
switches after the raw direction wins enough consecutive votes. Stutter,
direction hold, wall clearance, threat range, and fire range are tuning
constants rather than source parameters. The `Random` dependency makes
stochastic stutter deterministic in tests when a seed is supplied.

The test fake and demo tests demonstrate the two useful test styles:
fixed `IPlayerInputSource` implementations for consumers and seeded live-state
objects for AI decisions. `FakeInputSource.PollCount` can verify polling
frequency, though no input-system test currently asserts edge-detector call
counts or state-lifecycle behavior.

## Mapping

`ControlSettings` owns player controls and fixed port aliases. It reads:

- Player 1 and player 2 keyboard/gamepad bindings through `PlayerControls`.
- Pause as one cross-device binding.
- Fire as an action binding plus fixed Space, pad-A, and right-trigger fire.
- Skip-wave as fixed Insert.
- START 1/START 2 from fixed keys and pad 1 Start/Back buttons.

The fire alias is evaluated as an OR. Right-trigger fire requires
`Right > 0.5`; the threshold is strict. START pad buttons come from pad 1 even
for player 2, matching cabinet coin-door controls.

`PlayerControls.GetMoveDirection` and `GetShootDirection` combine positive and
negative bindings on each axis. Both key and pad bindings participate in the
same action line, but opposite actions cancel to 0 on that axis. The result is
clamped to -1/0/1, producing 8-way movement and aiming. `IsFiring` is true for
any held shoot action, so holding an aim key fires even when no dedicated fire
binding is used.

## Dead zones and quantization

`GamePadSticks.Read` uses circular dead-zone detection:
`ThumbSticks.LengthSquared() < 0.0625`. Exact 0.25 magnitude is therefore
outside the `<` test and is quantized. XNA's Y axis is negated so output uses
screen coordinates (Y down-positive). `Math.Sign` maps each stick axis to
-1/0/1. It is not per-axis dead-zone logic: one sufficiently strong component
does not make the orthogonal vector exempt from circular-dead-zone handling.

## Capture behavior

`ControlCapture.GetNewlyPressed` is an edge capture helper for the define-input
page. Priority is deterministic: newly pressed keys first (Keys enum order),
then pad 1/pad 2 stick directions, then pad 1/pad 2 buttons. This means a key
beating a simultaneous stick/button capture is intentional. A held key is not
captured, and a stick is captured when its quantized direction changed from the
previous snapshot. Capturable pad buttons are explicit; stick clicks are
captured as buttons, while stick axes are captured as directions. The function
uses raw snapshots and has no hardware or UI dependency.

## Testability findings

Strengths:

- Hardware state is represented by `KeyboardState`, `GamePadState`, and
  `InputSnapshot`, all of which can be constructed in tests.
- `BoundPlayerInputSource.Poll(InputSnapshot)` and
  `ControlCapture.GetNewlyPressed(previous, current)` are deterministic seams.
- `IPlayerInputSource` makes gameplay and state code independent of MonoGame
  hardware.
- `Random` injection makes demo AI stutter deterministic.

Gaps:

- `ButtonEdgeDetector` has no direct tests or reset API. A reset contract is
  ambiguous if the same detector instance survives an attract-state transition
  with a long-held button.
- Test coverage does not pin trigger-threshold equality, either fire alias, or
  exact dead-zone boundary behavior.
- Demo tests cover direction hold and wall avoidance, but do not seed or assert
  the stutter branch independently.
- Capture tests cover key/button/stick basics and pad selection, but not all
  captureable DPad/shoulder combinations on pad 2 or simultaneous stick
  directions on both pads.

## Recommended tests

Add minimal tests for:

1. Edge detector: first press, held no-repeat, release/repress, and three
   independent press flags.
2. Snapshot poll: `BoundPlayerInputSource.Poll(snapshot)` uses the configured
   player index and does not mix player pads.
3. Gamepad dead zone: below, exactly at, and above 0.25 magnitude; left/right
   stick independently; XNA Y sign conversion.
4. Fire aliases and fixed starts: Space, pad-A, >0.5 trigger, Insert, D1/D2,
   NumPad1/2, and player-pad ownership.
5. Capture priority and stick-axis change from one snapshot to another.
