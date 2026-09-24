# Code review: issues to fix (2026-09-24)

This is a work list for an AI agent. Each item says **where**, **what is wrong**, **what to do** and
**how you know it is done**. Read the rules in section 0 before touching anything. The standards that
these issues are measured against are in [coding-standards.md](coding-standards.md).

---

## 0. Rules for whoever fixes these

1. **Do not change behaviour** unless the item is in section A and says so. Everything else is a
   rename, a move, a comment change or a deletion.
2. **Never fix an item in section J.** Those need a decision from the author first.
3. **One item per commit.** Put the item ID in the commit message, e.g. `B07: fix stale spheroid burst remark`.
4. **After every item, run all three and make sure they pass:**
   ```
   dotnet build Robotron2084.slnx -c Debug
   dotnet build Robotron2084.slnx -c Release
   dotnet test Robotron2084.slnx
   ```
   0 warnings (warnings are errors), 0 failed tests. If a test fails, undo your change and report it.
   Do not edit a test to make it pass unless the item tells you to.
5. **Do not suppress anything**: no `#pragma warning disable`, no `[SuppressMessage]`, no `NoWarn`.
   A method over the cyclomatic complexity budget (15) must be split instead.
6. **One top-level type per file.** A new class, record, enum or struct goes in its own file.
7. **Keep every ROM cross-reference** (`ROM: RRC11.ASM ENFSHT`, `R5 $4F8C`, `notes §52`) when you rewrite a
   comment. Those are the one kind of comment this codebase always wants.
8. **Do not edit** `docs/arcade-fidelity-notes.md`, `ledger.md`, `status.md`, `plan.md` or the handoff files.
   The author keeps those.
9. **Line numbers are from commit `1ce3b0f` plus the working tree on 2026-09-24.** Earlier fixes will shift them.
   Find code by the quoted text or the member name, not by the line number alone.
10. After a rename, search the **whole solution, including `tests/`**, for the old name, and run
    `git status` to check for untracked leftover files (notes §121 found a stale file this way).
11. If an item is unclear, or the fix turns out bigger than described, **stop and report**. Do not guess.

### Suggested order

C (dead code), then B (comments), then K (tooling), then D (numbers and units), G (naming),
H (consistency), E (duplication), I (tests), then F (structure). Section A items go on their own
commits with a test first. Section J is for the author.

---

## A. Bugs and behaviour risks (write a failing test first, then fix, then flag the commit for the author)

**A01. Tank tread animation advances twice per beat, and also while frozen or being born.**
`src/Robotron2084/Entities/Tank.cs:117` and `:192` both do `_animationTicks++`. Line 117 runs on every
tick, including frozen ticks and ticks during the birth animation. Line 192 runs once per beat, and its
comment says "one walk frame per beat (ROM: TANK3 advances the picture once)".
*Fix:* write a test showing the tread frame changes on ticks where no beat happens. Then delete line 117
and rename `_animationTicks` to `_treadFrameCounter`, because it counts beats, not ticks.
*Done when:* the new test passes and the tread frame only changes on a beat. Say in the commit message
that this changes what the tank looks like on screen.

**A02. `TankShell.BouncedThisUpdate` keeps its old value on ticks where the shell does not move.**
`src/Robotron2084/Entities/TankShell.cs:89`: `BouncedThisUpdate = false;` sits inside the
`if (_moveTimer >= 6)` block. On the tick with no move (1 tick in 6), a bounce from the previous tick is
reported again, and `PlayField.Update` (`PlayField.cs:420-426`) asks for the bounce sound a second time.
*Fix:* move `BouncedThisUpdate = false;` to the top of `Update`, before the early returns.
*Done when:* a test shows the flag is false on the tick after a bounce when that tick does not move.

**A03. The spheroid's two escape exits use different origins.**
`src/Robotron2084/Entities/Spheroid.cs:177-178`: `leftExit` adds `bounds.X` but `rightExit` does not:
```csharp
int leftExit = bounds.X + ScreenSize.Scaled(2 * GameplayConstants.SpheroidEscapeExitLeftColumn);
int rightExit = ScreenSize.Scaled(2 * GameplayConstants.SpheroidEscapeExitRightColumn);
```
One of them is wrong. **Do not fix.** Write a test that pins the current right-exit X, and add the
question to your report for the author. (Also covered by D05 for the bare `2`.)

**A04. The laser-wall flare dither uses 2-row bands, but the comment says 1 row.**
`src/Robotron2084/Level/PlayField.cs:1060`: `int arcadeRow = ScreenSize.Scaled(2); // one arcade pixel row = 2 port px`.
`ScreenSize.Scaled(2)` is **4** port px. Either the comment or the code is wrong. **Do not change the
code.** Report it to the author. Once the author answers, fix whichever one is wrong and name the
constant (see D03).

**A05. The START 1 / START 2 buttons always read pad 1, for both players.**
`src/Robotron2084/Input/ControlSettings.cs:76-77` reads `padOne` for START 1 and START 2 regardless of
`playerIndex`, but reads fire from `own` pad. This may be intended (the cabinet has one START panel).
**Do not fix.** Report it as a question.

---

## B. Misleading, stale or wrong comments (comment-only changes; zero behaviour risk)

**B01.** `Tuning/GameplayConstants.cs:9-10`: the class summary says values are "original placeholders unless
marked spec-stated". Most values are now ROM-derived. *Fix:* rewrite it to say what the class holds (tuning
and ROM-derived constants), and that each constant's own summary gives its source.

**B02.** `Tuning/GameplayConstants.cs:302`: `// Flash: plan 8.5 says ... -> uses SpheroidFlash* constants.`
Those constants were deleted (see line 279-282). *Fix:* delete the line.

**B03.** `Tuning/GameplayConstants.cs:716-722`: `MissileMarkArcadeWidth` has **two** `<summary>` blocks, and
the first ("= 1 x 2") contradicts the value 2. *Fix:* delete the first summary (line 716).

**B04.** `Entities/CruiseMissile.cs:20`, `:28` and `:209` say the mark is "1 arcade px wide and 2 tall" /
"1x2px". The constants are 2x2 (`MissileMarkArcadeWidth = 2`, `MissileMarkArcadeHeight = 2`). *Fix:* say 2x2,
and say it by naming the constants rather than the numbers.

**B05.** `Level/PlayField.cs:650-651` (`ResolvePlayerVsContactKills` summary): "it expires on its own
lifetime". A cruise missile has no lifetime (`CruiseMissile.cs:17-18`: "It has no lifetime of its own").
*Fix:* "a missile is not removed on contact; only a laser removes it."

**B06.** `Entities/Enforcer.cs:14`: "five pictures of 9 frames (40 in all)". 5 x 9 = 45, which is what
`EnforcerGrowUpRomFrames` holds. *Fix:* name the constant instead of the number.

**B07.** `Entities/Spheroid.cs:19-20` and `:106-107`, and `Entities/Quark.cs:93-94`, say the bespoke death
burst "is still to be built / not built yet, so the strip explosion stands in". It **is** built:
`ScoreBurst`, wired in `RobotKinds.cs:55-71`. *Fix:* say that a laser hit plays `ScoreBurst` (see `RobotKinds`).

**B08.** `Rendering/TunnelEffect.cs:27-28` says the effect takes "about 0.9 s". `PassFifths`'s own doc
(`:63-76`) says ~2.2 s, and that is what the code does. *Fix:* delete the 0.9 s sentence and point at `PassFifths`.

**B09.** `Rendering/TunnelEffect.cs:151` (`Update` summary): "One ROM task pass = one frame". A pass is two
ROM frames (`PassFifths = 2 * 6`). *Fix:* correct it.

**B10.** `Input/DevKeys.cs:28` and `:37` say "while F3 is held". The key is **F7** (`RobotronGame.cs:152`).
*Fix:* F7. Better: do not name the key at all (see coding standard CMT-4). Say "while the fast-forward key
is held (see `RobotronGame.HandleAttractDevKeys`)".

**B11.** `Input/PlayerInputState.cs:12`, `:15` and `:31`: "gamepad right stick deferred", "the port's test key (P
...)" and "aim stick is deferred". The right stick is bound (`PlayerControls.Defaults`), and the skip key is
Insert (`ControlSettings.cs:75`). *Fix:* remove the stale text. Describe the fields without naming keys.

**B12.** `Persistence/ControlSettingsStore.cs:13-20`: the example file shows `moveup.pad=P1-LS-UP` and
`[pause] key=P`. `Write` produces `input=` for pause (`:153`) and display names with spaces. *Fix:* make the
example match what `Write` produces. A good way is to copy the output of `Write(ControlSettings.Defaults())`.

**B13.** `Core/SpriteMask.cs:74`: `<see cref="Rendering.SpriteSet.ArtRect"/>`. The method is now `DrawnRect`.
*Fix:* update the cref.

**B14.** Stale `IArtSource` crefs (the interface is now `IAnimationFrameSource`): `Entities/Tank.cs:263`,
`Entities/Quark.cs:294`, `Entities/Spheroid.cs:313`, `Entities/Brain.cs:317`. *Fix:* update them.

**B15. 33 stale `<param name="sprites">` tags** left by the §121 constructor change, in 20 files:
`Brain, CruiseMissile, Electrode, Enforcer, Grunt, Hulk, Human, LaserSlots, Player, PlayerLaser, Prog, Quark,
RescueScoreMarker, ScoreBurst, SkullMarker, Spark, Spheroid, Tank, TankShell` (all in `Entities/`) and
`States/GameOverState.cs`. On `Draw(SpriteBatch)` methods and on `CurrentAnimationFrame` **properties**, where
there is no `sprites` parameter, delete the tag. On constructors that **do** take `sprites`, keep it, and add it
where it is missing (e.g. `Spark`'s constructor, `Grunt`'s constructor).
*Done when:* K01 is in place and the build shows no CS1572/CS1573.

**B16.** Duplicate `<summary>` blocks: `Entities/Enforcer.cs:201-202`, `Entities/Electrode.cs:97-98`.
*Fix:* keep one.

**B17.** `Level/PlayField.cs:913` and `:915` are the same section header twice. *Fix:* delete one.

**B18.** `Level/PlayField.cs:1042-1044`: the Draw section header says "explosions/human family are deferred
per spec's 'to be added later'". Both are drawn. *Fix:* delete that clause.

**B19.** `Level/PlayField.cs:1283-1284`: the header "PHASE D human spawning" sits above `SpawnBrains`, not above
`SpawnHumans` (`:1303`). *Fix:* move it above `SpawnHumans` and drop "PHASE D" (see B24).

**B20.** `Level/PlayField.cs:1018`: "`NAP 2` = 2 ROM frames = 12 sixths (notes §52).;" is on a property named
`FifthsRemaining`, and ends in a stray `;`. *Fix:* once H01 is done, the property counts clock units. Say so,
and remove the `;`.

**B21.** `Entities/Human.cs:261`: `TeleportTo` is documented as a "Test-only positioning hook", but
`Brain.BeginReprogramming` and `Brain.AdvanceReprogramming` call it in production (`Brain.cs`, the two
`TeleportTo` calls). *Fix:* rename it `MoveTo` and document it as the brain's hold on its victim (ROM `BMUT`).

**B22.** `Entities/Brain.cs:92`: the `Kill` remark says "A brain killed mid-reprogram releases its victim". `Kill`
does not; `PlayField.ReleaseVictimsOfDeadBrains` does. *Fix:* say so and cref the method.

**B23.** `Entities/Player.cs:294`: "Current frame index ... (frame 1..12)". The value is a **0-based** index,
0..11. *Fix:* "0-based index into `SpriteSet.PlayerFrames`; arcade frame N is index N-1."

**B24. Plan phases and milestone codes in comments** ("Phase 11.3", "PHASE D", "PHASE E", "plan 9.1", "M4",
"M5", "round 6"). There are 87 of these in `src/`. They point at documents a reader of the code does not have
open, and they go stale. *Fix:* replace each with what it means ("wave-clear check", "human collisions",
"colour-cycle shader") or delete it. Keep any ROM label or `notes §NN` next to it.
Find them with: `grep -rnE "M4\b|M5\b|round [0-9]|Phase [0-9]|PHASE [A-Z]|plan [0-9]" src --include=*.cs`

**B25. History and author quotes in comments** (39 hits): "the author's report", "playtest 2026-09-13",
"(author, 2026-09-16)", "The old guard cancelled...", "Returning 'keep reading' made ...", "That is what made the
prog's ghost frames black", "the black-ghost-card bug". Comments must say what the code does and why. The story
of how it got there belongs in git history and the notes (standard CMT-3).
*Fix:* rewrite each to state the current rule only. Examples:
- `Level/Attract/AttractObjectMachine.cs:403-411`: replace the paragraph with "PSHAKE is a one-byte script: RPROG
  owns the process until it ends, so return false (stop reading)."
- `Level/Attract/AttractObjectMachine.cs:466-471` and `:478-483`: same treatment.
- `Rendering/SpriteSet.cs:436-443` and `:725-727`: keep the device-state explanation, drop the bug story.
- `Tuning/GameplayConstants.cs:139-145, 259-263, 290-296, 318-320, 339-348, 603-607, 612-613, 765-766`.
- `Audio/Sound.cs:16-29`: keep "off by default; set `ROBOTRON2084_SOUND=1` to enable", drop the quote.
Find them with: `grep -rnE "author's report|the author|playtest 20|Playtest 20|\(author, 20|used to|The old|was wrong|bug\)" src --include=*.cs`

**B26. "Tombstone" comments about code that does not exist.** `GameplayConstants.cs:151-152, 253-254, 278-282,
298-301, 334-336, 346-348`: "(No GruntDyingBlinkTicks: ...)", "(No SpheroidFlash*: ...)" and so on. *Fix:*
delete them. The fact they record ("no enemy blinks on death") belongs on the entity's `Kill` summary, which
already says it.

**B27. Comments that restate values the code already holds, and will go stale** (standard CMT-4):
- `Entities/PlayerLaser.cs:12`: "flies at 12 px/tick". Name `GameplayConstants.LaserSpeed` instead.
- `Entities/Spark.cs:96`: "(25 points)". Delete it; the score lives in `ScoreValues`.
- `Entities/Player.cs:15-18`: "2/3 px a tick", "(WASD)", "(IJKL)". Name the constants; do not name keys,
  because they are rebindable.
- `Level/PlayField.cs:1327`: "within Scaled(30)". Name `SpheroidNearWallBiasDistance`.
- `Rendering/SpriteSet.cs:18-22`: the frame-count list duplicates the `NumberedNames(..., n)` calls. Delete the
  list. D08 turns the counts into named constants.

**B28.** `Core/IntVector2.cs:27`: "Hands off to `Rectangle`'s all-int constructor". The method returns a `Point`.
*Fix:* "Converts to MonoGame's `Point`."

**B29.** `Level/RobotKinds.cs:16-20` says "Nothing else needs an edit" to add a kind, and
`Level/EntityList.cs:14` says "a field here". Adding a kind also needs a field and a `ListOf` arm in `PlayField`,
entries in `_updateOrder` / `_drawOrder*`, and a line in `NearestLivingRobotPositionTo` (until E02). *Fix:* list
the real steps, or do E02 and F01 first so the claim becomes true.

**B30.** `Level/PlayfieldWall.cs:9`: "The colour-cycling 4px (spec) border". In the real game the colour comes
from the wave's palette slot (`PlayField.Draw`, `:1052-1053`), and `WallColorCycle` only runs in tests. *Fix:* say
the colour is supplied by the caller and fall back to `WallColorCycle` only when none is (see J07).

**B31.** `Level/Attract/MovieObject.cs:7` crefs `MovieProcess`, a **private nested** class of
`AttractObjectMachine`. *Fix:* reword to "the process that drives it lives in `AttractObjectMachine`".

**B32.** `Level/LevelParameters.cs:4-19` and `Level/LevelParameterGenerator.cs:4-17` describe milestones
("Since M5 (2026-09-12)") and a CSV override that is never used (see C06). Rewrite them after C06.

**B33.** `Entities/TankShell.cs:82`: "X is bounced before Y (see the remarks)". The remarks do not mention it.
*Fix:* add one sentence to the class remarks explaining that only one axis bounces per frame (X first), or
drop "(see the remarks)".

---

## C. Dead code (delete it; zero behaviour risk)

**C01.** `Entities/WallFleeHelper.cs`: the whole class is unused (no references in `src/` or `tests/`). Delete
the file.

**C02.** Unused constants in `Tuning/GameplayConstants.cs` (no reference anywhere in `src/` or `tests/`):
`GruntCountMin/Max, HulkCountMin/Max, SpheroidCountMin/Max, QuarkCountMin/Max, ElectrodeCountMin/Max,
MaxEnforcersPerSpheroidMin/Max, MaxTanksPerQuarkMin/Max, DifficultyGrowthCapLevels` (`:61-75`), `SpriteFrameTicks`
(`:83`), `GruntSpeed` (`:252`), `SpheroidDropCooldownMinTicks/MaxTicks` (`:283-284`), `EnforcerSpeed` (`:297`,
with its comment block `:289-296`), `TitleBlinkIntervalSeconds`, `GameOverMinPauseSeconds`,
`TitleHighScoreCycleSeconds` (`:515-517`), `TitleLineOneRow`, `TitleLineTwoRowOffset` (`:523-524`).
Before each deletion, grep the whole solution for the name to confirm.

**C03.** `Entities/ScoreBurst.cs:27` and `:54`: the field `_count` is written and never read. Delete it. Keep
the constructor parameter, which is still used for `_remaining`.

**C04.** Unused `speedBonus` constructor parameters, documented as "Unused (kept for the uniform spawn shape)":
`Grunt` (`Grunt.cs:47` + ctor), `Enforcer` (`Enforcer.cs:43` + ctor), `Tank` (`Tank.cs:62` + ctor). Delete the
parameter and its doc tag, and fix the callers (tests use named arguments in places).

**C05.** `Level/LevelParameterGenerator.cs`: the `random` constructor parameter is "kept for API compatibility"
and never used (`:34`). Remove it and the `(Random random)` overload, and update callers and tests.

**C06.** `LevelParameters` fields nothing in gameplay reads: `MaxEnforcersPerSpheroid`, `MaxTanksPerQuark`
(`LevelParameters.cs:33-34`, "legacy ... for anything still reading them"; nothing does), `ShellSpeed`,
`EnemySpeedBonus`, plus the `LevelTable.csv` override in `LevelParameterGenerator` (no such file exists in the
repo). **This is J01**: ask the author before deleting the CSV feature. The two "legacy" fields can go now.

**C07.** `Level/RobotKindInfo.cs:24`: `WaveCount` is read only by a test
(`PlayFieldRobotRegistryTests.OnlyTheWaveBroughtKindsHaveAWaveCount...`). Production spawning reads
`Parameters.XCount` inside each `Spawn` delegate, so the column duplicates information. **J02**: either use
it in the spawners or delete it. Ask the author.

**C08.** `Entities/Spark.cs:159`: `MoveBy(field, 0, 0);` on ticks with no move step does nothing: it clamps
zero and re-assigns the same position. Delete the call. Then `return` at `:156` is also unneeded.

**C09.** `Entities/Spark.cs:28` / `:54`: `_stepScale` is a per-instance copy of the constant
`GameplayConstants.SparkVelocityScale`. Use the constant directly and delete the field.

**C10.** `Level/WaveTable.cs:172-183` duplicates `ResolveWave` (`:212-221`). Make `ForWave` call
`ResolveWave`.

**C11.** `Entities/Explosion.cs:102-109`: the private `Start` factory only forwards to the constructor, and its
summary ("with the fan's fixed point at the picture's MIDDLE") describes something it does not do. Delete
`Start`, call `new Explosion(...)` from the two public factories, and move the remark about the middle anchor
to `Layout`, where the anchoring happens.

**C12.** `Entities/Explosion.cs:295-296`: `const int unit = 1; int step = spacing * unit;`. Multiplying by 1 is
noise. Delete `unit`, and use `spacing`. Also `:300`, `* unit`.

**C13.** `Entities/Explosion.cs:241-252`: `PicturePlacement` returns `(Width, Rows, Left, Top)`, and the first two
are just its inputs handed back. The one caller discards them (`:288`). Return `(int Left, int Top)`.

---

## D. Magic numbers and units (standard NUM-1..NUM-5)

Before you start this section, do **H01** (the clock constants) and **H02** (the column constant helper).
Most D items use them.

**D01. The clock literals `5` and `6`** appear in 22 files: every `_timer += 5`, `_timer -= 6`, `>= 6`,
`* 6` and `_moveTimer = 6`. Worst: `Spark.cs` (11), `Enforcer.cs` (11), `Spheroid.cs` (7), `TankShell.cs`,
`Quark.cs`, `Brain.cs` (6 each), `Tank.cs` (5). After H01, replace each with
`ArcadeClock.UnitsPerPortTick` / `ArcadeClock.UnitsPerRomFrame`. Do not change any arithmetic.
Find them with: `grep -rnE "(\+=|-=) 5;|\* 6\b|>= 6\b|< 6\b|= 6;" src --include=*.cs`

**D02. PlayField caps and timings** (`Level/PlayField.cs`):
- `:209` `< 8` → `GameplayConstants.EnforcerCap` (ROM `ENFCNT`)
- `:211` `< 20` → `GameplayConstants.TankCap` (ROM `TNKCNT`)
- `:217` `< 20` → `GameplayConstants.ShellsPerWave`
- `:135` `PortTicks(270)`, `:875` `PortTicks(225)`, `:877` `< 30`, `:94` `= 2`, `:888` `2 *`, `:892`
  `== 2 ? 1 : 2` → named constants with ROM xrefs (the xrefs are in the comments at `:131-134` and `:859-867`).
- `:300` `ScreenSize.Scaled(3)` → `GameplayConstants.BrainCatchReachArcadePixels`
- `:970-971` `ScreenSize.Scaled(4)` → `LaserWallFlareThicknessArcadePixels` / `LaserWallFlareLengthArcadePixels`
- `:1331` `Next(100)`, `:1339` `Next(4)`: name the percentage base, and use an enum or named constants for
  the four edges.
- `:1271` `GridScanStep = 4`: already named. Its summary should give the unit (port pixels).

**D03.** `Level/PlayField.cs:1060` `ScreenSize.Scaled(2)`. Name it once A04 is answered.

**D04. Entity literals.** Name each of these in the entity (private const, with a summary that gives the unit and
ROM source) or in `GameplayConstants`:
- `Hulk`: `Next(1, 32)` twice (`Hulk.cs:60`, `:154`), `Next(-16, 16)` in `PickDirection`, the step sizes `3 : 4 : 2`
  and `& 1` / `& 3` in `Update`, and the knockback `Next(2)`, `* 2`, `Next(4) < 3`, `* 4` in `ApplyKnockback`.
- `Tank`: `Next(256) <= 96` (`Tank.cs:216`), `Next(0, 32)` (`:79`), `VerticalMoveThresholdScreenPx = 32`
  (derive it: `ScreenSize.Scaled(16)` arcade px, named).
- `Enforcer`: `Next(0, 32)` twice (`:141-142`, `:177`), `delta / 2` (`:149`), `/ 256` (`:159-161`), and the
  default `fireDelayRomTicks = 24`.
- `Spheroid`: `RollAccelerations` (`:236-238`: 32, 16, 64, 32, 15), `ClampThenDamp` (`:273`: `-4`, `- 4`,
  `>> 8`), `AdvanceAxis` (`>> 8`, `<< 8`), `RerollDropCountdown` (`/ 4`).
- `Quark`: `_dropDelayRomTicks >> 1`, `Next(2)`, and `ScreenSize.Scaled(1)` in `StartFlee`.
- `Brain`: the missile muzzle `3 * ScreenSize.SpecScale, 4 * ScreenSize.SpecScale` (`Brain.cs:195`). Use
  `ScreenSize.Scaled` like everything else, and name it `CruiseMissileMuzzleOffsetArcadePixels`. Also the
  placement offsets `Scaled(1)`, `Scaled(8)`, `Scaled(4)`, `Scaled(2)` in `BeginReprogramming` (`:235-253`).
- `Prog`: `* 4` and `19`, `* 2` in `RollOffsets` (`Prog.cs:253-254`).
- `Human`: `Next(8)`, `Next(128)` in the constructor (`Human.cs:92-94`) and `PickNewDirection`, and `* 4` / `% 4`
  (four substeps per block).
- `Grunt`: `moveLimitBeats = 15` default, and `_walkFrame % 4 + 1`.
- `Player`: `& 3` in `AdvanceWalkAnimation`; the walk group numbers `0..3` (see G09).
- `TankShell`: `Next(0, 32)`, `Next(-1, 2)`.
- `ScoreValues.RescueBonus` / `RescueScoreMarker`: `5` (the SVITAB cap) is written twice. Make one constant.

**D05. The ROM column written as a bare `2`** (1 column = 2 arcade pixels). `GameplayConstants.ArcadePixelsPerColumn`
exists; use it (via H02) in: `Spark.cs:60`, `:65-66`; `Spheroid.cs:177-178`; `Tank.cs:139`;
`CruiseMissile.cs:34`, `:86`, `:142`; `Quark.cs:208` (`coordinateUnitArcadePixels: 2`); `Enforcer.cs:141`;
`MovieObject.cs:81`; `AttractPageMachine` (`NextByte() * 2`, twice).

**D06. Hard-coded screen sizes.** `Rendering/TunnelEffect.cs:85-86`: `640f / 304f` and `400f / 256f`. Use
`ScreenSize.Width`, `ScreenSize.Height`, `GameplayConstants.ArcadeScreenWidth` and `ArcadeScreenHeight`.

**D07. Hard-coded pixel-per-unit values** that should be derived, not written out:
- `GameplayConstants.cs:349-353`: `TankBirthOffsetX = 8`, `TankBirthOffsetY = 12`, `TankBirthOffsetYOffTopWall = 10`
  are port pixels written as numbers, while every other offset is in arcade units and scaled at the use site. Store
  columns and rows (2, 6, 5), and scale at the use site in `Quark.AdvanceTankDrop`.
- `Grunt.cs`: `StepScreenPixels = 8`, `DeadZoneScreenPixels = 4` → `ScreenSize.Scaled(4)` / `Scaled(2)` from
  arcade-pixel constants.

**D08. Frame counts in `SpriteSet`'s constructor** (`Rendering/SpriteSet.cs:218-285`): `12, 3, 9, 8, 6, 27, 38`
and `PlayerFrames[6]`. Make them named constants (`PlayerFrameCount`, ...), and name the index
(`FirstDownFacingPlayerFrame = 6`).

**D09. Shader names as strings**: `"GlyphCycle"`, `"SolidRemap"`, `"SlotId"`, `"RemapColor"`
(`SpriteSet.cs:414, 417, 683, 685`). Make them `private const string`s in one place, commented as "must match
`Content/Effects/ColorCycle.fx`".

**D10. Input literals** in `Input/ControlSettings.cs:66-77`: `0.5f` (trigger threshold), and the hard-wired
`Keys.Space`, `Keys.Insert`, `Keys.D1/D2/NumPad1/NumPad2`, `Buttons.A/Start/Back`. Name the threshold, and
collect the fixed keys as named `static readonly` fields with a summary saying why they are not rebindable.

**D11.** `States/PlayingState.cs:158`: `(slot.Wave % 255) + 1` → a named constant for the byte-counter wrap
(ROM `GEXX`).

**D12.** `Rendering/DefineInputsHighlight.cs:28` (new, uncommitted): `Green = 0x38` repeats
`GamePalette.DefaultSlots[6]`. Read the value from there instead of copying it.

---

## E. Duplication

**E01. `PlayField` "live count" properties** (`PlayField.cs:209-238`): eleven copies of
`_x.Count(e => e.LifeState != EntityLifeState.Dead)`. Add one private helper
`static int CountLive(IEntityList list)` and use it. Note `ActiveSparkCount` (`:204`) counts
`== Alive`, not `!= Dead`. Keep that difference and give it a comment, or ask the author (J08).

**E02. `NearestLivingRobotPositionTo`** (`PlayField.cs:342-373`) lists nine robot lists by hand. That breaks the
registry promise in `RobotKinds`. It also repeats the Manhattan distance from `NearestHumanPositionTo`
(`:323`). *Fix:* add `private static int ManhattanDistance(IntVector2 a, IntVector2 b)`, use it in both, and
loop over `RobotKinds.All` with `ListOf(robot.Kind).Entities`, skipping `Electrode`, `Spark` and `TankShell`.
That matches the current set: today's list has no electrodes, sparks or shells, but **does** include
`CruiseMissile`. *Done when:* `PlayFieldNearestRobotTests` still pass.

**E03. `InnerBounds` is computed identically in three states**: `PlayingState.cs:33`, `AttractState.cs:34`,
`StorylineState.cs:31`. It is paired with the same `Margin`. Move it to one place, e.g.
`ScreenSize.PlayfieldBounds` or a `PlayfieldLayout` static class, and use that.

**E04. The `WallColorCycle` construction** is repeated in `PlayingState.BuildField`, `AttractState` (`:63`) and
`StorylineState` (`:53-55`). `WallColorCycle`'s own defaults already provide those values. Use `new WallColorCycle()`,
or remove it entirely (J07).

**E05. Start-button edge detection** (`_previousStartOne` / `_previousStartTwo`) is duplicated in
`AttractState.cs:73-81`, `StorylineState.cs:66-75` and `TitleScreenState.cs:157-179`. Extract a small
`StartButtons` edge-detector class in `Input/` with `bool OnePlayerPressed(PlayerInputState now)` etc., or
put the edge detection into `PlayerInputState` itself.

**E06. Human kind → size and kind → frames** are written twice: `Human.ArcadeCollisionSize` / `Human.FramesOf`
and `Prog.ArcadeCollisionSize` / the switch in `Prog.Draw`. Move both to one place. `HumanKind` extension
methods in `Entities/HumanKindExtensions.cs` fit, or `SpriteSet.FamilyFrames(HumanKind)` for the frames.

**E07. Player death reset** is written twice: `Player.StartDeath` (`:315-324`) and `ResetForLevelRestart`
(`:327-341`) both reset the five death fields. Extract `ResetDeathAnimation()`.

**E08. `Player.DeathSolidSlot`** (test hook) repeats the slot choice in `Player.Draw`. Make `Draw` use
`DeathSolidSlot`, and make it private-with-internal-getter as needed.

**E09. `Draw` repeats `CurrentAnimationFrame`'s lookup** in `Grunt.Draw` (`_sprites.GruntFrames[WalkArtIndex(...)]`)
and `Hulk.Draw`. Use `CurrentAnimationFrame`, as the other entities do.

**E10. Duplicate constants**: `GameOverTextColumn/Row` (`GameplayConstants.cs:586-587`) and
`GameOverMessageColumn/Row` (`:808-809`) are the same values (62, 128) for the same ROM string. Keep one pair,
and change `GameOverState.cs:75-76` to use it.

**E11. The subpixel scale 256** has three names: `SparkVelocityScale`, `QuarkSubpixelsPerPixel`, and a bare
`256` in `Enforcer` and in `Spheroid` (as `>> 8`). Make one `GameplayConstants.SubpixelsPerPixel = 256`
(ROM: 16-bit coordinates, high byte = pixel) and use it everywhere.

**E12. Persistence path**: `HighScoreStore.cs:19-22` and `ControlSettingsStore.cs:93-96` both build
`%LocalAppData%\Robotron2084\`. Extract `Persistence/AppDataPaths.cs`.

---

## G. Naming and terminology (standard NAM-*; do these with the IDE's rename so tests follow)

**G01. `...RomTicks` means ROM frames.** 19 identifiers use `RomTicks` for what the ROM and the rest of the code
call **ROM frames** (`BeatIntervalRomTicks`, `SparkLifeMinRomTicks`, `fireDelayRomTicks`, `hulkSpeedRomTicks`,
...). `PortTicks(int romTicks)` itself documents its parameter as ROM frames. Rename every one to `...RomFrames`.
Find them with: `grep -rhoE "\w*RomTicks\w*" src tests --include=*.cs | sort -u`

**G02. Parameters in ROM frames that are really beats.** `Enforcer._fireDelayRomTicks`, `Tank._fireDelayRomTicks`,
`Quark._dropDelayRomTicks` and `Spheroid._dropDelayRomTicks` hold the wave-table value but are used as counts of
**beats** or **rotations** (`Tank.cs:166`: `_fireCooldownBeats = _fireDelayRomTicks`). Name them for what they
count (`_fireIntervalBeats`, `_dropDelayRotations`), and fix the `<param>` docs, which say "in ROM frames".

**G03. "Speed" that is a period.** `LevelParameters.HulkSpeed`, `BrainSpeed` and the constructor parameters
`hulkSpeedRomTicks` / `brainSpeedRomTicks` are delays: a bigger number is **slower**. Rename them to
`HulkStepDelayRomFrames` / `BrainBeatDelayRomFrames`, keeping the ROM name (`HLKSPD`, `BRNSPD`) in the summary.
Do the same for `QuarkMove` / `quarkSpeedRom` (`SQSPD`, a speed cap).

**G04. "Fifths", "sixths" and "6ths" name one unit.** `GameplayConstants.PortTicks` remarks call it "one fifth
of a port tick", `TunnelEffect.PassFifths` and `PlayField.LaserWallFlare.FifthsRemaining` call it fifths,
`AttractMovie._sixths` and eight `SixthsPerRomFrame` constants call it sixths, and `TunnelEffect.cs:64` calls
`PassFifths` "sixths of a port tick" (wrong both ways). After H01, call it **clock units** everywhere:
`PassFifths` → `PassClockUnits`, `FifthsRemaining` → `ClockUnitsRemaining`, `_sixths` → `_clockUnits`,
`_chaseSixths` → `_chaseClockUnits`, `_periodSixths` → `_periodClockUnits`, `_fifths` → `_clockUnits`.

**G05. `Grunt.WalkArtIndex`** (`Grunt.cs`) is left over from the "Art" → "AnimationFrame" rename (§121).
Rename it `AnimationFrameIndexFor(int walkFrame)`.

**G06. `PlayField.IsFrozen` vs `PlayField.RobotsFrozen`** mean different things (hit-stop vs start-grace/death).
Rename `IsFrozen` → `IsHitStopActive`.

**G07. `PlayField.Palette` is backed by `_wallPalette`** (`:84`, `:951`), but it is the whole 16-slot game
palette, used by the player's death fade. Rename the field and constructor parameter to `_palette` / `palette`.

**G08. `PlayField.Burst` / `PlayField.Shatter`** are verbs with no object. Rename them `KillWithScoreBurst` /
`KillWithStripExplosion`, which say what happens.

**G09. Magic "group" ints.** `Player.WalkGroup` returns `0 left / 1 right / 2 down / 3 up`; `Brain` uses
`LeftDirectionBase = 0 ...`; `Prog.WalkFrameIndex` switches `0..3`; `AttractObjectMachine.BeginWalk` takes
`direction` `0..3`. Introduce one enum `WalkFacing { Left, Right, Down, Up }` in `Entities/` (its own file) and use
it in all four. Keep the numeric values so the frame arithmetic (`(int)facing * 3`) is unchanged.

**G10. `Explosion`** is both the death explosion and the wave-start appear, with a nested `Kind` enum exposed as
the `Mode` property. Rename the class `StripEffect`, `Kind` → `StripEffectKind` (own file, per §121), and `Mode`
→ `Kind`. Rename `Dispatch` → `FanForShot`. This touches tests. Do it in one commit.

**G11. `ScoreBurst` and the `ScoreBurst*` constants** are the spheroid/quark **death** burst. The "score" part
is only its last phase. Consider `DeathBurst`. **Ask the author** (J09); the ROM calls it `CIRKP`, "death throes".

**G12. `PlayerLaser.Deactivate`** vs `Kill` on every other entity. Rename it `Kill`, and have `PlayerLaser`
implement `IRemovable`.

**G13. Aim vs shoot vs fire.** `PlayerInputState.AimDirection`, `PlayerControls.ShootDirection`,
`InputAction.Shoot*`, `Player.UpdateFiring(... aim ...)`. Pick **Shoot** (it is the name on the DEFINE INPUTS
page) and rename `AimDirection` → `ShootDirection`.

**G14. `PlayerInputState.FirePressed`** is true while held, not on the press edge (`Player.UpdateFiring` does
its own edge detection). Rename it `FireHeld`. Same for `SkipLevelPressed`, `StartOnePlayerPressed` and
`StartTwoPlayersPressed`: they are all "held" states.

**G15. Abbreviations**: `SoundEntry.Len` / `.Dur` (→ `TicksPerRepeat` / `Repeats`; check `SoundEngine` for
the meaning first), `MovieProcess.Pc` (→ `ScriptCursor`), `Rprog*` (→ `ReprogramShake*`), `_reDirStepsRemaining`
(→ `_stepsUntilNewDirection`), `(int gw, int gh)` in `Tank.Draw`.

**G16. `ScoreValues.RescueBonus(int savesThisGame)`**: the caller passes `RescuesThisLife`. Rename the parameter
`rescuesThisLife`.

**G17. `RobotKind` includes electrodes and shots** (`Spark`, `TankShell`, `CruiseMissile`), which are not robots.
The enum summary already says "every kind the playfield keeps a list of". Rename it `HazardKind` (with
`RobotKinds` → `HazardKinds`, `RobotKindInfo` → `HazardKindInfo`), or keep it and state the definition at the top of
`RobotKind.cs`. **Ask the author** (J10).

**G18. `CruiseMissile.StepColumns` / `StepRows`** hold port pixels, not columns or rows. Rename them
`StepXPortPixels` / `StepYPortPixels`.

**G19. `Hulk.Position` has an `internal set` test hook** (`Hulk.cs:91`), while `Player`, `Human` and `PlayerLaser`
use `TeleportTo`. Pick one idiom: remove the setter, add `internal void TeleportTo(IntVector2)`, and update
the tests.

**G20. `Presentation.Next(ScaleMode)`** → `NextScaleMode`. **`RectangleExtensions`** holds `IntVector2`
extension methods (`IsWithinDistance`, `IsFartherThan`). Move those two to `IntVector2Extensions.cs`.

**G21. `PlayingState.BuildField`** assigns `_field` **and** returns it (`:72-80`), and the constructor assigns the
return value again. Make it `private void StartTurn()` that assigns and resets, or make it pure and let the
caller assign. Do not do both.

---

## H. Consistency

**H01. One definition of the arcade clock.** There are three idioms for the same clock:
1. bare literals `5` / `6` in every entity (`Spark`, `Enforcer`, ...);
2. `private const int SixthsPerPortTick = 5; SixthsPerRomFrame = 6;` copied into **eight** files
   (`Hud/HighScoreFrameAnimation.cs:25-28`, `Hud/HighScorePageHold.cs:45-48`, `Hud/HighScorePrintSequence.cs:31-34`,
   `Hud/InitialsEntryModel.cs:43-46`, `Rendering/DefineInputsHighlight.cs:22-25`, `Rendering/HighScorePalette.cs:34-37`,
   `Rendering/PaletteAnimator.cs:30`, `Rendering/PresentationPagePalette.cs:29-32`);
3. `GameplayConstants.PortTicks(romFrames)`, which truncates. `SkullMarker`, `RescueScoreMarker`, `PlayingState`
   and `PlayField` use it, and its own doc says it runs one tick early.

*Fix, step 1 (mechanical):* create `src/Robotron2084/Core/ArcadeClock.cs`:
```csharp
namespace Robotron2084.Core;

/// <summary>The arcade's frame clock expressed in whole numbers (notes §52).</summary>
/// <remarks>A ROM frame is 6/5 of a port tick, so both are whole numbers of a CLOCK UNIT: a port tick
/// is <see cref="UnitsPerPortTick"/> units and a ROM frame is <see cref="UnitsPerRomFrame"/>. A timer
/// adds <see cref="UnitsPerPortTick"/> each tick and fires each time it has gathered a ROM frame's
/// worth, carrying the remainder, so it never drifts.</remarks>
public static class ArcadeClock
{
    /// <summary>Clock units in one port tick (1/60 s).</summary>
    public const int UnitsPerPortTick = 5;

    /// <summary>Clock units in one ROM frame.</summary>
    public const int UnitsPerRomFrame = 6;

    /// <summary>A number of ROM frames, in clock units.</summary>
    public static int Units(int romFrames) => romFrames * UnitsPerRomFrame;
}
```
Delete the eight private copies and replace every literal in D01 with these names. No arithmetic changes.
Move the long explanation in `GameplayConstants.PortTicks`'s remarks onto `ArcadeClock`, and leave
`PortTicks` a one-line summary that points at it.

*Fix, step 2 (optional, separate commit, only after step 1 is green):* add a `RomFrameTimer` struct in its own
file with `int Units`, and `bool Advance(int periodRomFrames)` that does
`Units += UnitsPerPortTick; if (Units < Units(period)) return false; Units -= Units(period); return true;`.
Use it for the "count up, compare, subtract" timers only (beat, move, flicker). Leave the count-down timers
(`_remainingLife -= 5`) alone. **Some timers are seeded at `6` so they fire on the first tick. Keep the
seeding** (`timer.Units = ArcadeClock.UnitsPerRomFrame`).

**H02. One way to convert arcade units to port pixels.** Add to `Core/ScreenSize.cs`:
```csharp
/// <summary>Arcade (ROM) pixels to port pixels on the playfield: one arcade pixel is one spec pixel.</summary>
public static int ArcadePixels(int arcadePixels) => Scaled(arcadePixels);

/// <summary>ROM columns to port pixels: a column is two arcade pixels.</summary>
public static int Columns(int columns) => Scaled(columns * GameplayConstants.ArcadePixelsPerColumn);
```
(`ArcadePixelsPerColumn` may need to move to `Core` to avoid a `Core` → `Tuning` dependency. Moving it is part
of this item.) Then use `ScreenSize.Columns(n)` for every D05 site. **Do not touch `GameplayConstants.ArcadeX`
/ `ArcadeY`.** They use a different, proportional mapping for the HUD. That difference is J05.

**H03. Entity class layout.** The entities put members in different orders. Test hooks sit in the middle of
some classes (`Spark.FrameIndex` between `Kill` and `Update`), and `LaserWallFlare` sits inside `PlayField`.
Use the order in standard STR-5 for each entity file. Moves only.

**H04. Unexplained `LifeState` guards.** `Update` in some entities returns on `!= Alive` and others on `== Dead`.
`Draw` does the same. For entities that are never `Dying` (all except `Electrode` and `Player`), the two are the
same. Use `!= Alive` everywhere except `Electrode` and `Player`, which need `== Dead`.

**H05. `Kill` guards.** `Prog.Kill` and `Electrode.Kill` guard on `Alive`, and the others do not. Harmless, but
make them uniform. Guard in all of them.

**H06. Early-exit style in loops.** `PlayField.ResolveBrainCatches` (`:748`) and `ResolveHulkVsHumanCollisions`
(`:791`) test `robotsHeld` **inside** the loops and `break`. Test it once at the top and `return`.

**H07. Formatting defects: a `<summary>` or a second member on the same line as a field.**
`Entities/Brain.cs:24`, `CruiseMissile.cs:26`, `Player.cs:28`, `PlayerLaser.cs:18`, `Prog.cs:26`,
`RescueScoreMarker.cs:16`, `SkullMarker.cs:18`. Put each on its own line. Also `Spark.cs:167`: a blank line
at the start of `MoveBy`.

**H08. Scattered fields in `PlayField`.** `_scoreBursts` (`:65`), `_laserWallFlares` (`:73`), `_shellsFiredThisWave`
(`:215`), `BrainCatchReach` (`:300`), `GridScanStep` (`:1271`) and `LaserWallFlare` (`:1011`) are declared in the
middle of methods. Group constants, then fields, at the top of the class. (Most of this goes away with F01.)

**H09. The test-hook block at the bottom of `PlayField`** (`:1456-1509`) has irregular blank lines and
interleaved `Add*`/list members. Sort it: all `Add*` methods, then all list properties, then the order arrays.

**H10. State constructors take their dependencies three different ways**: `GameServices` (attract states),
individual parameters (`PlayingState(SpriteSet, HighScoreStore, GameSession)`), and
`GameOverState.FromSession(input, sprites, store, session)`. `GameServices`' own doc says the in-game states
"do NOT need this bundle", but they take two of its four members one at a time. **Ask the author** (J11)
whether in-game states should take `GameServices` too.

---

## I. Tests

**I01.** 24 private `CreateField` / `CreateEmptyField` / `EmptyField` helpers across the test files each build a
`PlayField` their own way. Add `tests/Robotron2084.Tests/Level/PlayFieldBuilder.cs`, a fluent builder with
defaults (sprites, parameters, input, bounds, random seed, lives), and move the tests to it one file per commit.

**I02.** Test folders do not mirror `src/`: `Level/GamePaletteTests.cs` and `Level/PaletteAnimatorTests.cs` test
`Rendering/` classes. Move them to `Rendering/`.

**I03.** `Level/PlayFieldPhaseETests.cs` is named after a plan phase. Rename it by what it tests (brains, progs
and missiles), or split it by entity.

**I05.** `tests/Robotron2084.Tests/TestSprites.cs` holds two top-level types (`NoSpriteSource` at `:13` and
`TestSprites` at `:29`), which breaks the one-type-per-file rule (§121). Move `NoSpriteSource` to
`NoSpriteSource.cs`.

**I04.** `Player.StartDeathForTesting` exists because `InvincibleForTesting` defaults to `true` (see J03).
Once J03 is resolved, the hook may be unnecessary. Revisit it then.

---

## K. Tooling that stops these coming back

**K01. Turn on XML documentation checking.** Add `<GenerateDocumentationFile>true</GenerateDocumentationFile>` to
`Directory.Build.props`. With `TreatWarningsAsErrors` this makes CS1572 (param tag with no parameter), CS1573
(parameter with no tag), CS1574 (cref that does not resolve), CS1587 (misplaced doc comment) and CS1591 (public
member without docs) **build errors**. That catches B13-B16 and the orphaned-summary problem from notes §120.
The build will fail at first. Do this in two stages. First count the errors by code. Fix every
CS1572/CS1573/CS1574/CS1587 (most are B15). Then look at CS1591: if there are more than about 150, **stop
and report the count to the author** before writing summaries. **Do not suppress CS1591.** The author's rule
(notes §109.1) is that every member has a summary.

**K02.** Add the one-type-per-file scan and the magic-clock scan from `coding-standards.md` (section "Review
checklist") as a script in `tools/`, e.g. `tools/lint-conventions.ps1`, that exits non-zero on a hit.

---

## F. Structure (larger refactors; one sub-step per commit; behaviour must not change)

**F01. Split `PlayField` (1510 lines).** It holds collisions, spawning, materialisation, grunt speed progression,
laser-wall flares, sound requests, drawing, queries and test hooks. Extract, in this order, one per commit:
1. `Level/GruntSpeedProgression.cs`: fields `_gruntSpeedFloor`, `_gruntSpeedFloorStep`, `_gruntProgressTimer`;
   methods `UpdateGruntSpeedProgress`, the floor used by `SpeedUpGrunts`, and `GruntSpeedFloor`. `PlayField` holds
   one instance and calls `Update(grunts)`.
2. `Level/LaserWallFlares.cs`: the list, `SpawnLaserWallFlare`'s geometry, the per-tick countdown (`:391-401`),
   the drawing block in `Draw` (`:1055-1080`), and the `LaserWallFlare` record (move that to its own file too).
3. `Level/WaveMaterialisation.cs`: `_pendingAppear`, `_assembling`, `_appearSequence`, `QueueMaterialise`,
   `AdvanceMaterialisation`, `IsMaterialising` and `PendingAppearCount`. It needs the explosions list and the
   strip clip. Pass them in.
4. `Level/SpawnPlacement.cs`: `FindSpawnPoint` (both overloads), `RandomPointInside` and `RandomSpheroidCandidate`.
   Replace the `candidateOverride` + `IntVector2.Zero` sentinel with a `Func<IntVector2?>` that returns `null`
   for "use a uniform point", and delete the five `static _ => default` arguments.
Keep `PlayField`'s public and internal API the same, so tests do not change.

**F02. Split `GameplayConstants` (844 lines)** into files by domain under `Tuning/`, keeping the class names
obvious: `WavePaletteTables` (the four per-wave tables and `*ForWave`), `PlayerTuning`, `EnemyTuning` (or one
class per enemy), `CollisionSizes`, `HudLayout` (the `Hud*`, `*MessageColumn/Row`, `ArcadeX/Y`), `AttractTuning`
and `ScreenTimings`. The existing `// ---- ... ----` section comments are the split lines. Do it one class per
commit, updating usages with the IDE.

**F03. Split `SpriteSet` (753 lines)** into:
- `SpriteSet`: the textures only (the constructor and properties);
- `ArcadeText`: `DrawGlyph*`, `DrawSmallFontText`, `DrawLargeFontText`, `Measure*`, `DrawTableNumber`,
  `DrawRubMarker`, `GlyphIndex`, `DrawMiniMan`. This also removes `SpriteSet`'s dependency on `Hud.ScoreFormatter`
  (Rendering should not depend on Hud);
- `BlitterDraw`: `DrawSprite`, `DrawSpriteSolid`, `DrawSolidRectangle`, `DrawSpriteSolidWithBackground`,
  `UsePassThrough`, `SlotColor`, and the `ColorCycleEffect` / `Palette` properties.
This touches many call sites. **Ask the author before starting** (J12).

**F04. Replace hard casts in the robot registry.** `RobotKinds.cs:51` `((Hulk)target)`, `:96`, `:104`, `:113`
and `PlayField.cs:534`, `:547` `((IRemovable)target)` throw `InvalidCastException` if a list ever holds the wrong
type. Use pattern matching with a clear exception:
`if (target is not IRemovable removable) throw new InvalidOperationException($"{target.GetType().Name} cannot be removed by a laser.");`
Alternatively, make every `LaserHit` go through one `PlayField.Remove(IEntity)` helper that does this check once.

**F05. Test-only default in a production constructor.** `PlayField(... bool playerInvincibleForTesting = true ...)`
(`PlayField.cs:111`): the **real game** (`PlayingState.cs:77`) omits it and gets an invincible player. See J03.
The structural fix, once J03 is decided, is to remove the parameter's default so every caller states it.

---

## J. Decisions for the author (do NOT fix; list them in your report)

**J01.** Delete the unused `LevelTable.csv` override in `LevelParameterGenerator` (no CSV exists), and the
`LevelParameters` fields only it uses (`EnemySpeedBonus`, `ShellSpeed`)?

**J02.** `RobotKindInfo.WaveCount`: use it in the spawners, or delete it? (C07)

**J03. The player cannot die in the real game.** `GameplayConstants.PlayerInvincibleForTesting => true`
(`:137`, "TEMPORARY playtest aid (2026-09-13 round 7) ... TURN THIS OFF") feeds `Player.InvincibleForTesting`,
and `PlayingState` never overrides it. Is the playtest over? If so, set it to `false`, delete the constant, and
make invincibility a dev key or a constructor argument that only tests pass.

**J04.** `Audio/Sound` is a static service locator with global mutable state (`Enabled`, the engine), called
from `PlayField` and `Brain`. Inject an `ISoundPlayer` instead? Also, the sequencer is ticked once per **port
tick** (`SoundEngine.cs:49`, "≈ one arcade vblank"), while everything else converts ROM frames at 6/5. Should
sound use the arcade clock too?

**J05. Two meanings of "arcade pixel".** Entities treat 1 arcade px as 1 spec px (`ScreenSize.Scaled(n)`, a
factor of 2.0). The HUD, messages and tunnel map arcade px proportionally, `ArcadeX(n) = n * 640 / 304` (a factor
of 2.105) and `ArcadeY(n) = n * 400 / 256` (1.5625). Notes §113 raised the column question. This is the same
problem one level down. Which mapping is canonical, and should the other get its own name (e.g.
`ArcadeScreenX` vs `ArcadePlayfieldPixels`)?

**J06. Four implementations of the ROM's generic mover (`OPB80`)**, each handling the fraction differently:
`Enforcer.AdvancePosition` and `Quark.AdvancePosition` drop the whole-pixel step on a blocked axis but keep
the fraction, `Spark.MoveBy` clamps the step to `SparkMaxSpeed` first, and `Spheroid.AdvanceAxis` keeps the old
fraction on a blocked axis. Unify them into one `SubpixelMover` (behaviour change), or document why they differ?

**J07.** `WallColorCycle` and `DefaultWallPalette` only colour the wall when there is no live palette, which
happens only in tests. Delete them and give tests a palette, or keep them?

**J08.** `PlayField.ActiveSparkCount` counts `Alive` and every other count uses `!= Dead`. Sparks are never
`Dying`, so they agree today. Standardise?

**J09.** Rename `ScoreBurst` → `DeathBurst`? (G11)

**J10.** Rename `RobotKind` → `HazardKind`? (G17)

**J11.** Should in-game states take `GameServices` too? (H10)

**J12.** Approve the `SpriteSet` split in F03?

**J13. Entities depend on the whole concrete `PlayField`** (`IEntity.Update(GameTime, PlayField)`). An entity can
reach every list, every spawn method and every test hook. A narrow interface (e.g. `IFieldContext`: `Wall`,
`Player` position, `RobotsFrozen`, the spawn requests, `NearestHumanPositionTo`, `Palette`, the `Can*` caps) would
make each entity's needs explicit and testable. This is the largest refactor on the list. Approve before
anyone starts?

**J14. `Explosion` (and `ScoreBurst`, `SkullMarker`, `RescueScoreMarker`) implement `IEntity`** only so they can
live in an `EntityList<T>`. `Explosion.Update` ignores both parameters, and even declares
`PlayField? field = null` (`Explosion.cs:158`), which does not match the interface. An `IFieldEffect` interface
(Update/Draw/LifeState, no Position/Bounds) would be honest (ISP). Worth doing?

**J15.** `GameMode.TwoPlayerSimultaneous` (`GameMode.cs:22`) is selectable with F3 but runs the alternating game.
Hide it until it is built, or show a "not yet" message?

**J16.** `GameSession.FromSlots` (`:91-99`) always sets `GameMode.OnePlayer`, even for two slots, so `Mode` and
`IsTwoPlayer` can disagree. Take the mode as a parameter?

**J17.** The dev keys (F4-F9) and the skip-level key (Insert) are live in every build. Put them behind
`#if DEBUG` or a settings flag?

**J18.** `HighScoreStore.Load` and `ControlSettingsStore.Load` catch `Exception` and silently return defaults
(`HighScoreStore.cs:40`, `ControlSettingsStore.cs:52`). A corrupt high-score file is silently replaced on the
next save. Narrow the catch to `IOException` / `JsonException` / `FormatException`, and keep a backup of the bad file?

**J19.** `Human.Steps` rows for UP+LEFT (`Human.cs:43`: frames `0,2,0,2`) and DOWN+LEFT (`:49`: `0,1,0,1`)
break the `0,1,0,2` pattern of the other six blocks. Is that the ROM's `HUMATB` exactly, or a transcription slip?

**J20.** `Player.FacingDirection` starts as `Up` (`Player.cs:87`), but the walk animation starts facing down
(`_animGroup = WalkGroup(Direction8.Down)`, `:65`). The first shot with no aim therefore goes up while the man
faces down. Intended?
