# Clean-code guideline verification (2026-10-11)

Verification of `src/`, `tools/` and `tests/` against the general clean-code checklist at
**https://gist.github.com/wojteklu/73c6914cc446146b8b533c0988cf8d29** ("Clean code
guidelines"). Rules are cited by the gist's own section and number, e.g. *Names 5* =
"Replace magic numbers with named constants".

**Authority.** This file is a checklist pass, not the project standard. Where a gist rule
and [coding-standards.md](coding-standards.md) disagree, **the repo standard wins** and the
disagreement is recorded in "Where the checklist and the repo standard disagree" — that
section is the part that needs the author's eye, because a checker running the gist alone
would "fix" things the repo has decided deliberately.

**Baseline.** `dotnet build Robotron2084.slnx -c Debug` — 0 warnings, 0 errors. No
suppressions anywhere. 619 test methods, 1933 asserts.

**Method.** Scans over all 323 `.cs` files, on top of the file-by-file reading recorded in
[code-review-2026-10-11.md](code-review-2026-10-11.md). The scans added for this pass:
line length and tabs; `bool` parameters and whether the method branches on them; parameter
counts; commented-out code; closing-brace comments; `if (!x)` guards and negatively named
booleans; `switch` statements and if/else density; `public static` members and static
classes; instance-field counts and `_`-prefix consistency; public mutable collections;
aligned declarations; `Math.Clamp/Max/Min/Sign` sites; `as`/`is`/`typeof`; `new T()` inside
constructors; threading and async; records that declare methods; three-dot chains; asserts
per test; test naming shape; sleeps, time and filesystem use in tests; unseeded randomness
in tests; parallelisation collections.

**Result: 33 rules pass, 12 partial, 20 fail, 1 not applicable.**

| Gist section | Pass | Partial | Fail | N/A |
|---|---|---|---|---|
| General rules (4) | 2 | 1 | 1 | 0 |
| Design rules (6) | 2 | 1 | 2 | 1 |
| Understandability tips (6) | 2 | 3 | 1 | 0 |
| Names rules (6) | 3 | 0 | 3 | 0 |
| Functions rules (6) | 1 | 2 | 3 | 0 |
| Comments rules (8) | 7 | 0 | 1 | 0 |
| Source code structure (10) | 8 | 1 | 1 | 0 |
| Objects and data structures (9) | 4 | 2 | 3 | 0 |
| Tests (5) | 4 | 0 | 1 | 0 |
| Code smells (6) | 0 | 2 | 4 | 0 |
| **Total (66)** | **33** | **12** | **20** | **1** |

The shape of the result is the same as the standard-based review: **the code passes the
rules a compiler or a test runner can see, and fails the rules about what a reader
sees** — line length, magic numbers, summary text, opacity, repetition.

---

## Rule-by-rule verdict

### General rules

| # | Rule | Verdict | Evidence |
|---|---|---|---|
| 1 | Follow standard conventions | **Pass** | C# conventions hold throughout: all 323 instance fields are `_camelCase` with no exceptions (the 3 apparent hits are expression-bodied properties); statics are `PascalCase`; `I`-prefixed interfaces; 4-space indent, LF endings, **0 tab-indented lines**; **0** `#pragma warning disable`, `[SuppressMessage]` or `NoWarn`. |
| 2 | Keep it simple stupid | **Partial** | Most methods are short and single-purpose, but `tools/SpriteExtractor/Program.cs:53` `Main` is 222 lines, `Level/PlayField.cs` is 887, and 309 `<summary>` blocks are assembly listings a human cannot read. |
| 3 | Boy scout rule | **Pass** | PROC-1/PROC-5 make the campsite a tracked place (`refactoring-ledger.md`), and the ledger is real: the `_sprites.Blitter.…` count fell from 101 to 86 between the two reviews, and SOLID-I's wording was cleaned up. |
| 4 | Always find root cause | **Fail** | Two live symptoms with untouched causes: the playtest invincibility flag (see the Blocker in the 2026-10-11 review) is a test default in production, and 592 undocumented members, 102 malformed comments and 6 broken `cref`s all trace to one cause the compiler is not asked to check — `GenerateDocumentationFile` is off. |

### Design rules

| # | Rule | Verdict | Evidence |
|---|---|---|---|
| 1 | Keep configurable data at high levels | **Pass** | `Tuning/` is 21 static data classes, `Level/LevelParameters.cs` is threaded through the wave as one record, and the user's knobs enter at the edge (`Persistence/GameSettingsStore.cs`) and are passed down (`PlayField.cs:112` takes `tankShellBugEnabled` as an argument, it does not read a file). |
| 2 | Prefer polymorphism to if/else or switch/case | **Partial** | 22 `switch` sites, and they are mostly honest ROM interpreters (`AttractObjectMachine.cs:258-514`, `AttractPageMachine.cs:191,220` walk the movie script's opcodes; `Player.cs:351` is the aim state). Collision dispatch is data-driven — a `typeof`-keyed table (`Level/Collisions/CollisionResponder.cs:24-37`), which is the right idea — but every entry casts the base result to a concrete type (SOLID-L). `Entities/Brain.cs` is the one file where 22 `if`/`else if` lines do work a kind-specific type would do. |
| 3 | Separate multi-threading code | **N/A** | **0** thread, `Task.Run`, `async`, `lock`, `Interlocked` or `volatile` sites in `src/`. MonoGame's loop drives everything; the only thread-adjacent code is `Audio/SoundBoardAudioSink.cs:62-63`'s `DynamicSoundEffectInstance` fill-buffer callback. |
| 4 | Prevent over-configurability | **Fail** | `Tuning/PlayerTuning.cs:83` exposes `PlayerInvincibleForTesting` as a public knob defaulting to on, with no dev-only gate and no tracking row; `Level/GameMode.cs:28-30` + `RobotronGame.cs:264-266` make an unbuilt mode selectable on F3. Both are knobs that let the program be configured into a state that does not work. |
| 5 | Use dependency injection | **Pass** | Collaborators arrive through constructors: `ISpriteSource` into `SpriteSet`, `IPlayerInputSource` into the players, `Random` into the spawners, `IAudioSink` into `Sound`. The 27 `new T()` calls inside constructors build the class's own private collaborators (`SpriteSet.cs:171-172` making its `BlitterDraw` and `ArcadeText`), which is composition, not a locator. The two leaks are defaults: `PlayField.cs:124` `contactTest ?? new BoxContactTest()` and the 6 `?? new Random()` sites (TIME-3). |
| 6 | Follow Law of Demeter | **Fail** | 86 calls through `_sprites.Blitter.…` and 40 through `.TextRenderer.` in 30 files — the gist's own example of the smell, and the repo's own STR-11 ("one dot") already bans it. 3 three-dot chains: `WaveSurvivors.cs:83`, `States/EntriesMaximumState.cs:56`, `States/GameOverState.cs:54`. |

### Understandability tips

| # | Rule | Verdict | Evidence |
|---|---|---|---|
| 1 | Be consistent | **Partial** | Field naming, member ordering (STR-5), doc shape and the entity `Update`/`Draw` idiom are uniform. Five exceptions: two unrelated nested `Process` types (`Palette/HighScorePalette.cs:175`, `Palette/PaletteAnimator.cs:120`); the same `PhaseInput` stub in two test files; two test-naming idioms (186 of 619 names are `Method_Scenario_Expectation`, 433 are sentence-style — see Tests 2); `AttractPageMachine` names opcodes in comments while `AttractObjectMachine` names them in a divider; numbered comment headers in 2 files. |
| 2 | Use explanatory variables | **Pass** | NAM-11 is followed, and the standard's own exemplar is in the code: `Entities/Enforcer.cs:272-273` names `targetX`/`targetY` before the call. The 25 sites that inline arithmetic into `new T(…)` are overwhelmingly operators (`Core/IntVector2.cs:15-25`) where a local would be noise. |
| 3 | Encapsulate boundary conditions | **Partial** | 120 `Math.Clamp/Max/Min/Abs/Sign` sites spread over 49 files. The playfield clamp is written per entity — `CruiseMissile.cs:258,270`, `Enforcer.cs:272-273`, `Hulk.cs:441-442`, `Spark.cs:223` — each with its own width/height subtraction, so the playfield's edge is a rule each robot remembers for itself. |
| 4 | Prefer dedicated value objects to primitive type | **Partial** | Good value objects exist and are used: `Core/GameTime.cs`, `Core/IntVector2.cs`, `Core/Direction8.cs`, `Core/ArcadeClock.cs`, `Core/ScreenSize.cs` conversions. What stays a bare `int` is the unit family — beats, port ticks, ROM frames, clock units, arcade pixels, port pixels, subpixels — separated only by name suffix (NAM-2). That is the repo's decision (glossary unit table), recorded as a divergence. |
| 5 | Avoid logical dependency | **Fail** | `States/PlayingState.cs:166-169` runs the death branch only if `_restartHandled` is false, and the only place that sets it false is `:318`, 150 lines away in another method — correctness depends on an invisible pairing. Same shape: `Level/WaveMaterialisation.cs:168` `_transportBegun`, `States/PlayingState.cs:53` (the comment does the name's job). Coupling at a distance: 16 entity files make 160 direct reads of `Tuning/` statics, so an entity's behaviour depends on a class it never receives. |
| 6 | Avoid negative conditionals | **Pass** | 159 `if (!x)` guards, and every one I sampled negates a positively named predicate — `if (!Enabled || AttractMuted) return;` (`Audio/Sound.cs:62`), `if (!CanPlay(number))` (`SoundBoard.cs:182`), `if (!IsWriteUnderWay())` (`:188`). **0** negatively named booleans (`noX`, `isNotX`) anywhere. |

### Names rules

| # | Rule | Verdict | Evidence |
|---|---|---|---|
| 1 | Choose descriptive and unambiguous names | **Pass** | The repo's NAM section is far stricter than the gist and is largely followed: unit-suffixed measures (`MoveDelayBeats`, `HulkBeatIntervalRomFrames`), predicate booleans, verb-first methods. |
| 2 | Make meaningful distinction | **Fail** | Two unrelated nested classes named `Process`; `Entities/Grunt.cs:233` `SpeedUp` beside `:245` `WaveSpeedTick` — two speed rules whose names differ by an unexplained word; `Audio/Sound.cs:40` and `Audio/Synthesis/SoundBoard.cs:147` both `IsPlaying` with different meanings; `Tuning/HudLayout.cs:15` `GameOverMessageColumn` against the comment at `Tuning/ScreenTuning.cs:17` pointing at a "Messages block" column of another name. |
| 3 | Use pronounceable names | **Pass** | No abbreviations in identifiers we own. The 6800 labels (`RNT`, `NFRQ2`, `PSTKIL`) appear only inside `<c>` quotes as ROM cross-references, which the terminology ledger requires. |
| 4 | Use searchable names | **Fail** | Magic numbers (Names 5) are the unsearchable values; the 309 shorthand summaries and 592 undocumented members are the unsearchable meanings — searching `GruntMoveDelay` finds a constant, searching "the crowd roar" finds an assembly listing. |
| 5 | Replace magic numbers with named constants | **Fail** | `Graphics/SpriteSet.cs:115-120,153-156` (frame counts 2, 6, 2, 3, 3 and `Create(6, 1…)`); `AttractMode/AttractPageMachine.cs:76,94,102,109,207-208,233,239,267`; `Level/LevelParameters.cs:38-50` (record defaults); `Entities/Grunt.cs:233` `* 7 / 8` and `:245` `stepBeats = 4`; `Palette/GamePalette.cs:85` (`slot = 10; slot <= 15`); `Graphics/SpriteFactory.cs:168,184` (`13`, `13 * 13`); `Hud/ArcadeHud.cs:118` (`wave >= 10`). |
| 6 | Avoid encodings — no prefixes or type information | **Pass, with one note** | No Hungarian types, no `Impl`/`Interface`/`Base` suffixes. The `_` prefix on private fields is a C# convention, not an encoding — the gist's letter loses to "Follow standard conventions" (General 1). The note: the `Sprite` suffix on `SpriteSet` members (`WallPixelSprite`, `TitleWordmarkCoreSprite`) appends the member's type to its name, and the refactoring ledger records that suffix as a deliberate disambiguation — keep it, and know which rule it trades against. |

### Functions rules

| # | Rule | Verdict | Evidence |
|---|---|---|---|
| 1 | Small | **Fail** | `Graphics/SpriteSet.cs:111-173` constructor, 62 lines (the repo's own cap is 50); `tools/SpriteExtractor/Program.cs:53` `Main` 222, `:376` `ExtractOne` 61, `:531` `CollectData` 53. |
| 2 | Do one thing | **Fail** | `Level/PlayField.cs:298-314` numbers its own jobs in comments (`// 1. Wall.`, `// 8. Player — ALWAYS last`) — a method that has to list its responsibilities. `RobotronGame.cs:60,68` the same. |
| 3 | Use descriptive names | **Pass** | Names say what the call does at the call site; `Tick`, `Materialise`, `PruneDead`, `RespondToLaserHit` read as sentences. |
| 4 | Prefer fewer arguments | **Partial** | 20 methods with 5+ parameters, maximum 6, and they are all drawing coordinates: `Graphics/ArcadeText.cs:74,96,170,182`, `Hud/ArcadeHud.cs:26,134`, `States/HighScoreTableState.cs:270`, `Graphics/BlitterDraw.cs:84`. A pen/position value object (x, y, slot) would cut three of them. |
| 5 | Have no side effects | **Partial** | No method named `Get`/`Is` mutates state. The gaps are the hidden work behind a property: `Entities/ScoreBurst.cs:109`, `Entities/StripEffect.cs:166`, `States/StorylineState.cs:243` each allocate a `new IntVector2` per read, and `Audio/Sound.cs:40` `IsPlaying` hides a null-conditional engine lookup. `Hud/PauseToggle.cs:23` `Tick(bool)` mutates and returns nothing, `Hud/HighScorePageHold.cs:62` `Tick(bool)` mutates and returns a `bool` — the same name, two shapes. |
| 6 | Don't use flag arguments | **Fail** | Seven parameters that choose the method's behaviour: `Level/WaveMaterialisation.cs:185` `beamsIn` (selects whether the transporter exists at all), `Level/WaveStartSequence.cs:135` `isBrainWave` (selects the appear length), `AttractMode/AttractObjectMachine.cs:105` `showsBrain`, `Entities/Quark.cs:402` `isPositive`, `Entities/Spheroid.cs:527` `wrapPass`, `Audio/Synthesis/SingSound.cs:210` `setsLoudness`, `States/HighScoreTableState.cs:270` `isLarge`. (`Hud/PauseToggle.cs:23` `isPauseHeld`, `Hud/HighScorePageHold.cs:62` `anySwitchHeld`, `Hud/HighScorePrintSequence.cs:76` `frameFinished` are state read from outside, not flags — not part of this finding.) |

### Comments rules

| # | Rule | Verdict | Evidence |
|---|---|---|---|
| 1 | Always try to explain yourself in code | **Pass** | Unit-suffixed names, named constants for most values, and the STR-2 rule that a comment header means several methods. |
| 2 | Don't be redundant | **Fail** | `Level/PlayField.cs:290` restates `Entities.PruneDead();`; `Entities/Grunt.cs:107` restates the `_random.Next` line; `:179-181` restates the `%` expression; `Level/PlayerSlot.cs:28` and `:40` document the same fact twice. |
| 3 | Don't add obvious noise | **Pass** | No `// increments the counter` noise; the comments carry ROM provenance, which is not obvious from the code. |
| 4 | Don't use closing brace comments | **Pass** | **0** sites. |
| 5 | Don't comment out code. Just remove | **Pass** | **0** commented-out statements in 323 files. |
| 6 | Use as explanation of intent | **Pass** | The strongest area of the codebase: nearly every rule quotes the ROM routine and address it implements (`AttractMode/AttractPageMachine.cs:13` names `MESS, DONE, COLOR, GRUNTS, DONE2`; `Hud/ScoreFormatter.cs:6` explains the BCD score). |
| 7 | Use as clarification of code | **Pass** | Unit and provenance clarifications throughout, e.g. `AttractMode/MovieObject.cs:12-13` explaining the fixed-point arithmetic. |
| 8 | Use as warning of consequences | **Pass** | `Level/PlayField.cs:314` "// 8. Player — ALWAYS last", `Tuning/PlayerTuning.cs:84` "TURN THIS OFF", `Audio/Synthesis/SoundBoard.cs:188`'s write-under-way guard, `States/WaveClearState.cs:114` "the six colour processes must not fight". |

### Source code structure

| # | Rule | Verdict | Evidence |
|---|---|---|---|
| 1 | Separate concepts vertically | **Partial** | STR-5 orders members by visibility and role, and `WaveMaterialisation`/`WaveStartSequence`/`CollisionResponder` show the split being done. `PlayField` still interleaves scoring, spawning, sound, pruning and drawing in one 887-line file. |
| 2 | Related code should appear vertically dense | **Pass** | The opcode interpreters keep each opcode with its handler; the tuning classes keep a value with the sentence that explains it. |
| 3 | Declare variables close to their usage | **Pass** | Locals are declared at their first use in every file sampled. |
| 4 | Dependent functions should be close | **Pass** | Private helpers sit under the public method that calls them (`Entities/Enforcer.cs`, `Entities/Grunt.cs`). |
| 5 | Similar functions should be close | **Pass** | `Graphics/ArcadeText.cs:74-182` groups the four large/small text and number draws together. |
| 6 | Place functions in the downward direction | **Pass** | Constructors and public methods read top-down into the private helpers below them. |
| 7 | Keep lines short | **Fail** | **42 lines over 140 characters.** Worst: `AttractMode/AttractMovieData.cs:231` (289), `Entities/Spark.cs:30` (226), `Entities/StripEffect.cs:503` (203), `Tuning/HudLayout.cs:21` (202), `Entities/Spark.cs:78` (197), `Entities/Tank.cs:412` (189), `tools/SpriteExtractor/Program.cs:334` (184). `.editorconfig` sets no `max_line_length`, so nothing enforces it. |
| 8 | Don't use horizontal alignment | **Pass** | **0** aligned `=` declarations. |
| 9 | Use white space to associate related things | **Pass** | Blank lines separate the constructor from the fields and each member from the next (FMT-1); no blank line after an opening brace. |
| 10 | Don't break indentation | **Pass** | 0 tabs, consistent 4-space indent per `.editorconfig`. |

### Objects and data structures

| # | Rule | Verdict | Evidence |
|---|---|---|---|
| 1 | Hide internal structure | **Fail** | 56 public ROM tables are writable arrays (`Level/WaveTable.cs` 24 tables, `Level/RobotKinds.cs:33`, `Level/Collisions/CollisionRules.cs:12`, `AttractMode/AttractMovieData.cs` 7, `Input/InputActions.cs:7`, …) — any caller can write to them. `Level/TransportImage.cs:74` hands out the animation frame's pixels as a `public Color[]`. `Graphics/SpriteSet.cs` exposes `Blitter` and `TextRenderer`, which is what makes the Demeter failures reachable. |
| 2 | Prefer data structures | **Pass** | The ROM data is in plain tables and records, and the behaviour reads them. |
| 3 | Avoid hybrid structures (half object, half data) | **Partial** | 6 records carry behaviour: `Level/PlayerSlot.cs:31,40` is the clearest — data with a mutable `Wave` that the record also mutates, and the same sentence documenting it twice (`:30` and `:33`); `Level/LevelParameters.cs`, `AttractMode/MovieObject.cs`, `Level/RobotKindInfo.cs`, `Persistence/HighScoreEntry.cs` are small and defensible; `States/GameServices.cs` is a collaborator bag (data shape, service role). |
| 4 | Should be small | **Fail** | 11 files over the 500-line cap: `PlayField.cs` 887, `Brain.cs` 725, `AttractObjectMachine.cs` 675, `Spheroid.cs` 598, `Hulk.cs` 580, `WaveTableSound.cs` 549, `StripEffect.cs` 542, `Player.cs` 540, `Prog.cs` 536, `Tank.cs` 504, `tools/SpriteExtractor/Program.cs` 886. |
| 5 | Do one thing | **Fail** | `PlayField` orchestrates, scores, spawns, sounds and draws; `AttractObjectMachine` is an interpreter, a walker and an explosion source; `SpriteExtractor/Program.cs` is argument parsing, ROM location, decoding and file writing. |
| 6 | Small number of instance variables | **Pass** | The maximum is 18 (`RobotronGame.cs`, `Audio/Synthesis/CrowdRoarSound.cs`), then 16 (`Spheroid.cs`), 13 (`SingSound.cs`), 12 (`Player.cs`, `Brain.cs`). No class is a god object by field count. |
| 7 | Base class should know nothing about their derivatives | **Pass** | **0** `abstract class` declarations in `src/` — the design is interfaces plus composition (`IEntity`, `IExplodable : IEntity`), so the rule cannot be broken. |
| 8 | Better many functions than passing code to select a behaviour | **Pass** | No strategy-passing parameters. The 7 flag arguments (Functions 6) are the same smell in the other direction. |
| 9 | Prefer non-static methods to static methods | **Partial** | 74 static classes: 21 in `Tuning/` and 14 in `Audio/Synthesis/` are data tables and pure helpers, which is fine. `Audio/Sound.cs` is a static mutable facade (the repo names it as debt to shrink), and `Hud/ArcadeHud.cs:26` `DrawCenteredLargeText(spriteBatch, sprites, …)` is a static that takes the collaborator it is missing (the repo's feature-envy rule, STR-8). |

### Tests

| # | Rule | Verdict | Evidence |
|---|---|---|---|
| 1 | One assert per test | **Fail (by the letter)** | 453 of 619 test methods assert more than once; the most is 20 (`PlayFieldCollisionTests.cs:116` `WallAndLaserCollideSlots_FollowTheRomWaveTables`, `DefineInputsModelTests.cs:28` 18, `AttractMovieTests.cs:76` 16). Read as "one behaviour per test" the codebase passes: each multi-assert test checks one named behaviour across a table of values. See the divergence section. |
| 2 | Readable | **Pass** | Test names are sentences about behaviour, not phases (TEST-4), e.g. `Brain_CatchingHuman_ReprogramsItThenLeavesAProg_NoSkull`, `ABoltNeverLeavesThePlayfield`. Note the two naming idioms (Understandability 1). |
| 3 | Fast | **Pass** | **0** `Thread.Sleep`, `Task.Delay`, `Stopwatch` or wall-clock waits in tests. |
| 4 | Independent | **Pass** | The one global-state test pair is handled properly: `Audio/SoundFacadeCollection.cs:13` is `[CollectionDefinition("SoundFacade", DisableParallelization = true)]`, both `SoundTests` and `SoundWiringTests` are in it, and each saves and restores `Sound.Enabled`/`AttractMuted` in `Dispose`. |
| 5 | Repeatable | **Pass** | **0** unseeded `new Random()` in tests; randomness is injected through `ScriptedRandom`. The filesystem tests use `Path.GetTempPath()` with a `Guid` name and delete in a `finally` (`Persistence/ControlSettingsStoreTests.cs:55-57`). |

### Code smells

| # | Smell | Verdict | Evidence |
|---|---|---|---|
| 1 | Rigidity — a small change cascades | **Fail** | The `SpriteSet` split (F03) touches 30 files because of the 86 second-dot calls. The rename cost is visible in the wreckage: `Graphics/SpriteSet.cs:333,334,341` still `cref` the pre-rename `TitleWordmarkCore`/`TitleWordmarkRim`. |
| 2 | Fragility — one change breaks many places | **Fail** | 56 writable global tables: one write to `WaveTable.Grunts[0]` changes every wave. `Entities/Player.cs:110` reads `PlayerTuning` directly, so the invincibility flag can be flipped from anywhere and no test can see it. |
| 3 | Immobility — parts cannot be reused | **Partial** | 90 files reference MonoGame, and 158 public members carry `Texture2D`, `Color` or `SpriteBatch` in their signature. For a desktop game port this is accepted; it is why the entity layer cannot be driven headless without `TestSprites`/`NoSpriteSource`. |
| 4 | Needless complexity | **Partial** | `SpriteExtractor/Program.cs:53`'s 222-line `Main`, and the two opcode interpreters that keep their opcodes as bare numbers (`AttractPageMachine.cs:94,102`). |
| 5 | Needless repetition | **Fail** | `Graphics/SpriteSet.cs:111-173`: 29 near-identical `source.LoadAll(...)`/`source.Create(...)` lines in a 62-line constructor. `AttractMode/AttractPageMachine.cs:207-208` and `:267`: the same `_gruntsLeft = 14; _gruntTimer = 16;` pair. The same `PhaseInput` stub in two test files. The same malformed `</item></list>` block in 21 files. |
| 6 | Opacity — the code is hard to understand | **Fail** | 309 summaries whose first sentence is 6800 mnemonics (`Audio/Synthesis/CrowdRoarSound.cs:127` = "`RNT`: `BSR`, `LDAA NFRQ2`, `DEC NFRQ`, `BEQ`"), 592 public members with no summary, and 102 comments whose XML does not parse. |

---

## New findings from this checklist

Items the 2026-10-11 standard-based review did not record. Rule IDs in brackets are the
repo's own, so a fixer can use one list.

```
[Major] gist Functions 6 (no flag arguments) — Level/WaveMaterialisation.cs:185
  `WaveMaterialisation(Random random, bool beamsIn)` (the flag decides whether
  `_transporter` exists at all, :187), Level/WaveStartSequence.cs:135
  `WaveStartSequence(int robotListCount, bool isBrainWave)` (:137 picks the appear length),
  AttractMode/AttractObjectMachine.cs:105 `BeginMono(…, bool showsBrain)`,
  Entities/Quark.cs:402 `ComputeAxisVelocitySubpixels(int scale, bool isPositive, …)`,
  Entities/Spheroid.cs:527 `AdvanceEscapeBeat(PlayField field, bool wrapPass)`,
  Audio/Synthesis/SingSound.cs:210 `CyclesBeforeEcho(bool setsLoudness)`,
  States/HighScoreTableState.cs:270 `DrawRank(…, int slot, bool isLarge)`.
  Why: the gist bans a flag parameter and says to split the method so the client calls the
  one it means. SOLID-A bans the same thing as an abstraction leak ("a 'just for one
  implementer' flag on a shared method"). Every caller of `WaveMaterialisation` must answer
  a question about the wave that the wave's own `BrainCount` already answers.
  Fix: `WaveMaterialisation.WithTransport(random)` / `.WithoutTransport(random)`, and
  `WaveStartSequence.ForBrainWave(count)` / `.ForRobotWave(count)`; `isPositive` becomes
  `ComputeAxisVelocitySubpixels(scale, coordinateUnit)` with the sign in the unit.
```

```
[Major] gist Understandability 5 (avoid logical dependency) —
  States/PlayingState.cs:166-169 reads `_restartHandled` and :318 is the only writer that
  clears it, 150 lines away; Level/WaveMaterialisation.cs:168 `_transportBegun`;
  Level/PlayField.cs:61 `_tankShellBugEnabled`.
  Why: "Don't write methods which work correctly depending on something else in the same
  class." The death branch is correct only if some other method remembered to reset the
  flag, and nothing says so at the reader's decision point — the comment at :53 admits it
  ("one-shot so the death branch fires exactly once").
  Fix: make the one-shot a type (`RestartHandler` with `TryHandle(...)`), so the pairing is
  in the type, not in a distance between two methods.
```

```
[Major] gist Understandability 3 (encapsulate boundary conditions) — the playfield clamp is
  written per entity: CruiseMissile.cs:258,270, Enforcer.cs:272-273, Hulk.cs:441-442,
  Spark.cs:223, and 120 `Math.Clamp/Max/Min/Sign` sites across 49 files.
  Why: "Boundary conditions are hard to keep track of. Put the processing for them in one
  place." Each entity repeats the same subtraction with its own collision size, so a change
  to the wall's inner edge is a 49-file search.
  Fix: one owner — `PortPlayfieldBounds.ClampToInside(entitySize)` (as the glossary names
  it) — and entities call it.
```

```
[Major] gist Source code structure 7 (keep lines short) — 42 lines over 140 characters, the
  longest 289 (AttractMode/AttractMovieData.cs:231), then Entities/Spark.cs:30 (226),
  Entities/StripEffect.cs:503 (203), Tuning/HudLayout.cs:21 (202), Entities/Spark.cs:78
  (197), Entities/Tank.cs:412 (189).
  Why: a 200-character prose line is unreadable in the IDE and in a diff, and it is where
  the value-quoting prose lives (HudLayout.cs:21 is the CMT-4 finding in a single line).
  `.editorconfig` has no `max_line_length`, so nothing enforces the rule.
  Fix: wrap the prose at 120 in `<remarks>` paragraphs; set `max_line_length = 120` for
  `*.cs` and fix the 42 sites.
```

```
[Major] gist Objects 1 (hide internal structure) — beyond the 56 writable ROM tables already
  logged as NUM-7: Graphics/SpriteSet.cs exposes `Blitter` and `TextRenderer` publicly, and
  Level/TransportImage.cs:74 `public Color[] Template { get; }`.
  Why: "Hide internal structure." The two public collaborators are what let 126 call sites
  reach through `SpriteSet` (Design 6), so one visibility fix shrinks two findings at once;
  `Template` hands out the pixels of an animation frame to anyone.
  Fix: make `Blitter`/`TextRenderer` `internal` (or private with the delegating methods the
  STR-11 fix wants), and `Template` an `IReadOnlyList<Color>`.
```

```
[Minor] gist Names 2 (make meaningful distinction) — Audio/Sound.cs:40 and
  Audio/Synthesis/SoundBoard.cs:147 are both `IsPlaying` with different meanings (the facade
  asks the engine, the board asks its own slot state); Hud/PauseToggle.cs:23 `Tick(bool)`
  mutates and returns nothing while Hud/HighScorePageHold.cs:62 `Tick(bool)` mutates and
  returns a `bool`.
  Why: "Make meaningful distinction" and the repo's CON-2 ("same name, same meaning").
  Fix: `Sound.IsAnySoundPlaying` / `SoundBoard.IsSlotPlaying`; name the returning one
  `TickAndReport(…)`.
```

```
[Minor] gist Understandability 1 + Tests 2 (be consistent) — 186 of 619 test names use
  `Method_Scenario_Expectation` and 433 use a sentence with no underscores
  (`ABoltNeverLeavesThePlayfield`, `ThePageIsTwentyOneLines_WithGapsBetweenTheSections`).
  Why: "If you do something a certain way, do all similar things in the same way" (CON-1).
  Both idioms are readable; the mix means a search for a method's behaviour has to guess the
  style.
  Fix: pick one (the underscore form scans better against the repo's `Method_…` naming) and
  convert the other set as a single PROC-5 refactoring entry.
```

```
[Minor] gist Objects 3 (avoid hybrid structures) — Level/PlayerSlot.cs:31 is a record of data
  that also carries behaviour, and :40 re-declares `public int Wave { get; set; }` publicly
  mutable, with the record's `<param name="Wave">` at :30 and the property's `<summary>` at
  :33 giving the identical sentence twice.
  Why: "Avoid hybrids structures (half object and half data)"; a public setter means any
  caller can move the wave a slot is on.
  Fix: keep the data in the record, move the behaviour to the owner that advances waves, and
  make the setter private.
```

```
[Minor] gist Functions 4 (prefer fewer arguments) — 20 methods take 5-6 arguments, all
  drawing coordinates: Graphics/ArcadeText.cs:74,96,170,182, Hud/ArcadeHud.cs:26,134,
  States/HighScoreTableState.cs:270, Graphics/BlitterDraw.cs:84, Graphics/SpriteFactory.cs:217.
  Why: "Prefer fewer arguments." `DrawTableNumber(spriteBatch, glyphs, value, x, y, slot)`
  passes a pen (x, y, slot) as three arguments at every call site.
  Fix: a `TextPen(int X, int Y, int Slot)` value object; `FillRect(canvas, rect, colour)`.
```

```
[Minor] gist Objects 9 (prefer non-static methods) — Hud/ArcadeHud.cs:26
  `public static void DrawCenteredLargeText(SpriteBatch spriteBatch, SpriteSet sprites, …)`
  and 303 other public static members.
  Why: "Prefer non-static methods to static methods." A static that receives its main
  collaborator is an instance method wearing a `static`; the repo's own STR-8 calls this
  feature envy.
  Fix: make the `ArcadeHud` helpers instance methods on the object that owns the text
  renderer; leave the `Tuning/` data classes static (they are data, not behaviour).
```

```
[Minor] gist Smells 5 (needless repetition) — Graphics/SpriteSet.cs:111-173 is 29
  near-identical `source.LoadAll(GetNumberedNames("Sprites/X", n))`/`source.Create(…)` lines
  in a 62-line constructor;
  AttractMode/AttractPageMachine.cs:207-208 and :267 repeat `_gruntsLeft = 14;
  _gruntTimer = 16;`.
  Why: "Needless Repetition." The pair in the page machine can drift apart, and the loading
  wall hides the one fact worth reading (how many animation frames each character has).
  Fix: a table of (member, path, frame count) rows loaded in a loop; one named constant pair
  for the grunt script.
```

---

## Where the checklist and the repo standard disagree

A checker running the gist alone would change things the repo has decided deliberately.
These are recorded, not filed as findings.

| Gist rule | Repo position | Decision needed |
|---|---|---|
| **Tests 1 — one assert per test** | 453 of 619 tests assert more than once, up to 20. The repo standard requires tests named for behaviour (TEST-4), and the multi-assert tests walk a ROM table checking one rule. | Keep "one behaviour per test". Splitting `WallAndLaserCollideSlots_FollowTheRomWaveTables` into 20 tests would lose the table shape the test is asserting. |
| **Names 6 — no prefixes** | `_camelCase` private fields (323 of 323 instance fields) is the C# convention, and General 1 says follow standard conventions. | Keep the `_`. |
| **Names 6 — no type information in names** | The `Sprite` suffix on `SpriteSet` members was added deliberately (refactoring-ledger) to separate a texture from a frame count. | Keep the suffix; it disambiguates, which is Names 2's higher priority. |
| **Understandability 4 — dedicated value objects over primitives** | The unit family (beats, port ticks, ROM frames, clock units, arcade/port pixels, subpixels) is bare `int` with a unit suffix, by the glossary's unit table. | Keep the suffixes; a `Beat`/`PortTick` type per unit is a bigger change than the naming rule asks for. |
| **Objects 9 — prefer non-static methods** | 21 `Tuning/` classes and 14 `Audio/Synthesis/` helpers are static data and pure functions. | Keep them static; the finding covers only statics that take their collaborator. |
| **Design 2 — prefer polymorphism to switch** | The opcode interpreters switch over ROM script bytes; the ROM's own structure is a table of opcodes. | Keep the switches; add named constants for the opcodes (NUM-1). |
| **Immobility** | MonoGame types in 158 public signatures. Accepted for a desktop port; `TestSprites`/`NoSpriteSource` give the tests their seam. | Accepted. |

---

## Genuine strengths, for the record

Evidence that this is a carefully kept codebase, from the same scans:

- **0** suppressed warnings, **0** commented-out code, **0** closing-brace comments,
  **0** tabs, **0** aligned declarations, **0** negatively named booleans, **0**
  `abstract class` with derivative knowledge, **0** threading hazards.
- **0** unseeded randomness and **0** sleeps in 619 tests; global static state in tests is
  quarantined by a `DisableParallelization` collection with save/restore in `Dispose`.
- Comments as intent and as warning (Comments 6 and 8) are the best-written part of the
  repo: ROM routine, address and consequence at the point of decision.
- All 323 instance fields follow one naming convention; every file follows one indentation
  and one member order.

## Summary

**33 of the gist's 66 rules pass, 12 are partial, 20 fail, 1 does not apply.**

The failures cluster in two places. First, **what the reader sees**: line length (42 lines
over 140 characters, longest 289), magic numbers, opacity (309 summaries in assembly
shorthand, 592 undocumented members), and repetition (55 near-identical loading lines).
Second, **what the type system could enforce but does not**: 86 second-dot calls
(Law of Demeter), 56 public writable global tables (hide internal structure), 7 flag
arguments, and the playfield's boundary condition written 120 times in 49 files.

The gist's own general rules describe the diagnosis: General 4, "always find root cause",
is the rule the codebase is failing, and it is the reason the two biggest lists exist at
all — the invincibility flag is a symptom of a test default in production, and every
documentation finding is a symptom of `GenerateDocumentationFile` being off.

Nothing in the checklist's failures contradicts the repo standard except the seven items in
the divergence table, and those read as deliberate decisions. The three fixes with the best
ratio of reach to cost: turn on `GenerateDocumentationFile`, make `SpriteSet.Blitter` and
`TextRenderer` `internal`, and set `max_line_length = 120`.
