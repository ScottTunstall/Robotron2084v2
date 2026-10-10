# Code review against `docs/coding-standards.md` (2026-10-10)

Full-repository review of `src/`, `tests/` and `tools/` against the standard in
[coding-standards.md](coding-standards.md). Every rule ID below cites that document.

**Baseline:** `dotnet build Robotron2084.slnx -c Debug` — 0 warnings, 0 errors.
`dotnet test Robotron2084.slnx` — 786 passed, 0 failed, 51 skipped.
Working tree clean (no untracked files). No suppressions (`NoWarn`, `#pragma`,
`SuppressMessage`) anywhere. No `RomTicks` names. No downcasts (SOLID-L). No external
`LifeState` comparisons (STR-9). No hand-written robot-kind lists outside `RobotKinds`
(STR-6). All labelled test hooks are used only by tests (CMT-9).

Findings are ranked most severe first.

---

## Blocker

```
[Blocker] TEST-2 src/Robotron2084/Level/PlayField.cs:84 — the constructor defaults
  `playerInvincibleForTesting` to `true`, and the one production call site
  (src/Robotron2084/States/PlayingState.cs:250) omits it.
  Why: the real game runs with the playtest aid on: the production player cannot be
  killed. `PlayerTuning.PlayerInvincibleForTesting` is hard-wired to `true`
  (src/Robotron2084/Tuning/PlayerTuning.cs:83, "flip to false when playtesting is
  done"), and `Player.InvincibleForTesting` (src/Robotron2084/Entities/Player.cs:90)
  defaults to it, so even a directly constructed Player is invincible.
  Fix: default the constructor parameter to `false`, set
  `PlayerTuning.PlayerInvincibleForTesting` to `false`, and make production pass the
  aid explicitly where it is wanted. Keep the attract demo's explicit `false`
  (AttractState.cs:119).
```

```
[Blocker] TEST-1/TEST-3 src/Robotron2084/Entities/Player.cs:90 — `InvincibleForTesting`
  is a public settable property with no "(test hook)" label, and the "TEMPORARY
  playtest aid" it carries names no tracking item.
  Why: a public switch any caller can flip, marked temporary without a tracking
  reference (TEST-3), and not following the one idiom per need (TEST-1: test hooks
  are `internal`, labelled "(test hook)", grouped last).
  Fix: make it `internal`, add "(test hook)" to the summary, group it with the
  other test hooks, and name the tracking item in the summary.
```

## Major

```
[Major] SOLID-I src/Robotron2084/Entities/*.cs — eighteen `IEntity.Update(GameTime,
  PlayField)` implementations document parameters they never use: BerzerkRobot.cs:136,
  CruiseMissile.cs:146, Electrode.cs:94-95, Enforcer.cs:134, Gorf.cs:129, Grunt.cs:156,
  Hulk.cs:186, Human.cs:203, PlayerLaser.cs:83, Prog.cs:235, Quark.cs:164,
  RescueScoreMarker.cs:66-67, ScoreBurst.cs:139-140, SkullMarker.cs:63-64, Spark.cs:138,
  Spheroid.cs:192, StripEffect.cs:208-209, Tank.cs:211; plus
  StorylineState.cs:246 where `MovieExplosionSource.Update` ignores both arguments
  entirely.
  Why: the standard is explicit: "Parameters documented as 'Unused (kept for the
  uniform shape)' are not allowed. Delete them." Nearly the whole entity layer is in
  the wrong interface shape.
  Fix: split the interface so the tick-driven entities get a clock-unit `Update()`
  (or drop the arguments each type ignores), and let only the entities that really
  need `field` (Player, Brains, Spheroids, ...) take it.
```

```
[Major] CMT-8 src/Robotron2084/Robotron2084.csproj — `GenerateDocumentationFile` is
  not enabled, in the csproj or in Directory.Build.props.
  Why: the standard requires it on "so the compiler enforces all of this" (summaries
  on every member, matching `<param>` tags, resolving crefs). The clean build
  proves nothing about documentation because the compiler is not checking it.
  Fix: set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` (with
  `<TreatWarningsAsErrors>` as the rest of the repo expects) and fix whatever the
  compiler then reports.
```

```
[Major] ERR-1 src/Robotron2084/Persistence/HighScoreStore.cs:34,
  GameSettingsStore.cs:45, ControlSettingsStore.cs:47 — `catch (Exception)` around
  file reads, silently returning the factory defaults.
  Why: a transient failure (file locked, disk full on the write-back) is indistinguishable
  from "no file": the game loads defaults and the next save overwrites the user's
  file. This is the data-loss pattern ERR-1 names.
  Fix: catch `IOException`, `JsonException`/`FormatException` (or the INI's
  `FormatException`) and surface a readable error; do not fall back to defaults on an
  unreadable file that exists.
```

```
[Major] STR-1a — files over the 500-line hard cap:
  src/Robotron2084/AttractMode/AttractObjectMachine.cs: 688 lines
  src/Robotron2084/Level/PlayField.cs: 622 lines
  src/Robotron2084/Audio/Synthesis/WaveTableSound.cs: 513 lines
  src/Robotron2084/Entities/Brain.cs: 506 lines
  tools/SpriteExtractor/Program.cs: 886 lines
  Why: the cap is hard; each file has taken on a second job (the object machine also
  holds its process records and opcode families; Program.cs holds the ROM parser,
  the extractor and the PNG writer).
  Fix: split each one per STR-1 and give the new classes their own files (STR-3).
  (tests/.../StripEffectTests.cs at 599 and tests/.../PlayFieldBrainProgMissileTests.cs
  at 837 also exceed it; the standard's length scan scopes to src and tools, so these
  are notes, not findings.)
```

```
[Major] STR-2a — methods over the 50-line hard cap:
  tools/SpriteExtractor/Program.cs:53  Main — 265 lines
  src/Robotron2084/Input/InputBinding.cs:15  ExpandDirection — 190 lines
  tools/SpriteExtractor/Program.cs:376  ExtractOne — 60 lines
  src/Robotron2084/Graphics/SpriteSet.cs:75  constructor — 58 lines
  src/Robotron2084/Entities/Prog.cs:237  Update — 55 lines
  src/Robotron2084/States/StorylineState.cs:107  Update — 55 lines
  tools/SpriteExtractor/Program.cs:531  CollectData — 53 lines
  Why: hard cap; the two Update bodies and the SpriteSet constructor mix setup
  steps that read as several methods each.
  Fix: extract named helpers (STR-7) until each body fits.
```

```
[Major] STR-3 — nested types in src:
  AttractMode/AttractObjectMachine.cs:151  record MovieProcess
  AttractMode/AttractObjectMachine.cs:64   enum MovieAction
  Core/DisplayInfo.cs:39                   struct Rect
  Entities/Prog.cs:188                     record struct Ghost
  Graphics/TunnelEffect.cs:236             record struct Ring
  Level/BozoMode.cs:29                     record Row
  Level/DifficultyTuning.cs:150            record struct Header
  Level/LevelParameterGenerator.cs:117     record LevelTableRow
  Palette/HighScorePalette.cs:204          class Process
  Palette/PaletteAnimator.cs:112           class Process
  Persistence/HighScoreStore.cs:58         record SavedTable
  States/StorylineState.cs:246             class MovieExplosionSource
  and src/Robotron2084/Hud/SettingsModel.cs holds two top-level types
  (enum SettingsAction + class SettingsModel) in one file.
  Why: "Do not nest a type inside another class — pull it out and give it its own
  file"; one type per file, named after it.
  Fix: give each type its own file with `internal` visibility.
  (Tests also nest small helpers — PlayerAnimationAndAimTests.cs:21,
  StripEffectTests.cs:35, DemoPlayerInputSourceTests.cs:21, PixelCollisionTests.cs:20,
  PlayFieldBrainProgMissileTests.cs:22 — flagged as Minor below.)
```

```
[Major] NAM-10 — calculated values exposed as properties, across the codebase. The
  three examples the standard itself names are all present:
  Core/DisplayInfo.cs:46-47        Width => Right - Left, Height => Bottom - Top
  Level/PlayerSlot.cs:38           HasMen => Lives > 0
  Persistence/GameSettings.cs:113  ExtraManEveryPoints => ExtraManEvery * 1000
  Other violations: every entity's `Bounds => new(...)` (BerzerkRobot.cs:77,
  Brain.cs:156, CruiseMissile.cs:83, Electrode.cs:45, Enforcer.cs:82, Gorf.cs:76,
  Grunt.cs:76, Hulk.cs:130, Player.cs:79, PlayerLaser.cs:36, Prog.cs:114-125,
  Quark.cs:123, RescueScoreMarker.cs:45, Spark.cs:93, Spheroid.cs:145,
  TankShell.cs:77, PlayfieldWall.cs:33); AttractMode/MovieObject.cs:79-88
  (Column, Row, ArcadeX, ArcadeY); Entities/Brain.cs:187 and Player.cs:116
  (WalkAnimationFrameIndex); Entities/Enforcer.cs:105 (GrowAnimationFrameIndex);
  Entities/Spark.cs:110 (AnimationFrameIndex); Entities/Grunt.cs:90 (IsFalling);
  Entities/Player.cs:93 (IsInvincible); Entities/Player.cs:110 (DeathSolidSlot);
  Entities/Tank.cs:149 (IsBeingBorn); Entities/StripEffect.cs:106 (Spacing);
  Graphics/TunnelEffect.cs:134,141,152 (IsErasing, OutermostRing, RingsRetained);
  Graphics/WordmarkAppear.cs:52,72 (IsFinished, StartedLetterCount);
  Graphics/WilliamsLogoBorder.cs:68 (IsFinished); Level/PlayField.cs:154,365
  (RobotsFrozen, GruntSpeedFloor); Level/RobotTransporter.cs:43 and
  Level/TransportImage.cs:65 (IsFinished); Level/WaveMaterialisation.cs:90
  (PendingCount); Palette/PresentationPagePalette.cs:67,74 (WordmarkColorSlot,
  WordmarkRimSlot); Hud/HighScoreFrameAnimation.cs:41,48,51;
  Hud/HighScorePrintSequence.cs:54,57; Hud/InitialsEntryModel.cs:114,117;
  Hud/DefineInputsModel.cs:55; Input/ButtonPresses.cs:10; Input/InputBinding.cs:46,50;
  Level/GameSession.cs:42 (IsTwoPlayer); Level/PlayerSlot.cs:42,46 (SpareMen,
  DisplayedMen); Audio/TransporterSound.cs:36 (IsRunning).
  Why: "A calculated value is a method, never a property, however small the
  calculation." The `Bounds` getters allocate a new Rectangle on every call; the
  LINQ-based ones loop each access.
  Fix: rename each to its method form per NAM-13 (`GetWidth()`, `HasMen()`,
  `GetBounds()`, `IsFalling()`, `GetAnimationFrameIndex()`, ...) and update call
  sites (NAM-8).
```

```
[Major] STR-11 — second-dot calls through another object, systematically:
  `_sprites.Blitter.DrawSprite(...)`, `_sprites.Blitter.GetSlotColour(...)`,
  `_sprites.Blitter.DrawSolidRectangle(...)`, `_sprites.Blitter.UsePassThrough()`
  in every entity's Draw (BerzerkRobot.cs:116, Brain.cs:201-204,
  CruiseMissile.cs:119-129, Electrode.cs:77, Enforcer.cs:119, Gorf.cs:113,
  Grunt.cs:116, Hulk.cs:174, Human.cs:167-176, Player.cs:139-144,
  PlayerLaser.cs:68, Prog.cs:207-220, Quark.cs:147, RescueScoreMarker.cs:62,
  ScoreBurst.cs:128-134, SkullMarker.cs:59, Spark.cs:122, Spheroid.cs:175);
  plus Player.cs:188 (`field.Input.Poll`) and Human.cs:291
  (`electrode.Bounds.Intersects`).
  Why: the standard names `sprites.Blitter.DrawSprite(...)` as the smell: "If a
  statement has a second dot after a property or a call, it is a smell."
  Fix: the named price — thin delegating methods on the near object
  (`_sprites.DrawWalkFrame(...)`, `field.PollInput()`, `electrode.Intersects(box)`).
```

```
[Major] TIME-3 — hidden randomness defaults: `random ?? new Random()` in
  BerzerkRobot.cs:72, Grunt.cs:70, Player.cs:65,
  Input/DemoPlayerInputSource.cs:44, Palette/PaletteAnimator.cs:57 and
  Palette/TunnelPalette.cs:85.
  Why: "`random ?? new Random()` defaults ... make behaviour unrepeatable." A null
  silently swaps in an unseeded source.
  Fix: make the constructor parameter non-nullable `Random` (or a required
  nullable that throws on null) so every caller chooses the source.
```

```
[Major] NUM-4 — unit conversions outside the named helpers:
  raw `* ScreenSize.SpecScale` in Entities/StripEffect.cs:193-201,
  Graphics/WordmarkAppear.cs:46,143,160-162, States/TitleScreenState.cs:150,311-312;
  raw `<< 8` / `>> 8` subpixel arithmetic in AttractMode/AttractObjectMachine.cs:107,
  274-275,416-417,646,671,680-681, AttractMode/AttractPageMachine.cs:154,
  AttractMode/MovieObject.cs:79,82 and Entities/StripEffect.cs:106,268,274,353
  (ScreenSize.SubpixelsPerPixel exists at Core/ScreenSize.cs:49 and is used by
  Enforcer, Quark and Spark — these files simply do not use it).
  Why: "Unit conversions go through named helpers ... never through inline
  arithmetic like `>> 8`"; the subpixel scale is named once, and these sites
  restate it.
  Fix: route every site through `ScreenSize.SubpixelsPerPixel` /
  `ToPortPixels` / `ToPortPixelsFromColumns`.
```

```
[Major] NUM-3 — the column written as `2`:
  Graphics/TunnelEffect.cs:303-304,365-367 (`firstColumn * 2`, `column * 2 + 1`),
  Hud/ArcadeHud.cs:55,88,91,111 (`arcadeColumn * 2`, `HudWaveTextColumn * 2`),
  Level/TransportImage.cs:169 (`column * 2`), States/StorylineState.cs:147
  (`exploded.Column * 2`), Level/LaserWallFlares.cs:22
  (`ScreenSize.ToPortPixels(2)` for one column).
  Why: "The column is never written as 2. Use `ScreenSize.Columns(n)` /
  `ArcadePixelsPerColumn`."
  Fix: replace each with `ArcadePixelsPerColumn` or a `...Columns` constant
  converted through the helper.
```

```
[Major] TIME-1 src/Robotron2084/Palette/WallColorCycle.cs:15-22 — the wall colour
  cycle is a `TimeSpan` timer (`_elapsed`, `_stepDuration`, millisecond constant).
  Why: "Do not introduce `TimeSpan`/`GameTime` timers in gameplay code"; the wall
  runs during play, and every other periodic thing uses the clock-unit accumulator.
  Fix: count the wall's step in clock units against
  `WavePaletteTables`'s ROM-frame interval.
  (States/TitleScreenState.cs:125-136 keeps TimeSpan timers for the attract
  page; attract is not gameplay — noted, not flagged.)
```

```
[Major] NAM-15 src/Robotron2084/AttractMode/MovieDescriptors.cs:11,22,39,40 —
  `const int Circle = 0x7EA9` and `const int Square = 0x7EA5` name the spheroid
  and the quark by forbidden words.
  Why: "never `Circle` or `Square`"; the attract screen names them Spheroid and
  Quark.
  Fix: rename to `Spheroid` and `Quark` (or `SpheroidScript`/`QuarkScript` if the
  value is a script address).
```

## Minor

```
[Minor] NAM-12 src/Robotron2084/Entities/CruiseMissile.cs:119 and
  src/Robotron2084/Level/LaserWallFlares.cs:49 — locals `trailColor` and
  `flareColor` use the American spelling.
  Fix: `trailColour`, `flareColour` (the type `Color` stays MonoGame's).
```

```
[Minor] CMT-4 src/Robotron2084/Graphics/TunnelEffect.cs:148 — summary says "53
  coloured then 53 black", restating a count that lives in a constant.
  Fix: name the constant in the summary instead of the number.
```

```
[Minor] NUM-1 src/Robotron2084/Hud/SettingsModel.cs:196-209 — the Turns and
  Difficulty line switches compare against bare literals (`< 2` ... `< 8`).
  Fix: name the band edges (they are the arcade's difficulty scale) or point the
  summary at the constants that define the scale.
```

```
[Minor] CMT-3/CMT-5 tests — history and playtest-round references in test
  comments: tests/.../PlayerAnimationAndAimTests.cs:13,72,101,102,123
  ("Round 8", "2026-09-12 playtest request", "the old behaviour"),
  tests/.../PlayerInvincibilityTests.cs:9 ("Round 7 playtest aid"),
  tests/.../PlayFieldBrainProgMissileTests.cs:17,823 ("round-6"),
  tests/.../PlayFieldCollisionTests.cs:227 ("round-7 playtest aid"),
  tests/.../GruntAnimationTests.cs:152 ("playtest round 12"),
  tests/.../HulkKnockbackTests.cs:15 ("Playtest 2026-09-13: the old fixed
  20-spec-px push"), tests/.../QuarkTankBehaviourTests.cs:11
  ("playtest round 10").
  Why: "No history in code" — the story belongs in git history and the notes.
  Fix: restate each comment as what the test pins now, with a `notes §NN` pointer
  where the reasoning is long.
```

```
[Minor] STR-3 (tests) — nested helper classes in
  tests/.../PlayerAnimationAndAimTests.cs:21, tests/.../StripEffectTests.cs:35,
  tests/.../Input/DemoPlayerInputSourceTests.cs:21,
  tests/.../Level/PixelCollisionTests.cs:20,
  tests/.../Level/PlayFieldBrainProgMissileTests.cs:22.
  Fix: one type per file, `internal` visibility.
```

```
[Minor] NAM-19 src/Robotron2084/Entities/ScoreBurst.cs:37 — `_showingPoints` and
  src/Robotron2084/Entities/Brain.cs:107 — `_reprogramMovingDown` are booleans that
  do not read as predicates.
  Fix: `isShowingPoints`, `isReprogrammingDown` (or a named state).
```

```
[Minor] NUM-7 src/Robotron2084/AttractMode/AttractObjectMachine.cs:60 —
  `DrainExplosions()` returns a mutable `List<MovieExplosion>`.
  Fix: return `IReadOnlyList<MovieExplosion>`.
```

## Notes (questions for the author / already acknowledged)

- **SOLID-D** — `Sound.Enabled`, `Sound.AttractMuted`
  (src/Robotron2084/Audio/Sound.cs:28,35) and `DevKeys.AttractFastForward`
  (src/Robotron2084/Input/DevKeys.cs:39) are static mutable state. The standard
  names these as "the existing ones to shrink, not grow": they are acknowledged
  debt, not new violations. No new static members were found.
- **Glossary** — `Human`/`HumanKind` "still carry the old word until the author
  decides on the rename" (glossary.md). Tracked; not flagged.
- **TIME-3 (root)** — `new Random()` at the two production roots
  (src/Robotron2084/RobotronGame.cs:209, src/Robotron2084/States/TitleScreenState.cs:255)
  is where the game's entropy legitimately enters; the finding above is about the
  `?? new Random()` defaults inside gameplay classes.
- **51 skipped tests** — the standard has no rule about skipped tests; the skips
  are visible in the test run output. Worth a look, but not a finding.

## Summary

**22 findings: 2 Blocker, 13 Major, 7 Minor, plus 4 notes.**
The most important is the first: the production game is currently running with the
playtest invincibility aid on, end to end (`PlayerTuning.PlayerInvincibleForTesting
=> true` → `Player.InvincibleForTesting` default → `PlayField` constructor default →
`PlayingState` omits the argument). Everything a wave does to the player is a no-op
in the real game until that chain is flipped.
The largest systemic items are the `IEntity.Update` interface (SOLID-I: eighteen
implementations with documented-unused parameters), the calculated-property
pattern (NAM-10: the standard's own three counter-examples all present, plus ~40
more), the `_sprites.Blitter.…` second-dot calls (STR-11: every entity's Draw),
and the eleven nested types (STR-3). The codebase is otherwise in strong shape —
clean build, green tests, no suppressions, faithful ROM cross-references, and the
orchestration rules (STR-12/STR-13) are followed — so the gaps are concentrated in
interface shape, the property/method line, and unit-conversion discipline.
Questions for the author: (1) should the SOLID-I fix split `IEntity.Update` into a
tick-driven form and a field-taking form, and (2) are the TimeSpan timers in
`TitleScreenState` acceptable outside gameplay, or should the attract page move to
clock units as well?
