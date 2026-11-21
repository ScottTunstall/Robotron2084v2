# Review: States/Models

## Scope

Reviewed only C# files directly under `src/Robotron2084/States/Models`:
`GameStateParams`, `MainMenuLine`, `SoundTestEntry`, and `TitleTextPane`.
Nested directories were not reviewed.

## Findings

### Medium — `GameStateParams` is a strong bundle but omits a player-two input source

- **Evidence:** `GameStateParams.cs` carries only one `IPlayerInputSource`,
  documented as player 1's input.
- `CreateFrom(GameSession, ...)` copies player 1's input separately and cannot
  select a player-two source for subsequent score-entry states.
- This matches the player-two initials routing concern in the root state review.

**Recommendation:** if initials and ceremony controls are player-specific,
extend the bundle or pass an explicit source into `InitialsEntryState`. If all
names are entered by player one, document that invariant on
`GameStateParams.PlayerInputSource`.

### Low — `MainMenuLine` numeric values are implicit

- `StartGame`, `GameStyle`, `Settings`, and `DeveloperTools` rely on implicit
  ordinal values 0-3. They are not persisted or serialized here, and are always
  consumed through the enum in switch/array logic.
- Invalid numeric casts can bypass a menu row while still reaching
  `default`-like behavior in `GetLabel` or `Choose`.

**Recommendation:** use explicit enum values or validate at public boundaries
if menu indices are ever loaded externally.

### Low — `SoundTestEntry` is internal but permits inconsistent state

- `SoundTestEntry.cs` allows `SoundNumber == null` for the transporter, and an
  integer for normal sounds, but imposes no range validation.
- The entry is created internally rather than from external data, and
  `SoundTestState` gives zero handling for out-of-range direct sound numbers.
- `Heading`/`Name`/`Description` can also be empty because there is no constructor
  guard.

**Recommendation:** validate direct sound numbers against the board's range and
define an explicit transporter sentinel if these records become externally
constructible.

### Low — `TitleTextPane` numeric values are implicit

- `TitleTextPane` has only `Arcade` and `Port`, and switch code uses only
  equality or nullable state.
- An invalid cast is not normally reachable from the in-tree constructors, but
  public APIs accept nullable `TitleTextPane?` and `GetShownPane` could receive
  an invalid numeric value.

**Recommendation:** reject invalid enum values in `TitleScreenState` if external
developer navigation is broadened.

## Summary

No immediate crash was identified. The most important model limitation is that
the shared state bundle carries only player one's input. The enums and
`SoundTestEntry` are mostly internal and currently receive valid values, but
they lack defensive constructor or enum validation at public boundaries.
