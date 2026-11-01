# Code review ledger — 1 November 2026 (main game: `src/`, plus repo config)

Companion ledger for the test project: `code review 1-NOV-2026-tests.md`.

Standard used: `docs/coding-standards.md` (rule IDs cited on every finding).
Method: every "Mechanical scans" run from the standards' Part 2 over `src/` and repo config, plus targeted
semantic checks. Each hit was triaged; false positives were dropped. A separate entry gives what still needs a
by-hand pass. Line numbers are from commit `81f31f7` — re-verify before fixing if the tree has moved.

Severity follows the standards' table: **Blocker / Major / Minor / Note**.

---

## 1. Blocker

Ignore. Not an issue atm.

---

## 2. Major

### M-01 — [Major] CMT-9 "test hook" that production code uses
`src/Robotron2084/Graphics/TunnelEffect.cs:141` — `internal int RingsDrawn { get; private set; }` is
documented as a test hook (summary at line 141), but production code reads it:
`src/Robotron2084/States/WaveClearState.cs:93` and `:113` (`_ringsPaletted != _tunnelEffect.RingsDrawn`),
and `TunnelEffect.cs:264` bumps it. The label is false.
**Fix:** rename/document it as a real member (e.g. public, summarised as "rings coloured so far"); update the
test-hook label only on the members production never touches.

### M-02 — [Major] STR-1a — class files over the 500-line hard cap

Ignore.


### M-03 — [Major] STR-2a — methods over 50 non-blank lines
- `src/Robotron2084/Graphics/TextureSet.cs:111` — `TextureSet(ITextureSource)` constructor runs to line 173
  (~58–61 non-blank lines). Extract named loader helpers.
- `src/Robotron2084/States/TitleScreenState.cs:204` — `Update(GameTime, GameStateManager)` runs to line 266
  (~52–54 non-blank lines). Borderline; count blank-excluded lines and split to fit.
(Checked and cleared as false positives from the rough awk scan: `InputBinding.ExpandDirection` (17 lines),
`PlayerSlot.GetDisplayedSpareLives` (45).)

### M-04 — [Major] STR-3 — one type per file, no nested types
Files holding two top-level types:
- `src/Robotron2084/Audio/BoardSounds.cs:7` + `:28` — `BoardSound` record beside `BoardSounds` class.
- `src/Robotron2084/Hud/SettingsModel.cs:6` + `:47` — `SettingsAction` enum beside `SettingsModel`.

Nested types to pull out into their own files (each with `internal`/`private`-equivalent visibility):

| File:line | Nested type |
|---|---|
| `AttractMode/AttractObjectMachine.cs:189` | `private sealed record MovieProcess` |
| `Audio/Synthesis/FilteredNoise.cs:290` | `private readonly record struct NoiseSettings` (NAM-26 also names this one in its "bare settings" rule) |
| `Core/DisplayInfo.cs:41` | `private struct Rect` |
| `Entities/Prog.cs:539` | `private readonly record struct Ghost` |
| `Graphics/TunnelEffect.cs:379` | `private readonly record struct Ring` |
| `Level/BozoMode.cs:95` | `private sealed record Row` |
| `Level/DifficultyTuning.cs:282` | `private readonly record struct Header` |
| `Level/WaveParameterGenerator.cs:120` | `private sealed record CustomWaveTableRow` |
| `Palette/HighScorePalette.cs:290` | `private sealed class Process` |
| `Palette/PaletteAnimator.cs:203` | `private sealed class Process` (two same-named classes — NAM-9) |
| `Persistence/HighScoreStore.cs:77` | `private sealed record SavedTable` |
| `States/StorylineState.cs:216` | `private sealed class MovieExplosionSource : IExplodable` |

### M-05 — [Major] TIME-3 — hidden randomness
Gameplay classes must take a `Random` with no fallback; call sites must not build one ad hoc.
`?? new Random()` defaults:
- `src/Robotron2084/Entities/BerzerkRobot.cs:85`
- `src/Robotron2084/Entities/Grunt.cs:92`
- `src/Robotron2084/Entities/Player.cs:98`
- `src/Robotron2084/Input/DemoPlayerInputSource.cs:51`
- `src/Robotron2084/Palette/PaletteAnimator.cs:127`
- `src/Robotron2084/Palette/TunnelPalette.cs:185`

`new Random()` at call sites:
- `src/Robotron2084/RobotronGame.cs:221`
- `src/Robotron2084/States/DeveloperToolsState.cs:194`
- `src/Robotron2084/States/TitleScreenState.cs:264`

**Fix:** make the `Random` parameter required; give the composition root one `Random` to hand down (tests pass a
seeded one).

### M-06 — [Major] STR-11 — Law of Demeter clusters (second dot after a property)
`_textureSet.BlitterDraw.*` and `_textureSet.ArcadeText.*` are named by the rule itself as smells; the fix is a
thin delegating method on `TextureSet` (the rule explicitly says the delegation is "the price, correct") or
injecting `BlitterDraw`/`ArcadeText` directly.

`TextureSet.BlitterDraw.*` call sites:
- `Entities/BerzerkRobot.cs:120`; `Entities/Brain.cs:344,345,347`; `Entities/CruiseMissile.cs:124,126,130,133`;
  `Entities/Electrode.cs:62,63`; `Entities/Enforcer.cs:130`; `Entities/FamilyMember.cs:202,206,207,211`;
  `Entities/Gorf.cs:119`; `Entities/Grunt.cs:130`; `Entities/Hulk.cs:360`; `Entities/Player.cs:178,179,183`;
  `Entities/PlayerLaser.cs:55,56`; `Entities/Prog.cs:246,250,251,254,258,259`; `Entities/Quark.cs:172`;
  `Entities/RescueBonusMarker.cs:66`; `Entities/ScoreBurst.cs:140,141,146,147`; `Entities/SkullMarker.cs:58`;
  `Entities/Spark.cs:123`; `Entities/Spheroid.cs:280`; `Entities/Tank.cs:247,252`; `Entities/TankShell.cs:113`;
  `Graphics/TunnelEffect.cs:328,329`; `Hud/ArcadeHud.cs:146`; `Level/LaserWallFlares.cs:53,58,64`;
  `Level/RobotTransporter.cs:132,175`; `States/DefineInputsState.cs:174`; `States/DeveloperToolsState.cs:222`;
  `States/HighScoreTableState.cs:230,248,288`; `States/InitialsEntryState.cs:103,107`;
  `States/SettingsState.cs:161`; `States/StorylineState.cs:57,75,160,161,171,173,174,175,207`;
  `States/TitleScreenState.cs:310`.

`TextureSet.ArcadeText.*` call sites:
- `Hud/ArcadeHud.cs:44,51,113,119,121,154`; `States/DefineInputsState.cs:182,219,262,263`;
  `States/DeveloperToolsState.cs:228,253,259,260`; `States/HighScoreTableState.cs:190,193,257,273,275,316,324,345,346,347`;
  `States/InitialsEntryState.cs:71,76,96,125`; `States/SettingsState.cs:167,200,206,207`;
  `States/SoundTestState.cs:197,198,203,204`; `States/StorylineState.cs:85`; `States/TitleScreenState.cs:296,301,332,333`.

Other reaches:
- `Entities/Player.cs:251` — `playField.PlayerInputSource.Poll()`.
- `States/PlayingState.cs:155` — `_gameSession.ControlSettings.IsPauseHeld`.
- `States/StorylineState.cs:114` — `_attractMovie.AttractObjectMachine.DrainExplosions()`.
- `Level/Spawning/BerzerkRobotWaveSpawner.cs:22,25`, `GorfWaveSpawner.cs:17`, `HulkWaveSpawner.cs:55,57,65`,
  `TankWaveSpawner.cs:27,29` — `waveSpawnContext.SpawnPlacement.*`, `waveSpawnContext.FieldEntities.*`,
  `waveSpawnContext.Random.*` — the context bundles collaborators; give it the methods or pass the parts in.
- `Level/Spawning/MidWaveSpawner.cs:68` — `_fieldEntities.Grunts.Count(...)`.
- `Level/FieldEntities.cs:269` — `list.FieldEntities.All(...)` on a passed-in list.
- `States/SoundTestState.cs:122` — `_textureSet.BlitterDraw.GamePalette is { } palette` (also NAM-26, see m-24).

**Note for the fixer:** LINQ over a property (`fieldEntities.Electrodes.FirstOrDefault(...)` at
`Collisions/HulkVsFamilyMemberCollisionRule.cs:21`, `PlayerVsElectrodeCollisionRule.cs:17`,
`RobotVsElectrodeCollisionRule.cs:38`, `Level/WaveSurvivors.cs:34`) is a borderline case — the LINQ exemption vs
"call on something got from something else" needs the author's call; if flagged, the fix is a method on
`FieldEntities` (`GetElectrodes()` / `AnyElectrodeTouching(box)`). `CollisionResponder.cs` reads through
`...Result` records (lines 55–147) — that is STR-12's designed report shape; confirm with the author before
"fixing".

### M-07 — [Major] NAM-23 / NAM-13 — `Try…(…, out T)` pattern, which the standard forbids
"There is no `TryGet…(…, out T value)`"; split into `Has…` query + command, or return the item or `null`.
- `src/Robotron2084/Input/InputBinding.cs:132` `TryParse(string?, out InputBinding)`, `:154` `TryParseGamePad`,
  `:177` `TryStick(out bool, out int)`, `:212` `TryDirection(out int dx, out int dy)`.
- `src/Robotron2084/Level/TransportImage.cs:123` `TryGetPixelIndex(..., out int pixel)` (used 142, 154, 168).
- `src/Robotron2084/Level/WaveParameterGenerator.cs:103` `TryParseRow(..., out CustomWaveTableRow?)` (used 84).
- `src/Robotron2084/Audio/Synthesis/OscillatorSound.cs:283` `ChangeNoise(out int? noise)` — command + query +
  `out` in one method.
Framework `Enum.TryParse`, `int.TryParse`, `Dictionary.TryGetValue` are exempt (we do not own those names).

### M-08 — [Major] NAM-23 — commands that return a value (CQS)
- `src/Robotron2084/Graphics/ArcadeText.cs:74,96,133,170,182` — `DrawLargeFontText`, `DrawLargeTableNumber`,
  `DrawSmallFontText`, `DrawSmallTableNumber`, `DrawTableNumber` all draw *and* return the next x. Split, or
  return nothing and expose `GetTextRight(...)` as a query.
- Duplicated private copies of the same smell (see also CON-1, m-27):
  `States/DefineInputsState.cs:217`, `States/DeveloperToolsState.cs:251`, `States/SettingsState.cs:198`
  (`DrawText` returning int), `States/HighScoreTableState.cs:270` (`DrawRank` returning int),
  `States/DeveloperToolsState.cs:172` (`Move(int line, int step)` moves the selection *and* returns the line).
- `src/Robotron2084/Input/ButtonEdgeDetector.cs:14` — `Advance(PlayerInputState)` advances edge state *and*
  returns `ButtonPresses`. Split into `Advance(...)` + `GetCurrentPresses()`.
- `src/Robotron2084/Level/ScoreBoard.cs:39` — `Add(int points)` returns bool (whether it rolled over?).
- `src/Robotron2084/Level/GameSession.cs:193` — `SwitchToPlayerWithLives()` commands and answers.
- `src/Robotron2084/Hud/DefineInputsModel.cs:93` — `Assign(...)` returns bool.
- `src/Robotron2084/Persistence/HighScoreTable.cs:172` — `Submit(int, string)` returns `SubmitResult`; and
  `:241` `Insert(list, entry, capacity)` (private) mutates and returns bool.
- Audio emulation bool-returning commands (state mutation + bool answer; also NAM-4, since they are not
  `Is`/`Has` questions): `Audio/Synthesis/CrowdRoarSound.cs:433` `TestWhistleEnd`,
  `ElectricSound.cs:94` `CountRound`, `:106` `HoldHalfWave`, `FilteredNoise.cs:211` `ShrinkLimit`,
  `OscillatorSound.cs:320` `ChangeOscillator`, `:355` `MovePitch`, `:396` `StepDecay`,
  `ScreamSound.cs:112` `LowerPitches`, `SoundBoard.cs:454` `TakeNextChange`,
  `SquareWaveSound.cs:129` `ChangeLowWait`, `:146` `CountDown`, `WaveTableSound.cs:313` `FindPlayableRange`,
  `:405` `MovePitch`. If these names mirror ROM labels (they mostly do: `PLAY1`, `SHRLIM`, `MVPTCH`…), see
  N-03 — decide the exemption once, with the author.

### M-09 — [Major] SOLID-L — hard downcasts in a callback table
`src/Robotron2084/Level/Collisions/CollisionResponder.cs:24-33` — the dictionary of lambdas hard-casts
`CollisionResult` to each concrete result (`(LaserHitResult)collisionResult` etc.). The rule demands
pattern-matching with an `InvalidOperationException` naming the type, or a type-guaranteed dispatch
(e.g. generic `Dictionary<Type, Action<T>>` behind a `Respond<T>(T result)`).

### M-10 — [Major] NAM-10 — computed bool properties (must be methods)
A getter that compares or combines members is a method, per the rule's bullet list ("works a value out … with a
comparison"), and the family propagates through the interfaces:
- `src/Robotron2084/Audio/ISoundBoard.cs:13` and `IAudioSink.cs:7` — `bool IsPlaying { get; }` declarations.
- `src/Robotron2084/Audio/Synthesis/SoundBoard.cs:147` — `IsPlaying => _waitingSoundNumber != NoSoundNumber || …`.
- `src/Robotron2084/Audio/SoundBoardAudioSink.cs:68` — `IsPlaying => _soundBoard.IsPlaying`.
- `src/Robotron2084/Audio/SoundEngine.cs:46` — `IsBoardPlaying => _audioSink.IsPlaying`.
- `src/Robotron2084/Audio/Sound.cs:40` — `static IsPlaying => _soundEngine?.IsBoardPlaying ?? false`.
**Fix:** make them `IsPlaying()` / `IsBoardPlaying()` methods (NAM-4 says a bool *method* starts `Is`).
Stored-value bool properties (`ScoreBurst.IsVisible` etc.) are fine per NAM-27.
Also: `src/Robotron2084/States/SoundTestState.cs:122` — `CurrentSoundTestEntry => Entries[_index]` is a lookup
(NAM-10 "looks something up") → `GetCurrentSoundTestEntry()`.

### M-11 — [Major] NAM-15 — "Circle"/"Square" name forbidden for cast members
`src/Robotron2084/AttractMode/MovieDescriptors.cs:11` (`const int Circle`), `:22` (`const int Square`), used at
`:39,40`. These are the Spheroid and Quark movie descriptors. Rename to `Spheroid`/`Quark` (NAM-8: the old words
gone everywhere).

### M-12 — [Major] NAM-8 / rename decision — retired identifier "Human" still in use
The 2026-10-31 decision renamed `Human` → `FamilyMember`. Survivors:
- `src/Robotron2084/AttractMode/MovieWalk.cs:14` — enum value `Human` (the family's walk table).
- `src/Robotron2084/AttractMode/MovieDescriptors.cs:30,31,32` — `MovieWalk.Human` used for Mommy/Daddy/Mikey.
**Fix:** rename to `FamilyMember` (keeps the quoted ROM table name `HUMANA` in the ROM cross-reference).

### M-13 — [Major] NUM-4 — unit conversions as inline arithmetic instead of named helpers
Subpixel scale (256) exists as `ScreenSize.SubpixelsPerPixel` / `SubpixelBits` yet is applied as raw shifts:
- `src/Robotron2084/AttractMode/AttractObjectMachine.cs:108,157,180,181,306,307,442,443,668` (`>> 8`, `<< 8`).
- `src/Robotron2084/AttractMode/AttractPageMachine.cs:162` (`NextWord` packs bytes — ROM byte order, see the
  exempt list; but `:180-181` style subpixel use at AttractObjectMachine is not byte order).
- `src/Robotron2084/AttractMode/MovieObject.cs:80,86` (`XSubpixels >> 8`, `YSubpixels >> 8`).
- `src/Robotron2084/Entities/StripEffect.cs:415,455,548,554` (`_spacingAccumulator >> 8` — "256 times the gap"
  is the same subpixel idea; name the scale once).
Arcade-pixel → port-pixel as multiplication by the constant instead of `ScreenSize.ToPortPixelsFromArcadePixels`:
- `src/Robotron2084/Graphics/WordmarkAppear.cs:60,61,163,164,165,188,189` (`* ScreenSize.PortPixelsPerArcadePixel`).
- `src/Robotron2084/States/TitleScreenState.cs:165,307,308`.
**Fix:** named helpers, e.g. `ScreenSize.ToColumnsFromSubpixels(...)`, `ToSubpixelsFromColumns(...)`, and
`ScreenSize.ToPortPixelsFromArcadePixels(...)` at the use sites.

### M-14 — [Major] NUM-3 — the column written as 2
`src/Robotron2084/Level/LaserWallFlares.cs:29` — `ScreenSize.ToPortPixelsFromArcadePixels(2)`; 2 arcade pixels
is one column → `ScreenSize.Columns(1)`. (Also check the comment at `Tuning/SparkTuning.cs:29` which quotes
`ToPortPixelsFromArcadePixels(2)` — CMT-4: name the constant, not its arithmetic.)

### M-15 — [Major] NUM-1 — magic numbers (confirmed samples)
- `src/Robotron2084/AttractMode/AttractObjectMachine.cs:172` — `var columnOffset = -1;` (a bare `-1`; a sign is
  `DirectionSigns.Left`), `:176` — `columnOffset = 2;`, `:181` — literal `6` inside `(6 << 8)`.
- `src/Robotron2084/Hud/SettingsModel.cs:122-136` — arcade adjustment thresholds `2,3,4,5` and `3,5,6,8` as
  switch arms with no names (e.g. `TurnsPerPlayerRecommendedMax`, `DifficultyConservativeMin`…; values come
  from the arcade's GAME ADJUSTMENT table — cite the ROM in each constant's summary, NUM-1/FID-1).
- `src/Robotron2084/States/SoundTestState.cs:64` — `QuietTicksNeeded = 6` is named, but check its use site is
  via the constant only.
- A full NUM-1 sweep of method bodies was *not* part of the mechanical scans; run one when fixing this row
  (`grep -nP '[+\-*/] (?!\d;)[2-9]\b|\b-?\d{2,}\b'` over `src/`, excluding `const` lines, Tuning data and the
  "What not to flag" list).

### M-16 — [Major] CMT-4 — comments restating values that live in code
Values drift; name the constant instead.
- `src/Robotron2084/AttractMode/MovieObject.cs:39,42` — "(256 = one column = 2 arcade px)": 256 is
  `ScreenSize.SubpixelsPerPixel`; "2 arcade px" is a column fact.
- `src/Robotron2084/Entities/Hulk.cs:511` — "up to 16 columns sideways, or up to 16 rows" restates
  `TargetOffsetMaxExclusive`/`Min = 16`.
- `src/Robotron2084/Entities/Tank.cs:84` — "about 38 times in 100" quotes the seek chance's value
  (find/name the constant, then reference it).
- `src/Robotron2084/Graphics/TextureSet.cs:280` — "spare life, 8 px apart … NOT the 8x12" bakes pixels and a
  sprite size into prose (compare `HudLayout.HudSpareLifeWidthPixels`).
- Palette prose: `src/Robotron2084/Palette/PaletteAnimator.cs:17,20,26,29,33,34` and
  `Palette/HighScorePalette.cs:14,21,32` quote interval counts ("every 8 fiftieths", "one colour every 2…")
  that live as numbers in the tables/constants beside them.
- `src/Robotron2084/Hud/HighScorePageHold.cs:12,26` — "600 frames (12 s)" in prose next to the code's own count.
- `src/Robotron2084/Hud/ScoreFormatter.cs:13` and `Hud/ArcadeHud.cs:137` — "6 px / 7 px" advances belong to the
  named constants.
**Fix:** replace each number with the constant/`<see cref>` name. Where the number exists *only* in prose, make
it a constant first (NUM-1), then reference it.

### M-17 — [Major] CMT-3 — history in code comments
- `src/Robotron2084/Audio/Synthesis/FilteredNoise.cs:33` — "It is used to shift `_limit`, both to set its
  starting value and to read its…" — describes readers/users of the field (CMT-15 forbids this outright, and
  "is used to" trips CMT-3's grep). Rewrite as purpose, not usage. (Same phrasing pattern to clear in the fix
  pass: `Entities/IEntity.cs:21`, `Entities/Prog.cs:474`, `Level/GruntSpeedProgression.cs:99` — "is used to"
  sentences about members. The `Prog` "used to be" lines (12, 204, 218, 263) are the domain fact that a prog
  *was* a family member and are **fine** — do not "fix" those.)

### M-18 — [Major] SOLID-O / FID — `InstructionCycles` and ROM constants duplicating the clock? (verify)
`src/Robotron2084/Audio/Synthesis/InstructionCycles.cs:55,73,82` — `= 6` values matched the NUM-2 scan but read
as 6809 *cycles*, not clock units. Triage result: **not** NUM-2. Kept here only as a verification row while the
NUM-1 sweep runs: confirm each cycle constant cites its ROM instruction (CMT-14 style docs).

---

## 3. Minor

### m-01 — [Minor] NAM-13 — value-returning methods not `Get…`/`Create…`/`To…`/`Parse` (non-audio group)
Each violates "every method that returns something other than a bool starts with Get…".
- `AttractMode/AttractObjectMachine.cs:65` `DrainExplosions()`, `:145` `Read`, `:150` `ReadSignedWord`, `:155` `ReadWord`
- `AttractMode/AttractPageMachine.cs:115` `DecodeCharacter`, `:155` `NextByte`, `:160` `NextWord`
- `AttractMode/MovieAnimationFrames.cs:40` `Resolve`; `AttractMode/MovieDescriptors.cs:49` `Resolve`; `Level/WaveTable.cs:348` `ResolveWave`
- `Audio/SoundBoardRenderer.cs:65` `AverageLevelOverOneSample`, `:89` `RemoveSteadyLevel`
- `Core/ArcadeRandom.cs:25` `PickUpTo` (Pick banned), `Core/IntVector2.cs:29` `ComputeDistanceSquared`, `Core/ScreenSize.cs:77` `ComputeMaxIntegerScale` (Compute banned)
- `Graphics/Presentation.cs:25` `CanvasDestination` (noun), `:50` `NextScaleMode` (Next banned)
- `Graphics/TunnelEffect.cs:200` `NextPair`
- `Graphics/WordmarkAppear.cs:89` `FindLetters`; `Level/Collisions/RobotVsElectrodeCollisionRule.cs:36` `FindTouchedElectrode`; `Level/Spawning/HulkWaveSpawner.cs:81` `FindNextLeftOverSlot`; `Level/SpawnPlacement.cs:49,65,88,101,109` `FindSpawnPoint*`/`PickRandomPointInside`/`PickRandomSpheroidCandidate` (Find/Pick banned)
- `Hud/HighScoreTableLayout.cs:146` `TodayPosition` (noun), `Hud/InitialsEntryModel.cs:268` `NextLetter`, `Hud/ScoreFormatter.cs:35` `DrawnDigits` (noun), `:74` `LayOutGlyphs`
- `Hud/SettingsModel.cs:153` `Activate` (bare verb, NAM-1), `Hud/DefineInputsModel.cs:77` `PlayerOf`, `:171` `NextLine`
- `Input/ActionBinding.cs:42` `With(...)` (returns a new binding — builder idiom; confirm with author), `Input/DemoPlayerInputSource.cs:90` `ChooseDirection`, `:112/129/150` `SteerClear*`, `:173` `HoldDirection`, `Input/GamePadSticks.cs:18` `Read`, `Input/InputSnapshot.cs:20` `Read`, `Input/ControlSettings.cs:85` `ReadPlayer`
- `Level/BozoMode.cs:75` `Apply`, `Level/DifficultyTuning.cs:192` `Apply`, `:234` `Adjust` (return values)
- `Level/PlayField.cs:427` `SpawnTank`, `Level/Spawning/MidWaveSpawner.cs:110` `SpawnTank` (make-new must be `Create…`, NAM-13)
- `Level/RobotTransporter.cs:81` `AssignImages`, `Level/ScoreValues.cs:90` `RescueBonus` (noun), `Level/WaveParameterGenerator.cs:50` `Generate`, `:73` `LoadTable`, `:103` `TryParseRow` (see M-07)
- `Level/WaveSurvivors.cs:67` `CountFamilyMembers`, `:76` `CountSpheroidsAfterConverting` — private LINQ-wrapper duplicates of `Count(…)` (NAM-13 "do not write what LINQ already does"; `CountFamilyMembers` may keep its name only if the domain meaning is not the bare count — verify)
- `Persistence/ControlSettingsStore.cs:35,125` / `GameSettingsStore.cs:34,116` / `HighScoreStore.cs:23,65` `Load` (returns values; confirm author intent — `Save`/`Write`/`Parse` are the store's verbs already blessed by usage), `Persistence/HighScoreTable.cs:172` `Submit`, `:204` `CountInitials`, `:223` `FindLowestInitialsIndex`, `:256` `PadToCapacity`, `:283` `InsertAllTime`
- `States/AttractState.cs:115` / `States/PlayingState.cs:241` `BuildField`, `States/TitleScreenState.cs:269` `ChooseMode`, `States/ScoreEntryCeremony.cs:35` `NextScreen`, `:53` `Submit`, `States/PlayingState.cs:210` `StartSoundFor`, `:233` `ApplyBozoMode`, `States/SoundTestState.cs:171` `ToDisplay` (To… conversion — acceptable form; name reads as a string→string map, verify it is not a property-shape issue)
- `Graphics/TextureFactory.cs:43-191` `Build*Pattern` family + `:230` `ScalePattern` (return arrays; `Create…` per NAM-13)
- `Graphics/ArcadeText.cs:197,208,213` `MeasureLargeText`/`MeasureSmallText`/`MeasureText` (return int — confirm whether Measure joins the blessed verb list with the author, else `GetMeasured…`)
- `Level/TransportImage.cs:98` `Step(Random random)` is a command — fine; `States/DeveloperToolsState.cs:172` `Move` covered by M-08.

### m-02 — [Minor] NAM-13 — audio-synthesis value-naming cluster (see N-03 first)
`IEnumerable<OutputChange>`-returning `Play*`/`Start*`/`Run*`/`Step*`/`Sweep`/`Sing`/`Store`/`Pass`/`Complement`/
`Change`/`Write*`/`Arm*`/`End*`/`Increment*`/`SlideToTarget`/`ChooseSlopeHigh`/`CountVoices`/`CountSend`/
`CyclesBeforeEcho`/`MixTones`/`FromWords`/`StartOf`/`SearchPattern`/`DrainExplosions`-equivalents across
`Audio/Synthesis/` — full line list in this ledger's scan appendix section below (M-08's list overlaps). Files:
`BoardOutput.cs:16,27,46,63`, `CountdownLoop.cs:18,26`, `CrowdRoarSound.cs:241,312,337,459`,
`ElectricSound.cs:87,123`, `FilteredNoise.cs:128,138,148,161,166,177,187,232`, `HyperSound.cs:42`,
`KnockerSound.cs:74`, `LightningNoise.cs:76,89,99,112`, `OscillatorSound.cs:215,224,233,242,254,262,267,373,426`,
`PlaneSound.cs:43`, `RadioSound.cs:59`, `RandomStep.cs:29`, `ScreamSound.cs:87,145`, `SingSound.cs:149,158,167,176,185,194,226,249,257,277,303,361,375`,
`SirenSound.cs:100,105,121,146`, `SoundBoard.cs:164,276,288,402,415,435,446,477`, `SpinnerSound.cs:54,73`,
`SquareWaveSound.cs:106,162,182`, `WaveTableData.cs:161`, `WaveTableSound.cs:234,248,261,429,441,455,477,490,508,523`,
`WhistleSound.cs:71`, `WhiteNoise.cs:64,89`.
**Hold until N-03's author decision** — many mirror ROM routine labels (`PLAY1`, `GWLD`, `MWAVE`…).

### m-03 — [Minor] NAM-4 — bool methods not starting `Is`/`Has`/`Was` (non-CQS survivors)
`Was…` forms are blessed by NAM-19's own example (`WasPressed`) — keep. Flag the rest:
- `Can…` banned outright: `Level/PlayField.cs:194,201,208,220,229` (`CanDropEnforcer`, `CanDropTank`,
  `CanFireCruiseMissile`, `CanFireShell`, `CanPlayerBeHurt`) and `Audio/Synthesis/SoundBoard.cs:156`
  (`CanPlay`). NAM-23's form is `Has…`/`Is…` + the command.
- `Overlaps` banned by name: `Graphics/SpriteCollision.cs:25`, `Graphics/SpriteMask.cs:75` → `IsOverlapping`.
- `Level/PlayfieldWall.cs:69` `Intersects` → `IsIntersecting`/`Hits` (author to pick the word).
- `Persistence/HighScoreTable.cs:144,150,156` `Qualifies`/`QualifiesForAllTime`/`QualifiesForToday`,
  `:198` `Beats`, `:241` `Insert` (also command+bool, M-08), `:270` `EnforceMaxAllTimeInitials`,
  `:311` `TopCarriesInitials` → questions (`IsQualifying…`, `IsHigherThan…`).
- `Level/BozoMode.cs:62` `AppliesTo(int wave, …)` → `IsAppliedTo` or move decision to caller.
- `Level/Collisions/BoxContactTest.cs:9`, `PixelContactTest.cs:34`, `Level/PlayField.cs:450,457` — `Touches`/
  `TouchesPlayer`: reads as a question but is not `Is`/`Has`; the interface `IContactTest.Touches` shares it —
  rename together with author's word choice.
- `Hud/HighScorePageHold.cs:62`, `Hud/InitialsEntryModel.cs:162`, `Core/ArcadeFrameTimer.cs:129` — `Tick`
  returning bool is sanctioned by TIME-1's own idiom; **not** flagged. `Input/ButtonPresses.cs:10`
  `AnyPressed` and `Level/FieldEntities.cs:249,266` / `FamilyList.cs:58` / `GameSession.cs:149` `Any…` bools —
  the standard's own examples bless the `Any…` form (`AnyPlayerSlotHasLives`); **not** flagged, listed here so
  the fixer does not "correct" them.

### m-04 — [Minor] NAM-12 — American spelling in prose
`src/Robotron2084/Core/Direction8Extensions.cs:42` — "NOT normalized" → "normalised". (The `behavior` grep hits
are all under `obj/` — generated, ignore.)

### m-05 — [Minor] NAM-19 — one name, several types
- `_explosions`: `AttractMode/AttractObjectMachine.cs:41` (`List<MovieExplosion>`) vs
  `States/StorylineState.cs:36` (`List<StripEffect>`).
- `_letters`: `Graphics/WordmarkAppear.cs:42` (`IReadOnlyList<LetterSpan>`) vs `Hud/InitialsEntryModel.cs:106`
  (`char[]`).
- `_pixels`: `Graphics/WilliamsLogoBorder.cs:77` (`byte[]`) vs `Level/TransportImage.cs:48` (`int[]`).
- `_processes`: `AttractMode/AttractObjectMachine.cs:43` (`List<MovieProcess>`) vs
  `Palette/HighScorePalette.cs:136` and `Palette/PaletteAnimator.cs:108` (`Process[]`).

### m-06 — [Minor] NAM-21 — Offset/Delta names without both ends
Grouped by file; each needs the "from" in the name or, when it would run too long, spelled out in its summary:
- `AttractMode/AttractObjectMachine.cs:172,176,180` local `columnOffset`.
- `AttractMode/MovieObject.cs:74` `ShakeRowOffset` (offset OF the row FROM the shake? say so) + uses at 86, 612, 615, 622.
- `Audio/Synthesis/SoundBoard.cs:76,97,127` `HighWaveTableOffset`, `LowWaveTableOffset`, `SquareWaveOffset`
  (offsets OF sound numbers FROM the table base).
- `Audio/Synthesis/WaveTableSound.cs:214` `_pitchOffset` (+252,361,397,423,515).
- `Entities/Brain.cs:123` `VictimRightOffsetColumns`; `:665-666` local `targetDeltaX`.
- `Entities/Hulk.cs:178,190` `TargetOffsetMaxExclusive`, `TargetOffsetMin` — the standard's example criticises
  exactly this shape (`AimOffsetMaxExclusive…` vs `AimOffsetFromTargetMaxExclusiveArcadePixels`).
- `Entities/Player.cs:325` `GetMuzzleOffset` (muzzle offset from `_position`).
- `Entities/Prog.cs:75,78,81,93,96,105,117` `OffsetXHalfRange`, `OffsetXRandomMax`, `OffsetXStepColumns`,
  `OffsetYCentre`, `OffsetYStepRows`, `OffsetYSteps`, `ReOffsetThreshold256`, fields `_offsetX`/`_offsetY`
  (530-534).
- `Entities/Quark.cs:325` local `rowOffset`.
- `Entities/Spheroid.cs:47,65` `AccelXOffset`, `AccelYOffset`.
- `Tuning/TankTuning.cs:51` `GrowDeltas` (+ use `Entities/Tank.cs:491`).
- `Hud/HighScoreTableLayout.cs:42,88` `AllTimeScoreOffsetColumns`, `TodayScoreOffsetColumns` (from which edge?).
- `Hud/InitialsEntryLayout.cs:44` `MarkerRowOffset`.
- `Palette/TunnelPalette.cs:195` `BlackOffset`.
- `Tuning/CollisionSizes.cs:7,10` `CruiseMissileBoxOffsetColumns/Rows`.
- `Tuning/ScoreBurstTuning.cs:8,10` `PointsOffsetXArcadePixels`, `PointsOffsetYArcadePixels`.
- `Tuning/TankShellTuning.cs:143` `StartOffsetColumns`.
Accepted by the rule (their words already name the second end): `TankTuning.BirthOffsetRowsOffTopWall`,
`BirthOffsetRowsOffTopWall`, `Hulk` local `offsetFromTarget` (`Hulk.cs:520`).

### m-07 — [Minor] NAM-26 — shortened object names (no IntelliSense rule)
- Parameters named `input` for a `PlayerInputState`: `Hud/InitialsEntryModel.cs:162,172,206,281,295,317,353`
  → `playerInputState`.
- Pattern-match locals named `palette` for a `GamePalette`: `States/DefineInputsState.cs:110,129`,
  `States/DeveloperToolsState.cs:123,143`, `States/HighScoreTableState.cs:92,371`,
  `States/SoundTestState.cs:122` → `gamePalette`.

### m-08 — [Minor] CMT-10 — untracked "not built yet" text
- `src/Robotron2084/Level/GameMode.cs:28` and `src/Robotron2084/States/PlayingState.cs:84` — "not built yet"
  without a tracking-item reference (notes §/handoff row). Name the tracking item or delete.

### m-09 — [Minor] TIME-1a — timer field names off the standard's table
Table says `_growTimer`, `_lifeTimer`, `_animationTimer` for those three ideas; the codebase appended
`ArcadeFrameTimer`:
- `_growArcadeFrameTimer`: `Entities/Enforcer.cs:68`, `Entities/Tank.cs:115`
- `_lifeArcadeFrameTimer`: `Entities/RescueBonusMarker.cs:36`, `Entities/SkullMarker.cs:33`, `Entities/Spark.cs:35`
- `_animationArcadeFrameTimer`: `Entities/Gorf.cs:45`
Decide once (author): amend the table to the `…ArcadeFrameTimer` form (then beat/move already match) or rename
the six fields. Concepts outside the table (`_shrivelArcadeFrameTimer` `Electrode.cs:33`,
`_deathArcadeFrameTimer` `Player.cs:55`, `_reprogramArcadeFrameTimer` `Brain.cs:226`,
`_stepArcadeFrameTimer` `ScoreBurst.cs:35`/`StripEffect.cs:72`) need rows added to TIME-1a (NAM-9: one idea, one
name — two different classes both hold a `_stepArcadeFrameTimer` doing different things; check they are the
same idea).
Also `Core/ArcadeFrameTimer.cs:56` — the class's example code says `_beatTimer`, the old name (TIME-1a says
`_beatArcadeFrameTimer`).

### m-10 — [Minor] CON-1 / CON-2 — copy-pasted helpers and duplicated constants across the state pages
Private near-identical helpers (also carry M-08's `DrawText` smell):
- `DrawText`: `States/DefineInputsState.cs:217`, `States/DeveloperToolsState.cs:251`, `States/SettingsState.cs:198`.
- `WasKeyboardPressed`/`WasPadPressed`: `DefineInputsState.cs:315,320`, `DeveloperToolsState.cs:277,282`,
  `SettingsState.cs:224,229`; `WasPressed(InputSnapshot, Keys, Buttons)`: `DeveloperToolsState.cs:289`,
  `SettingsState.cs:236`.
- The measure-large/small + text-right block (`DefineInputsState.cs:262-263`, `DeveloperToolsState.cs:259-260`,
  `SettingsState.cs:206-207`, `TitleScreenState.cs:332-333`).
Duplicated constants (same name, same value, several files — CON-2 says one constant):
`InputPaletteIndex = 6` (`DefineInputsState.cs:61`, `SettingsState.cs:51`), `HeadingPaletteIndex = 9`,
`CursorColumn = 57`, `LabelColumn = 75`, `LineStep = 26`, `InstructionsRow = 294`, `SecondInstructionRow = 312`,
`ExitRow = 342`, `LivePollArcadeFrames = 2`, `HighBit = 0x80`, `BeatIntervalArcadeFrames` ×3 (legitimate —
per-entity beat, *verify* the identical values are coincidence-free), `ByteBits = 8`, `ChannelsPerFrame = 2`,
`DefaultMaxDropsX2 = 10`, `DefaultMoveIntervalMaxBeats = 15`, `NewDestinationBeatsMaxExclusive = 32`.
Get the owning file for each with `grep -rn 'const int <NAME>' src`; page-layout ones belong in a shared
`Hud`/`States` layout class.

### m-11 — [Minor] CMT-13 / NAM vocabulary — "picture" and "image" for character drawings
- `Graphics/LetterSpan.cs:3,4` — "picture of a word", "the picture's own pixels".
- `Graphics/WordmarkAppear.cs:25,52,73` — "the same picture", "Where the whole picture is drawn".
- `Level/RobotTransporter.cs:16,23,31,43,46,66,72,78,127,142,148,159,162,163,182,183` — "image" used for an
  animation frame throughout.
- `AttractMode/AttractObjectMachine.cs:263,329,455` — "(descriptor, image)", "frames per image".
- `AttractMode/MovieAnimationFrames.cs:26,27` — "ROM image 2 is the same sprite".
ROM data-layout prose `(image x4, dx pixels, dy pixels)` at `AttractMovieData.cs:196`, `MovieWalk.cs:6` quotes
the ROM's table shape — rewrite the words around the quote to "animation frame", keeping `HUMANA` etc.
`MovieDescriptors.cs:5` "read out of the R5 image" means the ROM *dump* — fine, not flagged.

### m-12 — [Minor] NAM-13 (LINQ-shadow check) — `FieldEntities.cs:370` `entity.Position.GetManhattanDistance(...)`
Extension-method call; confirm `GetManhattanDistanceTo(...)` reads as the question NAM-13 wants (the `other`
argument is missing a `…To` in the name — NAM-21's "relative term" version).

---

## 4. Notes (author decisions / verification before fixing)

### N-01 — [Note] NUM-2 triage: clock-literal scan hits that are NOT the clock
`MessageHeight`, `LaserDiagonalSize`, `EnforcerAnimationFrameCount`, `MovesPerArcadeFrame`, palette indices
`= 6`, `TextPaletteIndex`, `PlayerAppearNapArcadeFrames = 6`, `LaserFlashRandomArcadeFrames = 6`,
`TitleArcadeTextSeconds`, Gorf counts, `HudLayout` `6`s, `PlayerDeathColourArcadeFrames`,
`BirthOffsetRowsOnTopWall` — all named constants meaning something other than 5 clock-units/6 clock-units.
No finding. `+= 5;` — no hits. If any of these is later re-derived from `ArcadeClock`, that is an improvement
(NUM-6 derive-don't-restate), not a fix.

### N-02 — [Note] SOLID-D static state — known, to shrink, not grow
`Audio/Sound.cs:31,37` and `Input/DeveloperKeys.cs:49` are the standard's own named "existing ones". Left as
findings-of-record only; no action unless the author opens the shrink.

### N-03 — [Note] Audio emulation verbs (plays into m-02, M-08)
Most `Audio/Synthesis` method names mirror ROM labels (`PLAY1`, `PLAY2`, `GWLD`, `SHRLIM`, `MVPTCH`, `NPLAY`…).
NAM-13 grants no ROM exemption, so strictly they are findings; but renaming would break the ROM cross-reference
mapping. **Ask the author**: exempt the emulation layer (write the exemption into the standard) or rename with
the ROM label kept in the summary. Do not churn 100+ names before this answer.

### N-04 — [Note] Attract movie opcode switch cases
`AttractMode/AttractObjectMachine.cs` — 28 raw `case <n>:` labels (lines 268-… through ~500) each carry a
`// SETOB`-style ROM comment. NUM-1 wants named constants; FID-5 wants the ROM's shape. Author to say whether
named opcode constants are wanted (`private const int SetObjectOpcode = 1; // SETOB`).

### N-05 — [Note] `DifficultyTuning.cs:241` — `(product >> 8) + ((product >> 7) & 1)`
ROM fixed-point rounding emulation (FID-2/FID-5). Confirm the ROM instruction is quoted nearby (CMT-1) and
leave the shift, or lift into a named `RoundShiftProductForRom(...)` helper (NUM-4). Verify the comment cites
the ROM's `ASLA` sequence.

### N-06 — [Note] `Input/ActionBinding.cs:42` `With(...)` and `Input/BoundPlayerInputSource.cs:22,29` /
`DemoPlayerInputSource.cs:60` `Poll()` — builder/LINQ-style or interface-shaped names. `Poll` exists on
`IPlayerInputSource`; NAM-13 would want `GetInputState()` — rename interface and implementations together if
the author agrees.

### N-07 — [Note] Rules not covered by mechanical scans — needs a by-hand pass
These need reading each file top-to-bottom (Part 2 step 4) and were **not** audited this round:
STR-5/STR-5a member order, STR-9/STR-10 (state comparisons and tell-don't-ask outside the hits above),
STR-8 feature envy, CMT-1/CMT-2 comment-necessity and restatement, CMT-8 `<summary>` quality on every
`Update`/`Draw` and enum member, CMT-12/15/16/17 doc-ownership drift, CMT-11 plain-English summaries and
label verification, FID-1/2/3 spot checks, NUM-5/NUM-6, STR-6 registry duplication (`RobotKinds` — grep for
hand-written kind lists came back clean; re-verify during the sweep), FMT-1 formatting details, PROC rules.
Suggested order: entities one family at a time (Grunt → Hulk → …), comparing siblings per CON-1.

---

## 5. Explicitly NOT findings (do not "fix" these)
- `WasPressed`-form bool methods — NAM-19's example blesses them.
- `random` parameter names — NAM-26 lists them as fine.
- `_isShowingArcadeText` (`TitleScreenState.cs:138`) — NAM-27's stated exemption.
- `Tick(interval)` returning bool — TIME-1's sanctioned idiom.
- `Enum.TryParse`, `int.TryParse`, `Dictionary.TryGetValue` call sites — framework names (NAM-12/ownership).
- Audio big-endian byte packing shifts (`KnockerSound.cs:79`, `OscillatorSound.cs:264`, `SirenSound.cs:87,169`)
  — byte order, not unit conversion (NUM-4's target is unit conversion).
- `Prog`'s "the family member it used to be" — domain fact, not code history (see M-17).
- ROM tables/byte arrays with a summary giving the ROM address (`WaveTable`, `AttractMovieData`) — exempt data.
- Gorf/Berzerk labelling — both correctly declare "not in the arcade" (FID-4a satisfied:
  `Gorf.cs:18`, `BerzerkRobot` checked).
- Deliberate deviations marked "do not revert without asking" — none found missing their marking in the
  scanned files.

---

## Summary
- **Blocker:** 2 (both sides of the invincibility default: `PlayField.cs:109`; `PlayerTuning.cs:83` +
  `Player.cs:137`).
- **Major:** 18 entries (several are groups of many call sites; the largest are STR-11's ~120 call sites,
  STR-1a's 9 files, STR-3's 14 files, TIME-3's 9 sites).
- **Minor:** 12 group entries.
- **Notes:** 7 (author decisions + the honest gap list N-07).
- Most important: **B-01/B-02** — the shipped game currently runs with the player invincible (constructor
  default plus the global `PlayerTuning` flag); fix them together. Next: M-01 (false test-hook label on a member the wave-clear palette logic depends on) and M-05
  (gameplay randomness not repeatable — undermines FID-3 style tests).
- Open author questions: N-03 (audio verbs), N-04 (opcode constants), N-06 (`Poll`), plus the `Any…`/`Can…`
  word choice in m-03 and the timer-name direction in m-09.
