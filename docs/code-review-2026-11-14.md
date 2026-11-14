# Code review — 14 November 2026

Scope, procedure and the scans behind every number here: `docs/code-review-2026-11-14-plan.md`. The whole of
`src/Robotron2084` (338 files) was scanned; the deep read (§4 of the plan) covered the files with the most commits
since 1 November. Baseline: Debug and Release build clean, 948 tests passed, 103 skipped, 0 failed.

**Totals: 0 new Blockers, 14 Majors, 16 Minors, 6 Notes.** One Blocker is carried over and still open (N-05).

---

## Majors

```
[Major] CMT-4 Level/PlayerSlot.cs:18 — 74 XML errors in the doc comments of 15 files, and the build cannot see one
of them.
Why: the `<list type="bullet">` in these class remarks is closed (`PlayerSlot.cs:17`), then a plain paragraph is left
outside it (`:18-21`) ending in a stray `</item>`, then a second `</list>` (`:22`) before `</remarks>`. The site below
is typical; fifteen files have it. `Robotron2084.csproj` never sets `GenerateDocumentationFile`, so the compiler
never parses a doc comment — and because `Directory.Build.props` sets `TreatWarningsAsErrors=true`, switching doc
generation on today turns all 74 into build errors. Every one of these files therefore ships with remarks nobody has
ever read.
Fix: delete the trailing `</item></list>` and fold the loose paragraph into the list (or close the list before it) at
each site below, then add `<GenerateDocumentationFile>true</GenerateDocumentationFile>` to the project so this cannot
happen again. Sites (CS1570, 36 places): Level/BozoMode.cs:20, Level/GruntSpeedProgression.cs:29 and :286 and :321,
Level/LaserWallFlare.cs:16, Level/PlayerAppear.cs:23, Level/PlayerSlot.cs:21, Level/RobotKindInfo.cs:22,
Level/ScoreValues.cs:14, Level/WaveMaterialisation.cs:39, :235, :407, Level/WaveParameterGenerator.cs:23,
Level/WaveParameters.cs:25, Level/WaveStartSequence.cs:27, Level/WaveSurvivors.cs:19, Level/WaveTable.cs:23,
Level/RobotTransporter.cs, Audio/Synthesis/CrowdRoarSound.cs:337 (a `<summary>` nested inside a `<summary>`).
```

```
[Major] CMT-11 Tuning/ScreenTuning.cs:13 — 86 doc blocks cite the original arcade source and never cite the
disassembly.
Why: CMT-11 asks for both sources, on separate bullets, always. The arcade line tells a reader where the number came
from; the disassembly line tells them which bytes in `asm/robomame.asm` to believe. Without the second, a value can
only be checked by a person who already knows it. Half of the Tuning folder is like this.
Fix: add the `<item>Disassembly: <c>LABEL</c> (<c>$XXXX</c>)</item>` bullet to each site. Full list:
`perl tools/review-scans/cite-audit.pl $(git ls-files 'src/Robotron2084/*.cs' | grep -v obj) | grep '^A:'`.
Breakdown: Tuning 51 (ScreenTuning.cs 11, TankTuning.cs 3, QuarkTuning.cs 3, SpheroidTuning.cs 3, ProgTuning.cs 3,
TransporterTuning.cs 3, BrainTuning.cs 3, the rest single), Level 20, Level/Spawning 7, Level/Collisions 5,
Persistence 2, States 1.
```

```
[Major] CMT-11b Level/WaveParameters.cs:3 — 35 doc blocks make 73 citations of disassembly labels that do not exist
in asm/robomame.asm.
Why: the remarks claim "Disassembly: <c>ENFSHT</c>", "<c>RTORG</c>", "<c>TRANSPORTER</c>", "<c>ANATAB</c>", "<c>AMAP</c>"
and 64 other labels; none appears in the disassembly — `grep -ic '\bRTORG\b' asm/robomame.asm` is 0 for every one of
them, and `RTORG` alone is cited in seven blocks. A reader who goes looking learns that the citation is invented, and from then on distrusts every honest
citation in the file. CMT-11b says a label must be the one actually in the disassembly and must never be guessed.
Fix: for each of the 33 sites (list: `… | grep '^C:'`), either replace the label with the label the disassembly
really has at that address, or drop to the bare address. The likely source of the invented labels is the
reference table in `docs/coding-standards.md` and the opcode mnemonics in `docs/glossary.md`, which hold original
source names under a heading that reads like disassembly — fix those two tables too or the wrong labels will come
back. The 35 sites: Level/WaveParameters.cs:3, Level/WaveMaterialisation.cs:9 :223 :339, Level/RobotTransporter.cs:9
:31 :49, Level/PlayerSlot.cs:6, Level/PlayerAppear.cs:101, Level/RobotKindInfo.cs:8, Level/GruntSpeedProgression.cs:6,
Level/LaserWallFlares.cs:9, Level/WaveStartSequence.cs:5, Level/TransportImage.cs:6,
AttractMode/AttractObjectMachine.cs:37 :225, AttractMode/MovieObject.cs:101, AttractMode/MovieExplosion.cs:3,
AttractMode/MovieWalk.cs:3, Graphics/LargeTitleLetterAppear.cs:8, Palette/HighScorePalette.cs:5 :154,
Palette/PaletteAnimator.cs:5, Palette/PresentationPagePalette.cs:5 :156, Palette/TunnelPalette.cs:3 :87,
Tuning/CruiseMissileTuning.cs:3, Tuning/ProgTuning.cs:3 :25, Tuning/QuarkTuning.cs:3, Tuning/ReprogramTuning.cs:3,
Tuning/SparkTuning.cs:3, Tuning/TransporterTuning.cs:3, States/SoundTestState.cs:13.
```

```
[Major] CMT-4 Entities/PlayerLaser.cs:70 — the ROM range of the four player laser sprites is quoted two ways in four
files, and neither end is right.
Why: `PlayerLaser.cs:70` and `Tuning/PlayerTuning.cs:32` both say the four sprites run from `$35C1` to `$35DC`, which
starts at the *second* sprite; `Graphics/TextureSet.cs:12` and `:256` and `Graphics/TextureFactory.cs:13`, `:107` say
`$35BE-$35DC`. Both stop at `$35DC`, ten bytes short of the end. The disassembly settles it: the four metadata
entries at `$35AE`, `$35B2`, `$35B6`, `$35BA` (asm/robomame.asm:5480) read `03 01 35 BE`, `01 06 35 C1`,
`03 06 35 C7`, `03 06 35 D9` — 3 + 6 + 18 + 18 bytes, one after another, so the pixel data runs `$35BE` to `$35EA`.
`TextureFactory.cs:99` and `:112` get the two starts right (`$35BE` 3×1, `$35C1` 1×6), which shows the metadata was
read once and then copied wrongly. The comments are how the ROM mapping is recorded, so a wrong address is not a
typo — it is a false record.
Fix: write "pixel data `$35BE`–`$35EA`, metadata `$35AE`–`$35BD`" in all six places, each saying which of the two it
means.
```

```
[Major] CMT-4 Level/FieldEntities.cs:272 — AreEnemiesGone() counts Gorf and Berzerk robots as enemies, and the
remarks just above say the arcade counts only six other kinds.
Why: the remarks say "the arcade only counts the grunts, spheroids, enforcers, brains, tanks and quarks, and clears
away anything else when the wave ends", citing `RRG23.ASM WVCHEK` and `COUNT_ENEMIES_ON_SCREEN` (`$2A73`). The
statement below them, `Level/FieldEntities.cs:274`, is
`IEntityList[] enemyLists = [Grunts, BerzerkRobots, Gorfs, Spheroids, Enforcers, Quarks, Tanks, Brains];`. Gorf and
the Berzerk robot are not in the arcade at all, so counting them is a port deviation — and an unmarked one. A wave
cannot end while a Gorf is alive, which changes when a Gorf wave ends.
Fix: either drop the two author robots from the list, or add a bullet saying plainly that the port also waits for
Gorf and Berzerk robots and that the arcade does not — and mark it "do not revert without asking", which is the
house rule for a deliberate deviation.
```

```
[Major] NAM-20 src — 322 publicly visible members have no `<summary>` at all.
Why: the compiler says so (CS1591, 322 occurrences, plan §3.1). It is not spread evenly, which is what makes it a
finding rather than a preference: Hud/HighScoreTableLayout.cs 58, Graphics/TextureSet.cs 34,
AttractMode/MovieDescriptors.cs 32, AttractMode/MovieAnimation.cs 32, Hud/InitialsEntryLayout.cs 20,
Input/InputBinding.cs 16, Core/Direction8.cs 16, States/GameStateManager.cs 10, RobotronGame.cs 10,
AttractMode/MovieWalk.cs 8. In `TextureSet` the neighbours disagree with each other: `MikeyAnimationFrameTextures`,
`BrainAnimationFrameTextures`, `TankGrowAnimationFrameTextures` and `SpareLifeTexture` are documented, while
`PlayerAnimationFrameTextures`, `GruntAnimationFrameTextures`, `HulkAnimationFrameTextures`,
`SpheroidAnimationFrameTextures`, `EnforcerTexture`, `ProgBurstTexture`, `LaserColumnTexture`,
`LaserDiagonalMainTexture` and `LaserDiagonalAntiTexture` are bare — and one summary (`LaserBarTexture`, :256) explains
all four laser sprites while three of the four properties it describes say nothing.
Fix: work through the CS1591 list per file, one commit per folder (PROC-4). Start with `TextureSet`, whose missing
summaries are what makes the four laser properties unreadable.
```

```
[Major] CMT-4 Level/SpawnPlacement.cs:76 — doc comments still describe parameters that no longer exist, and say
nothing about the ones that do.
Why: 22 more doc faults the compiler names exactly. `SpawnPlacement` (lines 76, 98, 107) documents a `point`
parameter that became `position` and `avoidedPosition`; `Level/PlayfieldWall.cs:34` still documents `cycle`, renamed
`wallColourCycle`; `Graphics/WilliamsLogoBorder.cs:87` still documents `logo`, renamed `logoSpriteMask`;
`Entities/BerzerkRobot.cs:173` documents a `textureSet` that is gone. `States/SpeechTestState.cs:81` has the same
`<param name="gameServices">` twice. `RobotronGame.cs:374` carries `<paramref name="key" />` in
`WasPadPressed(Buttons)` — it was pasted from the keyboard method beside it. Three `<see cref>` targets resolve to
nothing: `Audio/Synthesis/FilteredNoiseSettings.cs:5` (`ByteBits`), `:6` (`_limit`),
`Graphics/BlitterDraw.cs:60` (`Overlap`). Each of these is a comment that has already drifted from its code, which
is the defect this codebase produces most.
Fix: repair the 22 as named, then let the compiler keep them repaired by enabling doc generation (first finding
above).
```

```
[Major] NAM-4 Level/PlayField.cs:407 — bool-returning methods that do not read as questions, and eight read-only bool
properties that do not either.
Why: the scan in plan §3.4 finds 33 `bool` methods that do not start with `Is` or `Has`. `TryParse`
(`Input/InputBinding.cs:132`), `TryFirePlayerLaser` and the `Try…` parsing helpers are left to the decision in the
Notes below, which leaves about thirty. NAM-4 says a bool method starts with `Is` or `Has` and names `Can…`, `Try…`,
`Fits…` and `Overlaps…` as
banned. The `Can…` family alone is five in the play field: `PlayField.cs:216 CanDropEnforcer`, `:223 CanDropTank`,
`:230 CanFireCruiseMissile`, `:242 CanFireShell`, `:251 CanPlayerBeHurt`, plus
`Audio/Synthesis/SoundBoard.cs:156 CanPlay`. Two
`Overlaps` (Graphics/SpriteCollision.cs:25, Graphics/SpriteMask.cs:75) are named by the rule too — the cref ledger
has an open decision on `SpriteMask.Overlaps`, so settle it and rename in the same pass. Then `RobotsFrozen`,
`HitsWall`, `Touches` (×3), `Intersects`, `AppliesTo`, `AnyPressed`, `AnyAvailable`, `AnyFamilyMemberAvailable` (×2),
`AnyPlayerSlotHasLives`, `AreEnemiesGone`. On the property side, a bool with a private setter is a predicate by the
same rule and the standard calls `LasersFiredThisUpdate` a bad example by name, so `Entities/Grunt.cs:108
SteppedThisUpdate`, `Entities/BerzerkRobot.cs:99 SteppedThisUpdate`, `Entities/Player.cs:119 FiredLaserThisUpdate`,
`Entities/TankShell.cs:94 BouncedThisUpdate`, `Audio/Synthesis/BoardMemory.cs:51 RandomBitOut`,
`Hud/HighScorePrintSequence.cs:52 HeadersPrinted`, `:58 TopPrinted`, `Palette/HighScorePalette.cs:152 RampsStarted`
all need `Is` or `Has`. The ten settable `Is…` bools stay as they are — the author decided that (plan §5).
Fix: rename mechanically — `CanFireShell` → `HasShellAmmo` or `IsShellAllowed`, `RobotsFrozen` → `AreRobotsFrozen`
if the standard accepts `Are…`, otherwise `IsRobotHoldActive` — and record every rename in
`docs/refactoring-ledger.md` (PROC-5). Decide `Are…` with the author before starting; 33 renames is not a place to
guess.
```

```
[Major] STR Core/ArcadeFrameTimer.cs:129 — six methods change the world and answer a question at the same time.
Why: the standard is explicit — "a step that both changes something and answers a question is split into a query and
a command". `Core/ArcadeFrameTimer.cs:129 Tick(int)` advances the timer and tells you whether it elapsed;
`Hud/HighScorePageHold.cs:62 Tick(bool)` and `Hud/InitialsEntryModel.cs:154 Tick(PlayerInputState)` likewise;
`Hud/DefineInputsModel.cs:93 Assign(…)` commits a binding and answers whether it took;
`Level/ScoreBoard.cs:39 Add(int)` adds score and answers whether a spare life was earned;
`Level/GameSession.cs:193 SwitchToPlayerWithLives()` switches player and answers whether anyone had lives. Callers
must remember which of the two effects they wanted, and `if (timer.Tick(5))` reads like a test.
Fix: split each into a command plus a query (`Advance(…)` and `IsElapsed`; `Add(points)` and `EarnsSpareLife`), or
make the answer explicit in the name (`TickAndReportElapsed`) and log the choice in the refactoring ledger.
`ScoreBoard.Add` is the one worth splitting first: earning a life is a game event, not a return value.
```

```
[Major] NUM-2 Entities/Gorf.cs:196 — a unit word that is not a unit, divided by a magic number.
Why: `Math.Max(1, _dropIntervalSteps * GorfTuning.ReturnDropIntervalFifths / 5)` — "Fifths" is not a unit of anything
(NAM-2 allows only real units), the `5` is a bare literal, and the doc at `Tuning/GorfTuning.cs:28` explains the
constant by pointing at itself: "the steps between drops are this many fifths of what they were
(`ReturnDropIntervalFifths` out of 5)". A reader cannot tell whether 4 means four fifths or four units of five.
Fix: `ReturnDropIntervalPercent = 80` and `_totalSteps * ReturnDropIntervalPercent / 100`, with the doc saying that
a killed Gorf's remaining grunts come back sooner, at 80 percent of the normal gap. Log it in the refactoring
ledger (PROC-5).
```

```
[Major] STR-2a Graphics/TextureSet.cs:111 — the constructor is 59 non-blank lines, and it owns frame counts that
four entity classes also own.
Why: it is the only method in `src` over the 50-line cap. Worse, six calls pass the animation frame count inline —
`GetNumberedNames("Sprites/Gorf", 2)` at :115 and the five Berzerk robot sets at :116-120 — while every line around
them names its count (`PlayerAnimationFrameCount` :113, `GruntAnimationFrameCount` :114, `HulkAnimationFrameCount`
:121, `WalkCycleAnimationFrameCount` :143-146, and the consts at :25-86). `Entities/BerzerkRobot.cs:47`
(`SidewaysWalkFrameCount = 2`) holds the same 2 as the walk calls at :117 and :118, so one fact about a sprite sheet
has two owners and the six bare numbers are a third.
Fix: name the six (`GorfAnimationFrameCount`, `BerzerkRobotIdleAnimationFrameCount`, and four walk counts), and take
them from the entity that animates rather than restating them — or make the entity ask `TextureSet` for its own
count and let the texture set be the single owner (STR-6). Split the constructor along its existing seams (sprites,
font, tile wall, attract) into private `Load…` methods.
```

```
[Major] CMT-4 Graphics/TextureSet.cs:89 — the summary for FontSuffixes contradicts itself in one sentence pair, and
its neighbour contradicts it again.
Why: it opens "Font glyph file-name suffixes in ROM order (notes §38, §96)" and ends "The punctuation is APPENDED
so every existing index (and every caller's expectation of it) is unchanged" — appended at the end is not ROM order,
because the same sentence says the ROM carries the punctuation between `9` and `A`. `CursorArrowTexture` at :223 then
says "the ROM's table order and the port's differ", which contradicts the first claim. A reader maintaining glyph
indices has to guess which sentence to trust; the honest answer is the second one.
Fix: say what it is — "the ROM's order for `0`–`9` and `A`–`Z`, with the eight punctuation glyphs appended so
existing indices do not move" — and give the ROM order in one short table so the difference is visible without
opening the disassembly.
```

```
[Major] DEAD Level/WaveParameterGenerator.cs:62 — a hand-made wave table cannot set the number of drops: two of its
columns are written and never read.
Why: the custom path sets `MaxEnforcersPerSpheroid` and `MaxTanksPerQuark` from the CSV (:62-63) and never sets
`MaxDropsX2`, which stays at the record default of 10 (`Level/WaveParameters.cs:39`). Everything that actually
decides drop counts reads `MaxDropsX2` — `Level/Spawning/SpheroidWaveSpawner.cs:23`,
`Level/Spawning/QuarkWaveSpawner.cs:20`, `Level/Spawning/MidWaveSpawner.cs:95`, and the same field in `Spheroid`,
`Quark` and `Gorf`. So both columns of `CustomWaveTable.csv` do nothing at all, every custom wave drops 10, and the
test that loads a five-row CSV passes either way because it asserts into the two dead fields.
Fix: pass `MaxDropsX2: customWaveTableRow.MaxEnforcersPerSpheroid * 2 - 1` — or better, give the CSV one column for
one fact and delete the other two (next finding). Rewrite the CSV test to assert on `MaxDropsX2`, since that is what
reaches a spheroid.
```

```
[Major] SOLID Level/WaveParameters.cs:28 — one record of 26 bare ints, one fact stored three times, and a 25-argument
positional call that fills it.
Why: `MaxDropsX2`, `MaxEnforcersPerSpheroid` and `MaxTanksPerQuark` are the same number in three shapes:
`Level/WaveParameters.cs:77-79` writes `waveTableRow.MaxDropsX2`, `(MaxDropsX2 + 1) / 2`, `(MaxDropsX2 + 1) / 2`, and
`Level/DifficultyTuning.cs:207-209` does it again — that is where the dead CSV columns in the finding above were
able to hide. The remarks for the record admit it: they are "kept in step with `MaxDropsX2`, for anything that still
reads them", which is not English a person can act on and does not name what reads them (nothing does — that is the
finding above). Separately, the record takes 26 positional parameters, every one an `int`, and `CreateFromWave`
fills it with 25 positional arguments in a row (:60-85): swap two adjacent numbers and the build stays green while
GruntSpeedFloor becomes GunTimerBonus. The custom path already uses named arguments; the arcade path does not.
Fix: delete `MaxEnforcersPerSpheroid` and `MaxTanksPerQuark` and have the two spawners compute `(MaxDropsX2 + 1) / 2`
once, behind one named method on the record. Then give `CreateFromWave` named arguments for every parameter, or group
the 26 into the four or five things they describe (robot counts, timings, speed, drops, bonus). Log the deletions in
`docs/refactoring-ledger.md` (PROC-5).
```

---

## Minors

```
[Minor] CMT-11b AttractMode/MovieObject.cs:49 — 25 `Disassembly:` bullets give a label or a description and no
address. Sites: AttractMode/MovieObject.cs:5, :49, :58, :111, :123; AttractMode/AttractObjectMachine.cs:49, :58,
:154; AttractMode/AttractPageMachine.cs:207; Entities/Brain.cs:290; Graphics/LargeTitleLetterAppear.cs:8;
Level/GameSession.cs:39, :70; Level/PlayField.cs:394, :734; Level/PlayerSlot.cs:42, :106; Level/RobotKindInfo.cs:8;
Level/Spawning/TankWaveSpawner.cs:6; Level/WaveMaterialisation.cs:44, :53, :66; Level/WaveStartSequence.cs:169;
Level/WaveSurvivors.cs:5.
Why: CMT-11b — where there is no label, cite the address; never guess one. A label without an address cannot be
checked after a rename, which is the only reason the label is there.
Fix: add the address to each, or replace the bullet with "Port only" where the arcade has no such thing.
```

```
[Minor] CMT-4 Level/RobotKindInfo.cs:18 — the Disassembly bullet spends its breath saying it has no address.
Why: the bullet reads "Disassembly: no address in the disassembly: this record is the port's own gathering of them",
which repeats itself and is not a sentence a reader can act on. CMT-11b has the right words already: where the arcade
has no such thing, say "Port only".
Fix: "Port only: the arcade has no single place that gathers what each kind does".
```

```
[Minor] CMT-4 Level/PlayerSlot.cs:29 — the record's `<param name="Lives">` text is repeated word for word as the
summary of the `Lives` property at `:58`, and again for `Wave` at `:30` and `:40`.
Why: two copies of one sentence, both needing upkeep, one of them guaranteed to fall behind.
Fix: document the parameter once (the property summary points at it with `<inheritdoc />`, or the property drops its
own summary).
```

```
[Minor] DRY Entities/Gorf.cs:331 — GetDropColumn() and DropGrunts() recompute the same four geometry values.
Why: grunt width, grunt height, the row count and the spacing are worked out twice, from the same texture, so a
change to one place moves the drop one pixel and the check for another — the exact shape of bug that made the drop
column wrong for two rows (notes §149.1).
Fix: have DropGrunts call GetDropColumn, or lift both into one private `GetDropLayout()` that returns the column and
the body height together.
```

```
[Minor] Correctness Entities/Gorf.cs:114 — a Gorf owed more grunts than it has steps left drops them all at once.
Why: the constructor says `if (_dropIntervalSteps <= 0 && gruntDropCount > 0) _dropIntervalSteps = _totalSteps / (gruntDropCount + 1);`
and when the owed count reaches `_totalSteps` that quotient is 0, so `PlanGradualDrops` (`:285`) enqueues
`drop * 0` for every stop and the whole payload lands on the first beat instead of spreading along the hop. The
author already knew the shape: `Kill()` at `:196` wraps the same arithmetic in `Math.Max(1, …)`.
Fix: `_dropIntervalSteps = Math.Max(1, _totalSteps / (gruntDropCount + 1));` and add the boundary test (owed equal to
remaining steps).
```

```
[Minor] Correctness Entities/Gorf.cs:191 — Kill() before the first Update() loses the grunts it owes.
Why: `Kill()` (`:185`) opens with `if (_playField is null || …) return;` and `_playField` is only set in `Update()`, so
a Gorf that dies on the tick it spawns schedules nothing and its unpaid grunts vanish, against its own summary ("a
new Gorf comes on a little later to drop them … so a wave that is owed a number of grunts still gets them all").
Fix: take the play field in the constructor the way `Prog` does, or have the spawner owe the grunts rather than the
Gorf.
```

```
[Minor] STR-1 Entities/Gorf.cs:59 — `_dropIntervalSteps` sits below the mutable fields.
Why: the readonly block is `_spawnPosition`, `_totalSteps`, `_gruntDropCount`, `_beatIntervalSteps`, then four
mutable fields, then a fifth readonly one. The declaration order is how a reader separates what changes from what
does not.
Fix: move it up with the other readonly fields.
```

```
[Minor] CMT-4 Tuning/GorfTuning.cs:11 — "Gorf's two animation frames" states a number the texture set owns.
Why: the count is `GetNumberedNames("Sprites/Gorf", 2)` (Graphics/TextureSet.cs:115), `GorfTuning.cs:11` says "Gorf's
two animation frames" and `Gorf.cs:129` says "the two animation frames … 0 or 1". The code is right
(`ShowNextAnimationFrame` at `:365` steps modulo the array length), so it is only the prose that will go stale — but
three sentences for a number the array already carries is three to forget.
Fix: say "each of Gorf's animation frames" and "the frame that is showing" and let the array length carry the number.
```

```
[Minor] NAM-26 Input/InputBinding.cs:91 — the local `pad` holds a GamePadState.
Why: the standard names `pad` as the example of an abbreviation to avoid (line 335: "the same goes for locals: `pad`
is a…"), and the same method's other locals spell the type out. Three siblings make the same file read two ways:
`Input/ControlCapture.cs:48` and `:54` and `Input/PlayerControls.cs:42` use `pad` as a gamepad index, which NAM-2
wants as `gamePadIndex`.
Fix: `gamePadState` at :91, `gamePadIndex` at the three counters. While in `InputBinding`: `GetDirectionX` (:68) and
`GetDirectionY` (:75) have no `<summary>` (they are in the CS1591 list).
```

```
[Minor] CMT-2 Graphics/TextureSet.cs:22 — "(notes 32)" without the section sign.
Why: every other notes reference in the codebase is `notes §NN`; this is the only one that is not.
Fix: `notes §32`.
```

```
[Minor] CON-1 src — 83 doc comment lines mark code with markdown backticks instead of `<c>`.
Why: the house idiom is `<c>`, and a backtick renders literally in a tooltip, so the reader sees `ENFSHT` with
fences. Worst files: Persistence/HighScoreTable.cs 26 lines, Graphics/TunnelEffect.cs 15, States/TitleScreenState.cs
9, Graphics/TextureSet.cs 8, States/HighScoreTableState.cs 8, Persistence/SubmitResult.cs 5.
Fix: `grep -rnE '^\s*///.*`' --include=*.cs src` and convert per file.
```

```
[Minor] CON-1 Graphics/TextureSet.cs:241 — ROM numbers written both ways in one file.
Why: `@0xEC93` and `@0xEA2B` in a file that otherwise writes `$86BA`, `$8461`, `$35BE`; also
Level/TransportImage.cs:120 (`<c>0xF0</c>`) and Tuning/TransporterTuning.cs:37, :39. The disassembly and the
standard use `$`.
Fix: `$EC93`, `$EA2B`, `$F0`, `$0F`.
```

```
[Minor] ERR Level/WaveParameterGenerator.cs:93 — a wave table that cannot be read for reasons other than I/O takes
the game down.
Why: the summary at `Level/WaveParameterGenerator.cs:23` promises "A missing or damaged file is ignored", and only
`IOException` is caught, so an `UnauthorizedAccessException` (a CSV with the wrong permissions, which is exactly how
this feature gets used — a hand-edited file copied in from elsewhere) escapes and crashes on the first wave.
`DirectoryNotFoundException` is an `IOException`, so it is already covered; the access denial is not.
Fix: catch `IOException` and `UnauthorizedAccessException`, or `Exception` narrowed by a comment saying why breadth
is right here: a bad optional file must never stop a game.
```

```
[Minor] SOLID-D Audio/Sound.cs:31 — four process-wide bool switches, set from outside by whoever remembers.
Why: `Sound.Enabled`, `Sound.AttractMuted` (:31, :37), `Speech.AttractMuted` (Audio/Speech.cs:29) and
`DeveloperKeys.AttractFastForward` (Input/DeveloperKeys.cs:53) are static mutable state that any object can write
and any object reads. The standard asks behaviour to live with the data it needs (SOLID-D); the attract mute in
particular is a rule about attract mode, not about the sound player.
Fix: DECIDE with the author — either pass a mute/settings object into the attract and sound services, or keep the
statics and record the decision in the refactoring ledger so the next reviewer stops asking.
```

```
[Minor] CON-2 Level/PlayField.cs:570 — one question, three names, three levels.
Why: `PlayField.AnyFamilyMemberAvailable()` forwards to `FieldEntities.AnyFamilyMemberAvailable()`
(Level/FieldEntities.cs:255) which forwards to `FamilyList.AnyAvailable()` (Level/FamilyList.cs:56). Three files to
read for one `Any`, and two of them exist only to forward.
Fix: keep the owner (`FamilyList`) and the façade the entities need; drop the middle forwarder or give it a
different question.
```

```
[Minor] STR-1 AttractMode/MovieProcess.cs:57 — seven classes divide themselves with banner comments.
Why: `// ---- … ----` dividers are named in coding-standards.md:57 as the warning sign of a class doing several jobs,
and these seven files are the ones with them: AttractMode/MovieProcess.cs:57, Graphics/TextureFactory.cs:107 and
:174, Hud/HighScoreTableLayout.cs:54, Hud/InitialsEntryLayout.cs:22, :46, :53. `HighScoreTableLayout` and
`InitialsEntryLayout` are also the two files with the most undocumented public members (58 and 20 of the CS1591
list), which is the same symptom twice over: the file has grown several jobs and marks them with comments instead of
splitting.
Fix: when each of those files is next opened, split at the divider rather than tidying it (PROC-1) — a layout class
per job, or one class holding the geometry and one drawing it.
```

---

## Notes

```
[Note] CMT-11 Audio/** — the sound board citations in this folder cannot be checked in this repository.
Why: `Audio/Synthesis/*` cites `VSNDRM3.SRC` and addresses in the sound ROM (`$F5xx`–`$FBxx`). Neither the source
file nor the sound ROM is here; `asm/robomame.asm` covers only the main CPU ROM. Every audit run has to be told to
skip this folder, which means the next reviewer will either waste an hour or guess.
Fix: say so once in `docs/coding-standards.md` next to the CMT-11 table ("sound board citations name
`VSNDRM3.SRC`; that source is held outside the repository") and list the sound ROM the labels came from.
```

```
[Note] CMT-1 Level/CustomWaveTableRow.cs:1 — the summary says the wave number is discarded but not what that costs.
Why: `Level/WaveParameterGenerator.cs:54` picks a row with `table[(waveNumber - 1) % table.Length]`, so the CSV's
Wave column is checked for being positive and then thrown away. A CSV with rows out of order, or with a gap,
silently gives wave 3 the robots of wave 2. The class summary says the number "is not kept"; it does not say rows
must therefore be consecutive and in order.
Fix: one sentence in the summary — "rows are used in file order: keep them numbered from 1 with no gaps" — or use
the wave number the file already carries.
```

```
[Note] STR-6 Level/WaveTable.cs:165 — the ROM byte tables are public arrays, so any code can write to them.
Why: `public static readonly int[] MaxDropsX2` and its 31 siblings are the ROM's own values, and the reference is
readonly but the contents are not. Nothing writes them today. The tables themselves are exempt from the magic-number
rule (data with a ROM address in its summary), so this is about protection, not naming.
Fix: DECIDE with the author — expose them as `IReadOnlyList<int>` behind the existing accessors, or leave them and
note that ROM data is deliberately open.
```

```
[Note] PROC tools/** — the repository's own checkers cannot run in this environment.
Why: `tools/lint-conventions.ps1`, `tools/verify-fonts.py`, `tools/check-crefs.py` and `tools/playfield_compare.py`
need `pwsh` and `python`; this machine has neither, only `dotnet` and Git Bash. Their checks had to be rewritten as
bash, `grep` and `perl` for this review (plan §3).
Fix: state the required runner at the top of each script or in the README, and keep the bash equivalents in
`tools/review-scans/` alongside the two perl scripts added with this review.
```

```
[Note] Carried over and re-verified, not new.
Why: the 1 November blockers are still true at this commit — `Tuning/PlayerTuning.cs:137` still returns true for
`PlayerInvincibleForTesting`, `Level/PlayField.cs:114` still defaults `playerInvincibleForTesting` to true, and
`States/PlayingState.cs:251` still does not pass the setting while `States/AttractState.cs:124` passes false, so the
shipped game plays with the player unkillable (B-01, B-02). `Level/PlayField.cs` is still 964 lines against the
500-line cap (STR-1a, M-02) and `Graphics/TunnelEffect.cs` still carries seven test-hook properties read in
production (M-01). M-04, M-05, M-10, M-11, M-12, M-14 and m-05 from that ledger are fixed.
Fix: nothing here that the 1 November ledger does not already say; the list is recorded so nobody re-opens them as
newspaper findings.
```

```
[Note] DECIDE — two naming rules need an author ruling before a mechanical pass.
Why: NAM-4's letter bans `Overlaps` and `Try…`, but `TryParse` is the .NET idiom (Input/InputBinding.cs:132) and the
cref ledger has `SpriteMask.Overlaps` half-decided; and `AreEnemiesGone`, `AreRobotsFrozen` and friends read better
with `Are…`, which NAM-4 does not list at all.
Fix: the author answers two questions — is `TryParse` exempt as a framework idiom, and is `Are…` allowed for a bool
method about several things — and the answers go in the decision table so the next reviewer applies them instead of
arguing.
```

---

## Summary

Nothing new blocks a release, but the release is still blocked by the carried-over B-01/B-02 invincibility default,
which remains open for the fourteenth day. The 14 Majors divide into two groups with one cause each. The first is
that the compiler never parses a doc comment — `GenerateDocumentationFile` is off while `TreatWarningsAsErrors` is
on — so 74 malformed blocks, 322 undocumented public members, 22 wrong `<param>` and `<see cref>` entries and a
pasted-in `<paramref name="key">` have accumulated quietly in fifteen files; the fix is one property in the project
file plus a pass through the CS1591 list, one commit per folder. The second is that citations have been copied
between documents rather than checked: 35 blocks name disassembly labels that `asm/robomame.asm` does not contain,
86 cite the arcade without the disassembly at all, the four laser sprites are given two wrong ROM ranges across four
files (the true pixel data runs `$35BE`–`$35EA`), and `AreEnemiesGone()` counts two robots its own comments exclude.
The most important single finding is not a comment: the custom wave table's two drop-count columns are written into
fields that nothing reads, so a hand-made wave always drops 10, and the test written to prove those columns work
asserts into the dead fields. Open questions for the author are the `TryParse` and `Are…` rulings, whether the four
static audio switches stay static, and whether the ROM byte tables should be read-only.
