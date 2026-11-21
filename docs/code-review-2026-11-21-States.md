# Code Review: States

## Scope

Reviewed C# files directly under `src/Robotron2084/States` only:
`GameStateManager`, state interfaces, attract/navigation classes, gameplay states,
score ceremony states, menus, developer tools, and test states.
Nested folders `States/Models`, `States/SpriteTest`, and
`States/SpriteTest/Models` are documented separately.

## Findings

### Medium — `GameStateManager` permits null or re-entrant invalid states

- **Evidence:** `GameStateManager.cs:10-30` sets `CurrentGameState` directly and
  `TransitionTo` has no null guard and no in-transition check.
- The public constructor also accepts a null `IGameState`. A later `Draw` or
  `Update` dereferences it. `TransitionTo(null)` similarly leaves the manager in
  an invalid state.
- `TransitionTo` calls `Close()` before replacing `CurrentGameState`. If any
  future closable state starts another transition from `Close()`, transitions
  can close or replace states re-entrantly.

**Recommendation:** reject null in the constructor and `TransitionTo`, and make
the manager own a clear one-transition-at-a-time invariant. The normal update
loop should then never expose `CurrentGameState = null`.

### Medium — `AttractNavigation.CreateState` has no valid-step boundary

- **Evidence:** `AttractNavigation.cs:52-62` recognizes steps 0, 1, demo, and
  high scores, but every other integer is converted to a story page with
  `step - FirstStoryPageStep + 1`.
- Passing `-1`, `int.MaxValue`, or any other step outside the documented
  `0 <= step < StepCount` range can construct a negative or nonexistent story
  page. `CreateAtPage` then may have no valid page contract for that value.
- `GetNeighbourStep` normalizes only with modulo; it does not validate step
  itself, while `CreateState` assumes callers pass one of the four navigation
  categories.

**Recommendation:** validate `step` and return or throw explicitly for
`step < 0 || step >= StepCount`. Valid story pages should also be restricted to
`1..StoryPageCount`.

### Medium — Leaving the high-score table replaces the caller's control/settings bundle

- **Evidence:** `HighScoreTableState.cs:381-384` constructs a title state with
  `ControlSettings.CreateDefaults()`, while the table already retains the
  original `_gameSettings` and `_playerInputSource`.
- F10/developer navigation on the title page can subsequently use that fresh
  bundle. Custom control bindings are lost after leaving the table, even though
  the table was entered from a game or attract context that had valid custom
  bindings.
- The same event can reset only controls: `_gameSettings` and `_random` are
  passed through, but `_gameStateParams.ControlSettings` is replaced.

**Recommendation:** retain and pass the original `GameStateParams` to
`TitleScreenState`, or otherwise preserve `_gameSettings` and custom controls
when rebuilding it.

### Medium — Player-two initials entry may route through player-one input

- **Evidence:** `GameOverState.CreateFromSession` builds `GameStateParams` with
  player one's `IPlayerInputSource` only.
- `ScoreEntryCeremony` and `InitialsEntryState` read that one source. A
  two-player ceremony can submit several `FinalScore` values, including
  `PlayerNumber == 2`, but there is no player-two source in the bundle.
- If player two is expected to enter the initials for player two, that state
  cannot route its input to player two. If player one is intentionally meant to
  enter all names, the behavior should be stated explicitly.

**Recommendation:** carry both player input sources in the ceremony context or
pass the matching source for each `FinalScore`; do not silently rely on player
one for every player.

### Medium — Palette ownership is inconsistent with `IClosableGameState`

- **Evidence:** `IClosableGameState` is implemented only by `TitleScreenState`,
  `WaveClearState`, and `HighScoreTableState`. Yet `InitialsEntryState`,
  `SpriteTestState`, `DeveloperToolsState`, `SettingsState`, `DefineInputsState`,
  and several test states restore or start palette values in constructors.
- The sprite/test/service states only restore static palette indices on entry;
  they do not always restore dynamic/cycling state on exit. `Close` is not
  called for these states, so palette behavior depends on the next state
  constructor repairing every shared index.
- `InitialsEntryState` changes palette indices 4 and 9 in `RestorePalette` and
  has no `Close`; it relies on the next state's own palette setup.

**Recommendation:** standardize palette mutation through
`IClosableGameState`, or document and test exact palette ownership for static
and cycling indices. This avoids stale colours after transitions that skip a
palette-aware constructor.

### Low — Temporary and edge states can transition after their completion ticks

- `GameOverState` and `EntriesMaximumState` decrement/advance only a local tick
  counter and ignore input/game time after the threshold. They perform exactly
  one `TransitionTo` on the threshold tick.
- `InitialsEntryState` submits once when `InitialsEditor.Tick` completes.
- `TitleScreenState`, `MainMenuState`, and the attract/test states update edge
  input snapshots consistently.

No double-transition was found, because threshold states use a local elapsed
count and the normal update loop is not re-entered concurrently. The lifecycle
contract still lacks explicit Enter/Exit methods, so cleanup is constructor/Close
based rather than symmetric.

### State-Transition Map

- Game start: `MainMenuState` → `PlayingState`.
- Normal game end: `PlayingState` → `GameOverState` →
  `ScoreEntryCeremony`/`InitialsEntryState`/`EntriesMaximumState` →
  `HighScoreTableState` → `TitleScreenState`.
- Wave loop: `PlayingState` or `AttractState` → `WaveClearState` →
  `PlayingState` or `AttractState`.
- Attract cycle: `TitleScreenState` → `StorylineState` → `AttractState` →
  `WaveClearState`/`HighScoreTableState`.
- Service navigation: `MainMenuState`/`DeveloperToolsState` ↔ settings, input,
  sound, speech, and sprite-test states.

The graph is mostly explicit and replacement-based; no state machine stack or
return address is maintained. Substates that need a caller (for example the
score ceremony) explicitly carry it in a field.

## Summary

The important invalid-state risks are public null acceptance in
`GameStateManager`, unvalidated attract navigation steps, and loss of custom
controls when `HighScoreTableState` returns to the title. Palette ownership is
not uniformly represented by `IClosableGameState`. No source changes were made.
