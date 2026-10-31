# Terminology audit: one idea, two words (2026-10-31)

This is a work list for a fixer. It is in the shape of [naming-issues-2026-10-11.md](naming-issues-2026-10-11.md), but it
is about **words**, not about single names: places where the code uses two or more different words for one idea, or
one word for several ideas. [coding-standards.md](coding-standards.md) NAM-7 and NAM-9 are the rules it applies
("near-synonyms must mean different things"; "one word per concept, everywhere"). The glossary
([glossary.md](glossary.md)) says which word wins where it has already decided.

It is not the same as [terminology-ledger.md](terminology-ledger.md), which compares the *original source's* words with
ours. This list only compares our own words with each other.

> **Update, same day.** The author decided D01 (wave), D02 (lives) and D03 (Max and Min), and they were done in one change; see
> [refactoring-ledger.md](refactoring-ledger.md). D12 turned out to need no rename: every `SoundTables` field was already named
> after its ROM label's comment, so the table does not mix two vocabularies. The items below are left as they were written,
> because they are the record of what was found. Their status:
> D01 **done** (the namespace `Robotron2084.Level` was left), D02 **done** (`TurnsPerPlayer` and the arcade's on-screen words were left),
> D03 **done** (`Floor`, and the positions called `…Limit`, were left), D12 **no change needed** (the summary now says the names are
> the ROM's on purpose). Every other item is still open.

## How this list was made

1. Every identifier in `src/` (types, members, parameters, locals; 1,415 distinct words after splitting `CamelCase`)
   was counted, with the number of identifiers and files each word appears in.
2. Words that mean the same thing were grouped (wave and level, delay and interval, cap and limit and max, and so on),
   and words with many meanings were found by listing every identifier that carries them (`slot`, `step`).
3. Each group was read in place. A group became an item only if it was checked against the code, not only the counts.
4. The glossary's "Don't say" and "Retired words" columns were searched for in `src/`, `tests/` and `tools/`.

Counts are whole-word lines in `.cs` files, comments included, `obj/` and `bin/` left out. They show the size of the
rename, not the size of the edit. Find the code by name and quoted text, not by a line number.

---

## 0. Rules for whoever fixes these

Read all of this before touching anything.

1. **Each item has a status.** `READY` means a rule in the coding standards or the glossary already says which word
   wins, so you may fix it. `DECIDE` means the **author** has to pick the word. Do **not** start a `DECIDE` item, and do
   not pick a word yourself: CMT-6 says no vocabulary is invented without the author's consent. The "Proposed" column in a
   `DECIDE` item is a suggestion for the author to accept or change, nothing more. If you are unsure which a case is, stop and ask.
2. **A rename and nothing else.** No behaviour change, no signature change, no reordering, no reformatting of lines
   you did not need to touch. If a name can only be fixed by changing what the code does, stop and report.
3. **One item per commit** (or one part of an item, where the item says "Part A / Part B"). Put the item ID in the
   message, e.g. `R02: retire "roll" from the Hulk spawner`.
4. **NAM-8: the old word is gone everywhere**: identifiers, parameters, locals, `<param>` and `<summary>` text,
   `<see cref>`, test names, test helper names, `tools/`, and any doc that is not dated. Search `src/`, `tests/` and
   `tools/` after every rename.
5. **Do not touch:**
   - a **framework name**: MonoGame's `Color`, `Rectangle.Center`, `Rectangle`, `Game.Initialize` (NAM-12);
   - a **ROM label or ROM message quoted from the source**, and the on-screen text taken from the arcade
     (`"EXTRA MAN EVERY"`, `"TURNS PER PLAYER"`, `"RESTORE FACTORY SETTINGS"`);
   - the **keys written to the player's saved files** (`extramanevery=`, `turnsperplayer=` in `GameSettingsStore`, and
     the keys in the high score and controls files). Renaming a key silently throws away a saved setting. If an item
     needs one changed, it is a `DECIDE` item with a migration, and it is not in this list;
   - **content file names** (`Content/Sprites/...`, `Content/LevelTable.csv`) unless the item says so;
   - the **dated records**: `arcade-fidelity-notes.md`, every ledger, `status.md`, `plan.md`, the handoffs, the dated
     review files and this file. They keep the words they were written with.
6. **The working tree is already dirty.** The author has uncommitted edits in many files (see `git status`). Before you
   edit a file, run `git diff --stat -- <file>`. If the file already shows changes you did not make, **stop and tell the
   author** instead of committing their half-finished work under your item. Stage by name (`git add <file> ...`), never
   `git add <folder>` or `git add .`, and never stage any `docs/code-review-*.md`.
7. **After every item, all three must pass** with 0 warnings and 0 failed tests, and the test counts must not change:
   ```
   dotnet build Robotron2084.slnx -c Debug
   dotnet build Robotron2084.slnx -c Release
   dotnet test Robotron2084.slnx
   ```
   On this machine `dotnet test` may report "Zero tests ran" (exit 5). If so run the assembly directly:
   `dotnet tests/Robotron2084.Tests/bin/Debug/net10.0/Robotron2084.Tests.dll` and the same for
   `RobotronSoundPlayer.Tests`.
8. **Record it.** Every rename goes in [refactoring-ledger.md](refactoring-ledger.md) as a new section at the top
   (PROC-5: old name, new name, why). If a rename retires a word, add it to the glossary's "Retired words" table in the
   same commit (NAM-22). If it settles a word, add it to the glossary once (CMT-6, CMT-11); every other place links there.
9. **Comments follow the memory of how this project writes them:** plain English a child of ten could read, no arcade
   jargon, a term defined once (in the code that owns it) and linked with `<see cref>` everywhere else. When a rename
   moves a word that a `<summary>` explained, the summary stays and takes the new word.
10. Commit messages end with the `Co-Authored-By` line the session gives you.

### Suggested order

Part 1 first (everything in it is `READY`). Part 2 only after the author has answered the question in each item. Part 3 is
for the author to read.

---

## Summary

| ID | One idea, many words | Status | Size |
|---|---|---|---|
| R01 | `Color` / `Centered` / `Initialize` against `colour` / `centre` / `initialise` | READY | ~20 files |
| R02 | The retired word "roll" (a random number) | READY | 4 files |
| R03 | `Dev…` against `Developer…` | READY | 2 files |
| R04 | `_walkCycleStep` is an index and is not named as one | READY | 5 files |
| R05 | A rectangle called `box`, `rect` or `Rect` | READY | 6 files |
| R06 | The gap between beats called delay or limit | READY (Part A) / DECIDE (Part B) | ~12 files |
| D01 | **Wave** and **level** | DECIDE | 36 files + 53 test files |
| D02 | **Lives**, **men**, **ships**, **turns**, **extra man**, **extra life**, **mini man** | DECIDE | 18 files |
| D03 | **Max**, **Maximum**, **Cap**, **Limit** (and **Min**, **Minimum**, **Floor**) | DECIDE | ~25 files |
| D04 | **Remaining** and **Left** | DECIDE | ~25 files |
| D05 | **Explosion**, **explode**, **strip effect**, **appear**, **materialise** | DECIDE | ~25 files |
| D06 | **Rescue score**, **rescue bonus**, **points**, **award**, **note**, **show** | DECIDE | 8 files |
| D07 | **Slot** means five different things | DECIDE | the three non-palette meanings: 21 files + 16 test files |
| D08 | **Step** means many different things | DECIDE | 68 files + 30 test files carry the word |
| D09 | **Control**, **input**, **binding**, **action**, **pad**, **game pad** | DECIDE | ~25 files |
| D10 | **Mode**, **state**, **screen**, **page** (and the glossary's INTRO2, FAMPAG, HISTO, TABLE) | DECIDE | ~20 files |
| D11 | **Sprite**, **animation frame**, **texture**, **image**, **picture** | DECIDE | ~10 files |
| D12 | `SoundTables` mixes the ROM's words and ours (`CircleKill` beside `BrainKill`) | DECIDE | 3 files |
| D13 | Words for "how many times round": pass, iteration, rotation, redraw, shake, lap, repetition | DECIDE | ~10 files |
| D14 | `IsAlive` / `IsLive` / `GetActive…` / `GetSparkCount` | DECIDE | ~8 files |

---

# Part 1. READY: the standards already decide

## R01. British spelling: `Color`, `Centered`, `Initialize` in our own names

*Rule:* NAM-12 (British English everywhere; a framework member keeps its spelling, ours does not). The code has both
`Colour` (26 files, 94 lines: `GetSlotColour`, `ColourTable`, `GetColour`, `_packedColourPair`, `flashColour`) and our own
`Color` names (below). One idea, two spellings.

*Fix:* rename ours to the British spelling, everywhere (NAM-8).

| Now | Where | To |
|---|---|---|
| `WallColorCycle` (class, file) | `Palette/WallColorCycle.cs`, `PlayfieldWall`, `PlayField`, `AttractState`, `PlayingState`, `StorylineState` | `WallColourCycle` |
| `RobotronColor` (class, file) | `Graphics/RobotronColor.cs`, `SpriteSet`, `GamePalette` | `RobotronColour` |
| `ColorCycleEffect` | `BlitterDraw`, `RobotronGame` | `ColourCycleEffect` |
| `ColorCycle` (property or local) | `RobotronGame` | `ColourCycle` |
| `SolidColorParameter` | `BlitterDraw` | `SolidColourParameter` |
| `UpdateEffectColors` | `BlitterDraw`, `GamePalette` | `UpdateEffectColours` |
| `PageColors` | `PresentationPagePalette` | `PageColours` |
| `GetWordmarkColorSlot` | `PresentationPagePalette`, `TitleScreenState` | `GetWordmarkColourSlot` |
| `ChaseColor`, `CurrentColor`, `colorOverride` | `PresentationPagePalette`, `WallColorCycle`, `PlayfieldWall` | `ChaseColour`, `CurrentColour`, `colourOverride` |
| `StepColor`, `ColorOperand`, `StartColorOperand`, `ColorOperandStep` | `Graphics/WilliamsLogoPath.cs` | `StepColour`, `ColourOperand`, `StartColourOperand`, `ColourOperandStep` |
| `trailColor`, `flareColor` | `CruiseMissile`, `LaserWallFlares` | `trailColour`, `flareColour` |
| `markerColors`, `fixedColor` | two test files | `markerColours`, `fixedColour` |
| `GetCenteredX`, `DrawCenteredLargeText`, `DrawCenteredSmallText` | 7 files, among them `DefineInputsState`, `DeveloperToolsState`, `SettingsState`, `StorylineState`, `TitleScreenState`, `ArcadeHud`, and wherever they are defined | `GetCentredX`, `DrawCentredLargeText`, `DrawCentredSmallText` |
| `Sound.Initialize(IAudioSink)` | `Audio/Sound.cs` (and its callers and tests) | `Sound.Initialise` |

*Not in this item:* every use of the MonoGame type `Color` (`Color.White`, `Color[]`, a parameter of type `Color`) and
`Rectangle.Center`. `RobotronGame.Initialize` overrides `Game.Initialize` and keeps its name. `DrawCentredLogo` and
`centredLevel` are already British. `BlitterDraw.cs:30` has a `RemapColor`: read it first; rename it only if it is our
name and not a MonoGame one.

*Check, each should print nothing outside MonoGame types:*
`grep -rnE "\b\w*(Color|Center)\w*\b" --include=*.cs src tests tools | grep -vE "\bColor(\.|\[|\)| )|Rectangle|\.Center\b"`

## R02. The retired word "roll"

*Rule:* NAM-22 and the glossary's Retired words table: "roll, re-roll, `…RollSides` → random number, pick again,
`…RandomRange`. Dice talk. The code draws a random number; a method that does is `GetRandom…`."

*Where it is still in a name:*

| Now | File | To |
|---|---|---|
| `SearchFamilyListRollAtMost` | `Level/Spawning/HulkWaveSpawner.cs` | `SearchFamilyListRandomAtMost` |
| `TargetRollSides` | `Level/Spawning/HulkWaveSpawner.cs` | `TargetRandomRange` |
| `RollUpTo(int exclusive)` and its caller | `AttractMode/AttractObjectMachine.cs` | `GetRandomBelow(int exclusive)` (a query, so `Get…`; NAM-13, NAM-24) |

*Where it is still in a comment:* `Level/SpawnPlacement.cs` ("the roll that is checked against a percentage"),
`tests/.../ScriptedRandom.cs` ("choose every roll"), `tests/.../PlayFieldBrainProgMissileTests.cs` (the class summary and
a `// 50/25/25 "roll"` comment). Reword to "random number".

*Suggested names are suggestions.* If the glossary or `ArcadeRandom` already has a word for the same job, use that.

## R03. `Dev` against `Developer`

*Rule:* NAM-5 (no abbreviations). The screen is `DeveloperToolsState` and its key handler is
`HandleDeveloperToolsKey`, but the static class is `Input/DevKeys.cs` and the handler for it is
`RobotronGame.HandleAttractDevKeys`.

*Fix:* `DevKeys` → `DeveloperKeys` (class and file), `HandleAttractDevKeys` → `HandleAttractDeveloperKeys`, and the
`<see cref>` to it in `DevKeys`. Rename any `devX` local the same way.

## R04. `_walkCycleStep` holds a place in a list

*Rule:* NAM-19: "a place in a list ends in `Index`". `_walkCycleStep` is read as `WalkCycle[_walkCycleStep]` and
`GetFrames(Direction)[_walkCycleStep]`.

*Fix:* `_walkCycleStep` → `_walkCycleIndex` in `Brain`, `Hulk`, `Player` and `Prog`, and `WalkCycleStep` →
`WalkCycleIndex` in `AttractMode/AttractObjectMachine.cs` (check that it is also an index there before renaming). The
other `…Step` fields are in D08.

## R05. A rectangle that is not called `Bounds`

*Rule:* NAM-19: "a rectangle ends in `Bounds`".

| Now | File | To |
|---|---|---|
| `Rectangle box` (parameter of `HitsWall`, `IsClearOfElectrodes`; a local in `Player`) | `Level/PlayField.cs:324`, `Level/FieldEntities.cs:428`, `Entities/Player.cs:347` | `bounds` (or a name that says whose: `playerBounds`) |
| `Rectangle box` (second parameter of `IsInside`) | `Entities/Prog.cs:400` | `innerBounds` or `insideBounds`, whichever the method means |
| `Rectangle rect` | `Graphics/TunnelEffect.cs:248` (`DrawEdge`) | `bounds` |
| `BlitterDraw.DrawnRect(bounds, animationFrame)` (returns a `Rectangle`) | `Graphics/BlitterDraw.cs:65` and callers | `GetDrawnBounds` (NAM-13 as well: it returns a value) |
| `FillRect(canvas, x, y, width, height, color)` (takes ints, not a `Rectangle`) | `Graphics/SpriteFactory.cs:222` | `FillRectangle` (NAM-5: no abbreviation); its `color` parameter is R01's |
| `GetSharedScreenRect` (returns four ints) | `Graphics/SpriteMask.cs:102` | leave the idea; only the abbreviation: `GetSharedScreenArea` or `…Rectangle`. Author's choice |
| `workArea` | `Presentation` | leave: it names the Windows "work area" API |

*Also in the summaries:* `Entities/IExplodable.cs` says "the box the explosion starts in" for a `Rectangle`. Reword to
"bounds". `BoxContactTest` is a class that tests boxes against pixels and stays (the contrast with `PixelContactTest` is
the point).

## R06. The gap between a robot's beats: delay, limit, interval

*Rule:* NAM-16: "The gap between beats is an interval. Never call it a period, delay, rate, sleep or step." One number
in `Grunt` has three names: `moveLimitBeats` (constructor parameter, "the most beats a grunt waits between steps"),
`DefaultMoveLimitBeats` (constant) and `MoveDelayBeats` (property). `BerzerkRobot` has the same three. The same number
in the wave records is `GruntMoveDelay`.

**Part A (READY): names that hold a count of beats.**

| Now | File | To |
|---|---|---|
| `MoveDelayBeats`, `moveLimitBeats`, `DefaultMoveLimitBeats` | `Entities/Grunt.cs`, `Entities/BerzerkRobot.cs`, and every caller and test (search the names) | `MoveIntervalBeats`, `moveIntervalBeats`, `DefaultMoveIntervalBeats` |
| `_dropDelayBeats`, `dropDelayBeats`, `DefaultDropDelayBeats`, `RepeatDropDelayDivisor` | `Entities/Quark.cs` | `…DropIntervalBeats…` (keep the divisor's meaning; check its summary) |

If the *maximum* of a random wait is what the number really is (the Grunt summary says "the most beats a grunt waits"),
the name should say so (`MoveIntervalMaxBeats`); read the code before choosing, and say which you chose in the commit.

**Part B (DECIDE): the wave-table column names.** `GruntMoveDelay`, `SpheroidDropDelay`, `EnforcerFireDelay`,
`TankFireDelay`, `BrainFireDelay`, `QuarkDropDelay` (and their `…Header` constants in `DifficultyTuning`, and
`BozoMode.Row`) are names of the arcade's table columns and run through `LevelParameters`, `WaveParameters`,
`DifficultyTuning`, `BozoMode`, the spawners and many tests. The author must say whether table columns follow NAM-16 too,
and what unit they take (NAM-2: the unit is missing: beats? arcade frames?). Spheroid's `…DropDelayRotations` count
turns of the spheroid, not beats: see D13.

---

# Part 2. DECIDE: ask the author first

Each item gives the facts, the usual case for each word, and a proposal. **The proposal is not a decision.**

## D01. Wave and level

**One idea:** one round of the game, "WAVE 12". The glossary has no entry for it.

**Counts.** "Wave": 18 `src` files and 13 test files in the table below. "Level": 18 `src` files and 40 test files. The
disassembly says WAVE 137 times (the HUD text is `"<n> WAVE"`, `HudLayout.cs:61`); "level" there is mostly sound
level.

| Word | Where | Examples |
|---|---|---|
| wave | `WaveParameters`, `WaveTable`, `WaveStartSequence`, `WaveClearState`, `WaveSurvivors`, `WaveMaterialisation`, `IWaveSpawner`, `WavePaletteTables`, `PlayerSlot.Wave`, `SavedWaveParameters`, `ElectrodeSlotByWaveMod10` | the most; the HUD, the sound (`WaveEnd`) and the spawners |
| level | `LevelParameters`, `LevelNumber`, `levelNumber`, `LevelParameterGenerator`, `LevelTableRow`, `IsLevelCleared`, `SkipLevelKey`, `SkipLevelHeld`, `StartingLevelNumber`, `EnemySpeedBonusCapPerLevel`, `GetWaveToPlay` (returns a `LevelParameters`), the namespace and folder `Robotron2084.Level` | the type that holds *everything* about the current wave |

**Why it is a clash, not two ideas.** The code contradicts its own names:
- `LevelParameters`' summary says "everything that makes one **wave** what it is".
- `LevelParameterGenerator`'s summary says "works out what each **wave** contains"; its override file is `LevelTable.csv`
  and its summary calls that file "the table that replaces the arcade's **wave** tables".
- `LevelParameters.CreateFromWave(levelNumber, WaveParameters wave)` takes a level number and a wave.
- `ElectrodeWaveSpawner`: `var wave = field.Parameters.LevelNumber;`
- `PlayerSlot.SavedWaveParameters` is a `LevelParameters?`.
- `DifficultyTuning.Apply` passes `parameters.LevelNumber` into `GetEffectiveDifficulty(int wave, …)`, and
  `BozoMode.AppliesTo(int wave, …)` is called with `parameters.LevelNumber` too: the same number is "level" outside and "wave" inside.

There are two parameter records: `WaveParameters` is **one row of the arcade's wave table** (its summary says so), and
`LevelParameters` is that row plus the wave number plus the port's extras (Berzerk robots, Gorf, tanks, difficulty).

**Proposed (author to accept or change):**
- The word is **wave**: it is the arcade's word, it is on the screen, and it is already in the larger half of the code.
- The full record `LevelParameters` becomes `WaveParameters`; the table-row record that holds that name now becomes
  `WaveTableRow` (it is "one row of the wave table"). This is a two-step rename; the order matters, or the names collide.
- `LevelNumber`/`levelNumber` → `WaveNumber`/`waveNumber`; `LevelParameterGenerator` → `WaveParameterGenerator`;
  `LevelTableRow`, `_levelTable`, `levelTablePath` → `…WaveTable…` (careful: `WaveTable` already names the arcade's table, so
  the CSV override needs its own word; author to pick, e.g. `CustomWaveTable`); `IsLevelCleared` → `IsWaveCleared`;
  `SkipLevelKey`/`SkipLevelHeld` → `SkipWave…`; `StartingLevelNumber` → `StartingWaveNumber`;
  `EnemySpeedBonusCapPerLevel` → `…PerWave`; `PlayerSlot.Wave` → keep, it is already `Wave`.
- Questions the author must answer: (a) the **namespace and folder `Robotron2084.Level`** holds `PlayField`, the spawners,
  the collision rules and more, so it is not only about waves: leave it, or rename it? (b) `Content/LevelTable.csv` is a file
  a player may have already made: keep its name, or rename it and accept a missing file as "ignored"?
- **Not part of this item:** sound "level" (`OutputLevel`, `StartLevelCycles`, `AverageLevelOverOneSample`, `SilentLevel`,
  `WaveLevel`), the sound "wave" (`SquareWaveSound`, `WaveTableSound`, `SquareWaveVector`, `CreateLowWaveTableRoutine`),
  `Background2Level`, and `isTargetBelowOrLevel`/`isDestinationLeftOrLevel` ("level" meaning "equal"). They are different
  ideas that share a spelling (see Part 3).

## D02. Lives, men, ships, turns, extra man, extra life, mini man

**One idea:** how many times the player may die before the game ends.

| Word | Examples |
|---|---|
| lives | `Player.Lives`, `PlayerSlot.Lives`, `PlayerTuning.StartingLives`, `Player.AddLife()`, `ScoreBoard._extraLifeEveryPoints`, `ScoreBoard._nextExtraLifeThreshold`, `DifficultyTuning.ComfortableLives` |
| men | `HasMen()`, `GetSpareMen()`, `GetDisplayedMen()`, `AnyPlayerSlotHasMen()`, `SwitchToPlayerWithMen()`, `DrawSpareMen`, `DrawScoresAndMen`, `HudMaxMen`, `HudMenOriginColumnP1`, `HudMenPitchPixels` |
| man | `GameSettings.ExtraManEvery`, `ExtraManEveryValues`, `GetExtraManEveryPoints()`, `FactoryExtraManEveryThousands`, `SettingsModel.ExtraManLine`, `MiniManSprite`, `BuildMiniMan`, `DrawMiniMan`, `HudMiniManWidthPixels` |
| ships | `BozoMode.AppliesTo(…, shipsPerGame)` (summary: "the men each player starts with: the TURNS PER PLAYER setting") |
| turns | `GameSettings.TurnsPerPlayer`, `FactoryTurnsPerPlayer`, `SettingsModel.TurnsLine`, `PlayerTurnMessage…` (this one means a player's go) |

**Why it is a clash.** `Lives` includes the man you are playing; `SpareMen` is `Lives - 1` (`PlayerSlot.GetSpareMen`); the
HUD draws spare men. `BozoMode` passes `PlayerTuning.StartingLives` as `shipsPerGame` and `slot.GetSpareMen()` as
`spareMen`, and compares `shipsPerGame - 1 > spareMen`: three words and an off-by-one in one line. "Ships" is not a
word this game uses anywhere else. `LevelParameters` says "a man starts" in its summary.

**Facts for the decision.** The arcade's own screen text is "EXTRA MAN EVERY" and "TURNS PER PLAYER" (keep that text, and
keep the saved keys `extramanevery=` and `turnsperplayer=`). The disassembly says LIVES 36 times and MEN 12 times.

**Proposed:** **lives** for the count (`Lives`), **spare lives** for `Lives - 1` (`GetSpareLives`, `HasLives`,
`DrawSpareLives`, `HudMaxSpareLives`), **extra life** for the award (`ExtraLifeEvery…`, `ExtraLifeLine`), and
`shipsPerGame` → `startingLives`. Leave `TurnsPerPlayer` and `TurnsLine` (the operator's setting; the arcade's word).
The HUD icon `MiniManSprite` is a picture of the player; author to say if it stays "mini man" (it is the arcade's
picture) or becomes `LifeIcon…`/`SpareLifeSprite`. Add **lives** and **spare lives** to the glossary.

## D03. Max, Maximum, Cap, Limit (and Min, Minimum, Floor)

**One idea:** the biggest number allowed. Four words for it:

| Word | Examples |
|---|---|
| `Max…` | `MaxDropsX2`, `MaxTanksPerQuark`, `MaxEnforcersPerSpheroid`, `MaxGruntsPerDrop`, `SparkMaxSpeed`, `MaxScore`, `MaxVelocityXSubpixels` (the most identifiers) |
| `Maximum…` | `GameSettings.MaximumDifficulty`, `MaximumTurnsPerPlayer` |
| `…Cap` | `EnforcerCap`, `TankCap`, `QuarkSpeedCap`, `GlobalActiveSparkCap`, `EnemySpeedBonusCapPerLevel`, `_speedCap`, `DefaultSpeedCap`, `initialsCap`, `AllTimeInitialsCap` |
| `…Limit` | `SpawnTuning.ShellCountLimit` (the same job as `TankCap`, in the same file), `LapInsetLimit`, `moveLimitBeats`, `gruntLimit`, `rightLimit` |

And the smallest: `Min…` (`NewDirectionBeatsMin`, `RescueBonusMin`) against `Minimum…` (`MinimumTurnsPerPlayer`) against
`…Floor` (`GruntSpeedFloor`, `initialFloor`) against `GorfTuning.MinimumGruntCap` (a "minimum cap").

**Proposed:** `Max`/`Min` (the majority, and the shorter), used as a prefix; `Cap`, `Limit` and `Maximum`/`Minimum` retire
from our names. Keep `Floor` only if the author says a floor is a *different* thing from a minimum (in `GruntSpeedFloor` the
speed may not fall below it, which is what a minimum is). `RescueBonusMaxCount`, `…MaxExclusive` already follow this.
ROM table names in `WaveTable` (`GruntSpeedFloor`, `QuarkSpeedCap`) are the arcade's columns: author to say whether they
follow the rule. `LapInsetLimit` and `rightLimit` are positions, not counts: read them before changing.

## D04. Remaining and Left

**One idea:** how much of a countdown is still to run. NAM-19 allows *both* ("a countdown in `...Remaining` or
`...Left`"), so the standard does not pick one and the code uses both.

| Word | Examples |
|---|---|
| `…Remaining` (about 23 names) | `_fireBeatsRemaining`, `_moveBeatsRemaining`, `_newDirectionStepsRemaining`, `_invincibilityTicksRemaining`, `_hitStopTicksRemaining`, `_dropRotationsRemaining`, `RemainingClockUnits`, `PointsStepsRemaining` |
| `…Left` (about 21 names) | `_holdTicksLeft`, `_settleTicksLeft`, `_repeatTicksLeft`, `StepsLeft`, `MonoStepsLeft`, `LaserArcadeFramesLeft`, `LoopPassesLeft`, `ReprogramShakesLeft`, `chancesLeft`, `playsLeft`, `echoesLeft`, `periodLeft` (the sound code uses `…Left` throughout) |

`…Left` also means the left-hand side (`MoveLeft`, `UpLeft`, `_leftColumn`) and "left over" (`FamilySlotsLeftOver`), so it is
the weaker word. **Proposed:** `…Remaining`; then change NAM-19 to say so. Both lists are long: do it one folder at a time
(`Entities`, `AttractMode`, `Hud`, `States`, `Audio`).

## D05. Explosion, explode, strip effect, appear, materialise

**The glossary** says: *strip explosion* (not "explosion" on its own); *appear* or *materialise* for the same thing.
Two words for one idea is in the glossary itself.

| Word | Examples |
|---|---|
| strip effect | `StripEffect`, `StripEffectKind`, `HasRoomForStripEffect` |
| strip explosion | `StripExplosionTuning` (it also holds the numbers for **appear**), `KillWithStripExplosion` |
| explosion (bare, which the glossary forbids) | `IExplodable.GetExplosionAnimationFrame()`, `GetExplosionBounds()` (on 10 entities), `SpawnExplosion`, `CreateExplosion`, `MovieExplosion`, `ExplosionStartSizer`, `ExplosionFrames` |
| explode / explodable | `StripEffectKind.Explode`, `IExplodable` |
| appear | `StripEffectKind.Appear`, `CreateAppear`, `PlayerAppear`, `WordmarkAppear`, `StepAppear`, `_appearNumbers`, `AppearStartSizer` |
| materialise | `WaveMaterialisation`, `_materialisation`, `DrawTransport` (see below) |
| transport | `RobotTransporter`, `TransportImage`, `TransporterTuning`: the arcade's own word for the *brain-wave* beam-in only (glossary: keep) |

**Questions for the author.** (a) The family of two things (cut into strips and fly apart; strips rush together) is called
`StripEffect`; the glossary calls only the first one "strip explosion". Is "strip effect" the glossary word for both, with
*explosion* and *appear* as its two kinds? (b) `StripExplosionTuning` then misnames its content: `StripEffectTuning`?
(c) Appear or materialise: pick one; the classes and enum already say *appear*, only `WaveMaterialisation` says *materialise*.
(d) Bare "explosion" in `IExplodable`'s members: keep because it *is* the explosion kind, or `GetStripExplosion…`?

## D06. Rescue score, rescue bonus, points, award, note, show

**One idea:** the thousand-odd points for saving a family member, and the number that floats up on the screen.

| Word | Examples |
|---|---|
| score | `RescueScoreMarker` (entity), `FieldEntities.RescueScores` (its list), `SpriteSet.RescueScoreDisplays` (its textures), `PlayField.ShowRescueScore`, `PlayField.AwardScore` |
| bonus | `ScoreValues.RescueBonus(n)`, `RescueBonusMin`, `RescueBonusMaxCount`, `PlayField.AwardRescueBonus` |
| points | `ScoreBurst.PointsSlot`, `PointsBounds`, `PointsSteps`, `_pointsSprite` (the numbers on a death burst, which is a different thing: see below) |
| note | `GruntSpeedProgression.NoteScore()` |

Also "points" means *score* and also *the floating number* in `ScoreBurst` (the glossary's "death burst"; its name is still waiting
for the author). **Proposed:** the amount is the **rescue bonus** (the name the value already has); the thing on the screen is
the **rescue score marker**; `Display(s)` and `ShowRescueScore` follow the marker. Author to confirm, and to settle the glossary's
open "death burst" name in the same change, since `ScoreBurst` and the rescue marker do the same job.

## D07. Slot means five different things

The glossary defines **palette slot** ("one of the 16 colours"). The code also uses `Slot` for four other things.

| Meaning | Examples | Size |
|---|---|---|
| a palette slot (the glossary's) | `GetSlotColour`, every `…Slot` constant in `Tuning/`, `FontSlots`, `TextSlot`, `HeadingSlot`, `InputSlot`, `HudScoreSlotCurrent`, `HighScorePalette.Process.Slot`, `PresentationPagePalette.FirstSlot/LastSlot`, `GamePalette.SetSlot/SuspendSlot/ResumeSlot` | the great majority |
| a player's record | `PlayerSlot` (number, input, lives, wave, score), `GameSession.Players`, `playerSlot`, `deadPlayerSlot`, `AnyPlayerSlotHasMen` | 6 files + 2 test files |
| a family member's place in the list | `Human.FamilySlot`, `FamilyList.SlotCount/FirstSlot/LastSlot`, `GetMemberInSlot`, `GetNearestFamilySlot`, `GetOccupiedFamilySlots`, `Brain._targetSlot`, `FamilySlotsLeftOver` | 10 files + 4 test files |
| one of the laser store's three places | `LaserSlots` (class), `LaserSlots.HasFreeSlot()`, `PlayField.PlayerLasers` | 5 files + 10 test files |
| the key half or pad half of a control line | `ControlSettingsStore.AssignSlot`, `isPadSlot` (the summary says "sets ONE device slot of a line") | 1 file |

**The sharpest clash:** `PlayerTuning.LaserSlot = 10` is a *colour* and `LaserSlots` is *three places for lasers*, in the same
feature. `FamilyList.FirstSlot`, `PresentationPagePalette.FirstSlot` and `TunnelPalette.FirstSlot` are three different
kinds of "first slot".

**Proposed:** keep **slot** for the palette only (the glossary's word, and by far the most uses). The others take their own
word. Author to choose each: the player's record (`PlayerSlot` → `PlayerRecord`? `PlayerData`?), the family place
(`FamilyPlace`? `FamilyIndex`: an index fits NAM-19 and `Human.FamilySlot` is already an `int`), the laser store
(`LaserSlots` → `LaserStore`? `PlayerLasers`?), the control line's halves (`isPadSlot` → `isPadBinding`: `ActionBinding` already calls them `KeyBinding` and `PadBinding`).
Do the family one first; it is the smallest and an index.

## D08. Step means many different things

R04 fixes the one that is plainly an index. The rest need a word each.

| Meaning | Examples |
|---|---|
| a distance moved in one go | `GruntChaseStep.StepColumns`, `Brain.StepXPixels`, `Gorf.StepPixels`, `stepX`/`stepY`, `GetStepTowardTarget`, `ApplyStep` |
| a distance between lines or cells | `LineStep` (26, 26, 24 pixels in three states), `SpawnPlacement.GridScanStep` (4) |
| how far a growing or shrinking animation has got | `Enforcer._growStep`, `Tank._growStep`, `Electrode._shrivelStep`, `GrowSteps`, `TakeGrowStep` |
| a walking-animation counter | `BerzerkRobot._animationStep`, `Human._subStep`, `Human.StepCount`, `Gorf._stepCount`, `Gorf._stepInHop` |
| a beat's inner move (NAM-16: "its steps are the moves inside it") | `StepIntervalArcadeFrames`, `_stepTimer` |
| advancing an attract screen by one arcade frame | `AttractObjectMachine.StepFrame()`, `AttractPageMachine.StepFrame()` |
| one move of an attract character's walk | `MovieWalk.BrainStep`, `AttractObjectMachine.TableStep`, `BrainStep` |
| a change of sound pitch | `_pitchStep`, `PitchSteps`, `StepPitch` |
| a change of size | `SizerStep`, `AppearSizerStep` |
| a change of colour, or how many steps make a lap | `StepColor`, `ColorOperandStep`, `DefineInputsHighlight.StepsPerLap` |
| a count of steps | `StepCount`, `_totalSteps`, `StepsLeft` |

**Proposed (author to pick):** keep **step** for a distance moved and a beat's inner move (the glossary and NAM-16 already
say so). Name a place in a list as an index (NAM-19: `_growStepIndex`, `_animationStepIndex`) or a count of things done
(`GrowStepCount`). `StepFrame()` → `Update()` or `AdvanceArcadeFrame()` (it is the attract pages' own tick; author to say which).
Do **not** touch `InstructionCycles.IndexStep`: that is a processor cost (the number of clock cycles of an `INX`), not a step of ours.

## D09. Control, input, binding, action, pad, game pad

**One idea:** what the player presses. Words in use:

| Word | Examples |
|---|---|
| control(s) | `ControlSettings`, `ControlSettingsStore`, `PlayerControls`, `controls.ini`, `ControlCapture`, `controls` (parameters) |
| input | `InputBinding`, `InputBindingKind`, `InputAction`, `InputActions`, `InputSnapshot`, `PlayerInputState`, `IPlayerInputSource`, `DefineInputsState`, `DefineInputsModel`, `DefineInputsHighlight` |
| binding / action | `ActionBinding` (a line), `InputBinding` (a key or button), `KeyBinding`, `PadBinding`, `InputAction` (what the key does) |
| pad | `padOne`, `padTwo`, `PadOne`, `WasPadPressed`, `PadBinding`, `isPadSlot`, `DPadUp` |
| game pad | `GamePadState`, `GamePadIndex`, `GamePadSticks`, `TryParseGamePad` |

The screen is DEFINE INPUTS, it edits `ControlSettings`, and the stored class is `PlayerControls`: the same set of settings
under two nouns. `AssignSlot` and `isPadSlot` (in `ControlSettingsStore`) use "slot" for a *place on a line* (D07). The pad is `pad`
and `GamePad` in the same file. `naming-issues-2026-10-11.md` I01 and I02 are the same area.

**Proposed:** author to pick the one noun for "the player's key and button choices" (controls or inputs) and the one noun
for the pad (`Pad` or `GamePad`). The kind-of-thing split is already clean: *binding* = one key or button; *action* =
what it does; keep both.

## D10. Mode, state, screen, page, and the glossary's four names

The glossary's *Game things* table names the attract screens **INTRO2** ("not title screen, presentation page"),
**FAMPAG**, **HISTO** and **TABLE** ("not title page, story page, high score page"). The code has:

| Glossary | Code |
|---|---|
| INTRO2 | `TitleScreenState`, `TitleTextPane`, `TitleRow`, `TitleWallSlot`, `Title2084Sprite`, `PresentationPagePalette` |
| HISTO | `StorylineState`, `StoryTitleRow` |
| TABLE | `HighScoreTableState`, `HighScoreTableLayout`, `HighScorePageHold` |
| FAMPAG | (no state in `States/`; check `AttractMode`) |

Also: `AttractState`, `IAttractState` ("marks a **screen** as part of the attract cycle"), `AttractMode` (a namespace),
`IsAttractMode`, `_isAttractMode`, `onAttractScreen`, `GameMode` (an enum of **one or two players**), `BozoMode`, `ScaleMode`, `PageMachine`,
`AttractPageMachine`. "Mode" means three things (the attract cycle, how many people play, a setting); "screen", "page" and
"state" all mean one thing shown to the player.

**Question for the author:** the glossary says the arcade's names win, but the state classes use the descriptive ones. Which
should the code use? Renaming `TitleScreenState` → `Intro2State` would be an author's call (CMT-6). If the descriptive names
stay, the glossary rows should say so ("the code says `TitleScreenState`"). Separately, `GameMode` → `PlayerMode`/`PlayerCount`
would stop "mode" meaning two things.

## D11. Sprite, animation frame, texture, image, picture

The glossary: **animation frame** is one drawing; **sprite** is fine for the whole set or one drawing; do not say
picture, art, image or bare frame. NAM-19: a texture ends in `Sprite`, `AnimationFrame` or `AnimationFrames`.

- `GetCurrentAnimationFrame()` and `GetExplosionAnimationFrame()` return a `Texture2D`; `ISpriteSource` "makes sprites" that are
  `Texture2D`s; `SpriteSet` holds `Texture2D[]`. So in practice sprite = animation frame = texture.
- `AttractObjectMachine` uses **image** for an animation frame of an attract character: `SetImage`, `AdvanceImage`;
  `RobotTransporter` uses it for a group of robots: `imageNumbers`, `RobotsPerImage`, `robotsSharingImage`, `sharesImage`;
  `CreateImage` and `DrawImage` are on the same classes. `TransportImage` is a class the terminology ledger lists among the names
  it keeps "on the author's decision": do not rename it without asking.
- The word **picture** is still in comments and in one local: `TransportImage` (`bytesInPicture`), `TransporterTuning`,
  `StripExplosionTuning`, `WordmarkAppear`, `LetterSpan`, `RobotTransporterTests`, `RobotFirstMoveTests`. The glossary retires it.
- `SpriteFactory.cs:146` says "Humanoid" in a summary (a retired word: use family member or "person").
- `WarpIn`: `TransporterSound.WarpInSoundNumber` and `TransporterSoundTests.WarpIn` use the word the glossary and terminology
  ledger retire ("warp-in" → transporter). `0x12` is "the beam-in sound": `TransporterSoundNumber`? (author to name).

**Proposed:** not a rename of the types. Fix the *words*: `image` → `animation frame` in `RobotTransporter` and
`AttractObjectMachine` unless the arcade's own label is quoted; reword the "picture" and "Humanoid" comments; rename the
`WarpIn` constants. Author to say whether `Texture2D`-returning methods named `…AnimationFrame` are the glossary's
animation frame.

## D12. `SoundTables`: the ROM's words next to ours

`Audio/SoundTables.cs` names each table after the ROM's sound label (notes §127; the terminology ledger says so). That gives
a table that mixes two vocabularies, which NAM-15 and the glossary forbid in our own names:

- ROM words for characters that our code names otherwise: `CircleKill` (spheroid), `SquareKill` (quark), `PostKill` (electrode),
  `KillAHuman`, `SaveAHuman`, `HumanProgFinalConversion` (family member, prog).
- Our words in the same table: `BrainKill`, `EnforcerKill`, `ProgKill`, `TankKill`, `CruiseMissileKill`.
- Two verbs for one act: `BrainShoot`, `EnforcerShoot` against `TankFire` (the glossary: "Fire" makes a laser; the enemies' shots
  are "shot").

**Question for the author:** do the sound table names stay as the ROM's (then say it in the table's summary and in the
glossary), or take our names (`SpheroidKill`, `QuarkKill`, `ElectrodeKill`, `KillFamilyMember`, `SaveFamilyMember`, `ProgConversion`,
`Brain/Enforcer/TankShot`)? NAM-15 says a ROM label *quoted* keeps its spelling; these are not quoted, they are re-cased.

## D13. "How many times round"

Seven words for a repeat count, each used once or twice:

| Word | Examples |
|---|---|
| passes | `LoopPassesLeft`, `CheckIntervalPasses`, `FirstCheckPasses`, `PassesBeforeRobotIsDrawnWhole`, `AppearExtraPasses` |
| iterations | `PlayerDeathFlashIterations`, `ReprogramTuning.Iterations` |
| rotations | `Spheroid._dropDelayRotations`, `_dropRotationsRemaining` |
| redraws | `Brain._reprogramRedrawsRemaining`, `ReprogramTuning.RedrawsPerIteration` |
| shakes | `ReprogramShakesLeft` |
| laps | `StepsPerLap` |
| repetitions | `SoundEntry.Repetitions`, `SoundEngine._repetitionsLeft` (the ROM's `REPCNT`) |

Some of these are the arcade's words (`REPCNT`, a "pass" of the appear loop) and are different things. **Proposed:** the
author says which are one idea; NAM-19 already says a quantity ends in `Count`, so `…Count` could stand for all of them.
`Spheroid`'s `DropDelayRotations` is also an R06-Part-B name.

## D14. Alive, live, living, active, count

- `IsAlive()` (the entity's life state), `PlayField.IsLive()` (the wave start is over and the robots may act),
  `GetNearestLivingRobotPosition`, `IsOverlappingLivingElectrode`, `LivePollArcadeFrames` (per-robot constant), `HasJustGoneLive`,
  `TellRobotsTheGameIsLive`, `SkipToLive`: "alive" and "live" are one letter apart and mean different things (a robot is alive;
  the *field* is live).
- `GetActiveLasers`, `IsMonoActive`, `isSoundActive`, `GetActiveSparkCount` (on `PlayField`) against `GetSparkCount` (on
  `FieldEntities`): read both; if they count different things (all sparks against those still alive), the difference must be in
  the names (NAM-7). `GetCruiseMissileCount` and `GetLaserWallFlareCount` have no "active".
- `Living` against `Alive`: one adjective; `IsAlive()` is the standard word (STR-9).

**Proposed:** author to name the "wave start is over" state something other than *live* (`HasWaveStarted`? it already has
`WaveStartSequence`), and to say whether *living* in names becomes *alive*.

---

# Part 3. Looked at and left alone

So nobody re-reviews them. Each is one spelling, several ideas, and the code is clear about which.

- **Sound ROM vocabulary in `Audio/Synthesis/`.** `Cycles` (a processor's clock cycles), `IndexStep`, `Branch`, `Direct`,
  `Inherent`, `Wait`/`LowWait`/`HighWait`, `Gap`, `Period`, `Timeout`, `Delay`, `Loudness`/`Volume`/`Amplitude`, `Pitch`/`Frequency`,
  `Level` (output level), `Wave` (square wave), `Tone`, `Voice`, `Note`. They mirror the sound ROM's own routines, line for line,
  and are not our game's words. (Loudness, volume and amplitude *are* three words for how loud, and pitch, frequency and period
  are three for how high: if the author ever wants one vocabulary inside `Synthesis`, that is its own item.)
- **`Target`, `Destination`, `Victim`.** A target is a thing a robot follows (the player, a family member's place); a
  destination is a random spot it heads for; a victim is a family member in a brain's grasp. Distinct, and the glossary's retired
  words table already covers "aim".
- **`Kill`, `Dead`, `Dying`, `Alive`.** `Kill` is the glossary's word; `Dead`/`Dying`/`Alive` are the three `EntityLifeState`
  values. `PruneDead` removes the dead from a list and is fine. (`PostKill` is D12.)
- **`Create…`, `Spawn…`, `Build…`.** `Create…` makes an object (NAM-13), `Spawn…` puts a robot on the field, `Build…` assembles a
  sprite's pixels (`BuildGruntPattern`). Different jobs. (`BuildMiniMan` is D02.)
- **`Defaults` and `Factory`.** `GameSettings` follow the arcade's "factory settings" (`CreateFactoryDefaults`, `ResetToFactory`);
  `ControlSettings` and `PlayerControls` are the port's own and say `CreateDefaults`, `ResetToDefaults`. That is a real difference,
  not a clash. `GamePalette.DefaultSlotValues` is a different word for a different thing.
- **`Entity` / `Object` / `Thing`.** `IEntity` is a field object; `MovieObject` is an attract-movie object (its own machine);
  `PlayerTouchedDeadThingResult` uses "thing" once, for "any entity that is dead".
- **`Interval`, `Wait`, `Nap`, `Hold`, `Timeout`, `Duration`.** Beyond R06, the glossary and NAM-16 already give `interval` and
  `wait`; `Nap` is the arcade's macro `NAP` and is kept in the attract and appear code; `Hold` (a page staying on screen) and
  `Duration` (seconds in the title screen) are different from a beat's interval.
- **`Human`.** Already in the glossary and in `naming-issues` I03, waiting for the author.
- **`Settings`.** `GameSettings`, `ControlSettings`, `NoiseSettings`, `SingSettings`: naming-issues I02.
- **`BoxContactTest`.** Contrasts with `PixelContactTest`; a name for the kind of test, not a rectangle.
- **`Left` as a side.** `MoveLeft`, `UpLeft`, `_leftColumn`, `spriteLeft`, `topLeft`.

---

## What the author is asked to decide

In the order that unblocks the most:

1. D01: **wave**, and the names of the two parameter records; the namespace `Level`; `LevelTable.csv`.
2. D02: **lives** or **men**; `shipsPerGame`; the HUD icon name.
3. D07: the three words that replace **slot** for a player's record, a family place and the laser store.
4. D05, D10, D12: the glossary's own pairs (appear/materialise, INTRO2/title screen, the sound tables' ROM words).
5. D03, D04: `Max`/`Min` and `Remaining`, then change NAM-19 to match.
6. R06 Part B, D06, D08, D09, D11, D13, D14, as time allows.

## Scans to run after each item

```
# R01 British spelling: each should print nothing outside MonoGame types
grep -rnE "\b\w*(Color|Center)\w*\b" --include=*.cs src tests tools | grep -vE "\bColor(\.|\[|\)| )|Rectangle|\.Center\b"
# R02 roll
grep -rniE "\broll\w*|\w+Roll\w*" --include=*.cs src tests tools
# R03 Dev
grep -rnE "\b\w*Dev(Keys|[A-Z])\w*\b" --include=*.cs src tests tools
# R04 / D08 Step used for a place in a list
grep -rnE "\b_?[a-zA-Z]*Step\b" --include=*.cs src | grep -E "\[_?\w*Step\]"
# R05 a rectangle not called Bounds
grep -rnE "Rectangle (box|rect|area)\b" --include=*.cs src tests
# R06 delay and limit for a beat count
grep -rniE "\b\w*(Delay|Limit)Beats\b" --include=*.cs src tests
# D01 level
grep -rnE "\b\w*Level(Number|Parameters|Table|Cleared)\w*|SkipLevel\w*" --include=*.cs src tests
# D03 four words for the biggest
grep -rnE "\b\w*(Cap|Limit|Maximum|Minimum)\b" --include=*.cs src | grep -v Audio/
# D04 Left as a countdown
grep -rnE "\b_?\w+(Ticks|Steps|Cycles|Frames|Sends|Beats|Passes|Shakes)Left\b" --include=*.cs src
# Retired and "don't say" glossary words in names
grep -rniE "\b(Humanoid|WarpIn|bytesInPicture|ColourIndex)\b" --include=*.cs src tests tools
```
