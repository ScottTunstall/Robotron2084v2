# Code review against `docs/coding-standards.md` (2026-10-30)

Targeted review of the entity layer — `src/Robotron2084/Entities/` (23 entity types) and the
`Level/` code that orchestrates it — against the standard in [coding-standards.md](coding-standards.md).
The question asked was whether entities are implemented consistently, and whether the entity layer
is SOLID, clean, idiomatic and free of surprise. Every finding cites the rule ID from the standard.
Findings are ranked most severe first, in the format the standard's Part 2 prescribes.

**Baseline:** `dotnet build Robotron2084.slnx -c Debug` — 0 warnings, 0 errors.
`dotnet test Robotron2084.slnx` — 929 tests, 826 passed, 103 skipped (each skipped test names a
ROM file absent from `ref/rom`), 0 failed. Working tree clean apart from this document and the
untracked review documents listed under **Relation to earlier reviews**.

Still clean, re-checked by scan: no suppressions (`#pragma warning disable`, `[SuppressMessage]`,
`NoWarn`); no `catch (Exception)` (ERR-1); no `TimeSpan` timers in gameplay (TIME-1); no
`...RomTicks` names (NAM-2); no clock literals `+= 5` / `* 6` (NUM-2) and no column literal (NUM-3);
no `LifeState ==` comparison outside `EntityLifeExtensions` (STR-9 clean, 28 call sites checked);
no `public static ... { get; set; }` statics (SOLID-D); test folders mirror `src/` (TEST-4).
One `?? new Random()` group is unchanged (TIME-3, see Major).

**Method:** the standard's Part 2 procedure, scoped to `Entities/` and the entity-facing half of
`Level/`, plus one cross-cutting read of each of these dimensions across **all 23 entity types**,
comparing siblings rather than reading files one at a time:

1. which interface list each class declares;
2. where the beat interval is declared, and how the accumulator is written;
3. what guards the top of `Update`, and what guards the top of `Draw`;
4. how the entity asks the playfield for the player, the wall and the first beat;
5. who owns the wave-start wait (`IWaveStartRobot` or an in-`Update` guard);
6. how `LifeState` is set and read, and which kinds ever leave `Alive`.

Two probes were run beyond the standard's scans, and they are the source of the confirmation
under **CMT-8** and the `gameTime` count under **SOLID-I**:

```
dotnet build src/Robotron2084/Robotron2084.csproj --no-incremental -p:GenerateDocumentationFile=true
for f in Entities/*.cs; do awk '/void Update\(GameTime gameTime/,/^    }$/' "$f" | grep -c gameTime; done
```

The second probe returns exactly 1 for every entity file — the signature line and nothing else.
No entity body reads `gameTime`.

**Relation to earlier reviews:** this document is the fourth. It does not repeat findings already
recorded in [code-review-2026-10-10.md](code-review-2026-10-10.md),
[code-review-2026-10-11.md](code-review-2026-10-11.md),
[naming-issues-2026-10-11.md](naming-issues-2026-10-11.md) and
[clean-code-gist-review-2026-10-11.md](clean-code-gist-review-2026-10-11.md), except where the
same defect was re-verified and is worth restating because it is severe. Those are marked
**confirms**. The following were re-checked and are **unchanged since 2026-10-11**: the playtest
invincibility Blocker; `IEntity.Update`'s unused `gameTime` in all 22 implementations; the
`?? new Random()` defaults at `Grunt.cs:106`, `BerzerkRobot.cs:95`, `Player.cs:95`; and the
documentation toolchain — the probe still reports **102 `CS1570` errors in 21 files**, the same
count and the same file list as the earlier review.

**Counts: 1 Blocker, 9 Major, 6 Minor, 3 Notes.** Fifteen of these are new to the four documents
above; the Blocker and three of the Major entries restate defects recorded on 2026-10-10 and
2026-10-11 that a scan re-verified as still present today.

---

## Blocker

```
[Blocker] TEST-2 + TEST-3 + TEST-1 src/Robotron2084/Tuning/PlayerTuning.cs:83,
  src/Robotron2084/Level/PlayField.cs:109, src/Robotron2084/Entities/Player.cs:110 and :253 — the
  playtest invincibility aid is ON in the shipped game. **Confirms and extends the 2026-10-11
  Blocker: still unfixed, and the reason no gate catches it is now identified.**
  `PlayerTuning.PlayerInvincibleForTesting => true`; `PlayField`'s constructor still
  defaults `playerInvincibleForTesting = true`; `Player.cs:253` still returns early from `Kill()`
  while the aid is on.
  Why: in the running game the player cannot be killed, so no man is ever lost and no wave can
  end. Every feature built on death — the freeze, the wave-clear, the two-player hand-off, the
  high-score entry — is unreachable.
  The 826 green tests cannot see this, and the reason is structural rather than accidental: the
  test harness defaults the same way. `PlayFieldBuilder._playerInvincible = true`
  (tests/…/PlayFieldBuilder.cs:36), and **no test calls `WithPlayerInvincible(false)`** — only two
  tests call that method at all, both with a variable (`PlayFieldCollisionTests.cs:325`,
  `WaveStartSequenceTests.cs:45`). Death-related tests reach `Player.StartDeathForTesting()`
  (Player.cs:276), whose own summary says it starts the death animation "even when
  `InvincibleForTesting` is on. Tests use this." So the suite exercises death through a door built
  to bypass the guard, and an aid intended for playtesting is invisible to every gate the project
  has. `Player.InvincibleForTesting` is a public settable property with no "(test hook)" label and
  no tracking item in its summary (TEST-3), which is what lets it read as production behaviour.
  Fix: set `PlayerTuning.PlayerInvincibleForTesting` to `false`; remove the parameter's default
  at `PlayField.cs:109` **and** `PlayFieldBuilder.cs:36` so both the game and the harness are
  forced to state it — the second is what stops the suite going green over the same defect again;
  move the aid behind a dev key — the DEVELOPER TOOLS page added in ca13bfc is the natural host —
  with a tracking row; make `Player.InvincibleForTesting` `internal`, label it "(test hook)" and
  group it with the other test hooks (TEST-1). Then delete `StartDeathForTesting` if `Kill()`
  becomes reachable, or say in its summary why it still exists.
```

---

## Major

```
[Major] STR-13 + CON-1 + CMT-1 src/Robotron2084/Entities/Spheroid.cs:336 and Quark.cs:221, :239
  vs Grunt.cs:161, BerzerkRobot.cs:145, Gorf.cs:157, Enforcer.cs:151, Brain.cs:377, Hulk.cs:366,
  Prog.cs:355, Tank.cs:314 — "may I act this tick" has three different answers, and the one that
  differs changes behaviour.
  Grunt and its seven siblings return before the accumulator, so the beat timer does NOT advance
  while `field.RobotsFrozen()` is true:
      if (field.RobotsFrozen()) return;
      _beatTimer += ArcadeClock.UnitsPerPortTick;
  Spheroid and Quark guard only the movement, so the beat timer DOES advance while frozen:
      if (!field.RobotsFrozen()) AdvanceMover(field);
      _beatTimer += ArcadeClock.UnitsPerPortTick;
  Quark is a third case again: it moves no, animates yes, re-aims yes, and drops no tanks
  (`if (field.RobotsFrozen()) return;` at :239, after the aim). Shots, markers and effects do not
  consult the freeze at all, which is correct for them.
  Why: this is the one inconsistency in the entity layer that can change what the player
  experiences. A spheroid that has spun through a long wave-start hold or a player-death freeze
  has banked that time, and fires its first enforcer on the first tick play resumes; a grunt in
  the same hold has banked nothing. If that is the ROM's doing — the spheroid, enforcer, quark,
  spark and shell share the object list `$9813` that this very review's `IEntity` remarks name,
  while the grunt family share another routine — it is a deliberate deviation, and CMT-1 requires
  the reason to be written down. It is not written down anywhere, so a reader must assume
  accident. If it is not the ROM's, two robots behave wrongly during every freeze, and no test
  pins it.
  The codebase already knows how to record this kind of decision: `Human.cs:260` carries one
  sentence — "Unlike the robots, family members do not wait for the wave to start. They walk about
  while the robots are still appearing." — and `Human` consults `RobotsFrozen()` not at all, which
  is therefore readable as intended rather than overlooked. `Spheroid` and `Quark` need the same
  one sentence if the split is deliberate.
  This same shape has already been a fidelity bug here once. `arcade-fidelity-notes.md` §122/A01
  records that the tank's tread "advanced on every tick *and* on every beat … and kept advancing
  while frozen or being born", fixed by a red-first test written over the frozen, born and beating
  states. So "a timer that runs during the halt" is a known defect class in this project, not a
  style preference — and the same notes, at :1830, record that the ROM's halted state is a
  separate branch with its own wait (`NAP 4` is the FROZEN-status branch, `BITA #$7F` — robots
  halted) rather than the steady-state loop with a flag tested, which is the shape a beat timer
  that keeps accumulating would need.
  Nor does a test hold either reading. `SpheroidEnforcerTimingTests` pins the enforcer's grow-up
  and fire gaps to the exact tick (`Enforcer_FireGaps_MatchTheRomRate`), yet the one place that
  file touches the freeze steps over it rather than across it — "Run through the start of the wave
  so `RobotsFrozen()` is false for the enforcer" (:102-103) — so the accumulated-during-freeze
  question is exactly the gap between the pin and the guard. `QuarkTankBehaviourTests` pins the
  drop side (6 freeze cases) and nothing pins the beat side.
  Fix: first establish the ROM truth for the `$9813` list's `STATUS` poll — does it take a
  NAP-style frozen branch that skips the beat, as the brain's RRB10 entry does (notes :1830)? —
  and record the answer in `docs/arcade-fidelity-notes.md`. Then either align the two entities
  with the other eight, or keep the split and state it in `Spheroid`'s and `Quark`'s remarks with
  both sources per CMT-11 ("beats continue while `STATUS` is set; the ROM polls the second object
  list separately — disassembly <label>"), verified against `asm/robomame.asm` rather than copied
  forward. Either way, add the test §122/A01 used — a beat-timer assertion driven *through* a
  freeze, not around one — so whichever reading is right is held. Then settle in `IEntity` once
  which layer owns the freeze, and delete the re-checks from the entities that do not need them.
```

```
[Major] CON-1 + NUM-6 src/Robotron2084/Entities/Enforcer.cs:169 and :171, Spheroid.cs:340 and
  :342, Tank.cs:320 and :322, Human.cs:274 and :277 — the beat accumulator is written three ways,
  and four entities recompute the interval on every tick instead of caching it.
      // 1. cached static — Grunt.cs:170, BerzerkRobot.cs:148, CruiseMissile.cs:179,
      //    Prog.cs:358, Quark.cs:225
      if (_beatTimer < BeatIntervalClockUnits) return;
      // 2. recomputed inline, twice a tick — Enforcer, Spheroid, Tank, Human (the sites above)
      if (_beatTimer < ArcadeClock.ToClockUnits(EnforcerTuning.BeatIntervalRomFrames)) return;
      // 3. instance field — Brain.cs:386, Hulk.cs:375
      if (_beatTimer < _beatIntervalClockUnits) return;
  Why: 60 Hz × 2 × 4 entities of redundant conversion in the hot loop is the smaller half of the
  cost. The larger half is that a reader who learns the idiom in `Grunt` has no reason to expect
  a different one two files over, so the cached form reads as an optimisation someone forgot and
  the inline form reads as intentional. Neither is true, and CON-1 is explicit: "If the timer unit
  is a constant in one file, it is the same constant in every file."
  Fix: one idiom — `private static readonly int BeatIntervalClockUnits =
  ArcadeClock.ToClockUnits(BeatIntervalRomFrames)`, as Grunt, BerzerkRobot, CruiseMissile, Prog
  and Quark already have. For the four entities in form 2, cache it and call the cached name on
  both lines.
```

```
[Major] NAM-9 + CON-1 src/Robotron2084/Level/PlayField.cs:160 and :244, :181 and :250 — the
  playfield answers the same two questions twice, and entities use both answers.
  `Player` (PlayField.cs:160) and `GetPlayerPosition()` (:244) are the same fact;
  `Wall` (:181) and `GetPlayfieldBounds()` (:250) are the same fact. The entity layer has split
  into two clean camps, and one entity alone asked the same question two ways:

  - **playfield bounds** — 16 sites across 10 entities reach through the property
    (`field.Wall.PlayfieldBounds` at Grunt.cs:183, Enforcer.cs:236 and :268, BerzerkRobot.cs:231,
    Brain.cs:593 and :718, CruiseMissile.cs:244, Hulk.cs:365 and :528, Prog.cs:352, :390 and :495,
    Spark.cs:227, Spheroid.cs:535 and :555, Tank.cs:499); `Quark` alone calls the facade, at
    :236, :331, :368, :435 and :437. No other entity calls `GetPlayfieldBounds()`.
  - **player position** — 7 sites use `field.Player.Position` (Tank, Prog, Grunt, CruiseMissile,
    Brain, BerzerkRobot, Enforcer:185); `Quark` is absent because it does not chase the player,
    and `Enforcer` alone calls `field.GetPlayerPosition()` at :269 and :270 — the same question,
    84 lines after its own `field.Player.Position` at :185.
  Why: STR-11 names `field.Wall.Intersects(box)` as the second-dot smell and `field.HitsWall(box)`
  as its fix — and the fix is taken, 6 times over (`Hulk.cs:386`, `Human.cs:288`,
  `PlayerLaser.cs:91`, `Tank.cs:454`, `:461`, `:467`). So the sanctioned route works for the
  question it answers, and the split above is narrower than a general facade problem: it is
  specifically the two facades that return *data* which are bypassed, because `Player` and `Wall`
  are public on `PlayField` as well. Where the facade is a behaviour, one route; where it is data,
  two — and with two routes in place every new call site is a coin flip, which is how this drift
  started. It also means the facade stops being the single place the structure behind it can
  change.
  Fix: choose one and make it the only route. Either delete `GetPlayerPosition()` and
  `GetPlayfieldBounds()` and let `field.Player.Position` / `field.Wall.PlayfieldBounds` stand —
  both are plain data reads, which would want adding to STR-11's exemption list, and the rule
  text would then need amending rather than the code — or sweep the 23 property sites onto the
  facades and keep STR-11 as written, `HitsWall` being the precedent that the facade route is
  usable. Either way `Quark`'s 5 and `Enforcer`'s 3 are the small edits, and the camps are already
  clean, so nothing has to be reasoned about per site. Do not keep both routes, and do not add a
  third.
```

```
[Major] STR-6 + SOLID-O src/Robotron2084/Level/FieldEntities.cs:127 — `AreEnemiesGone` lists
  eight kinds by hand, while its sibling 55 lines below is registry-driven.
  `GetNearestLivingRobotPosition` (:182) walks `RobotKinds.All.Where(kind =>
  kind.IsChasedByDemoPlayer)` and so needs no edit when a kind is added. `AreEnemiesGone` names
  Grunts, BerzerkRobots, Gorfs, Spheroids, Enforcers, Quarks, Tanks and Brains in one boolean
  expression, so adding a ninth kind means remembering this method — and forgetting it makes the
  wave clear while the new robot is alive and shooting.
  Why: STR-6 — "If a type claims 'add a row here and nothing else' (`RobotKinds`), then nothing
  else may list the kinds by hand. A hand-written list elsewhere is a bug waiting to happen, even
  when it is right today." It is right today, and the ROM provenance is good (`WVCHEK`,
  `COUNT_ENEMIES_ON_SCREEN` `$2A73`), which is exactly why a registry row is the better home for
  the decision: the hulk, electrode, prog and cruise-missile exclusions are four separate
  judgements compressed into one shared sentence, and `Gorf` and `BerzerkRobot` are the author's
  own kinds, so the ROM remark cannot justify their inclusion and nothing else records it.
  Fix: add `bool CountsForWaveClear` to `RobotKindInfo`, set it per row with its own remark, and
  write `RobotKinds.All.Where(kind => kind.CountsForWaveClear).All(kind =>
  GetList(kind.Kind).GetLiveCount() == 0)`. `IsChasedByDemoPlayer` and `IsOnRobotList()` are
  already this shape of registry data; this is the third such flag, currently spelt as `&&`.
```

```
[Major] CMT-8 + NAM-8 src/Robotron2084/Level/RobotKinds.cs:18 — the "to add a kind of robot"
  remarks send the reader to a method in a class that has no such method, and omit two of the
  five places a new kind must be registered.
  The text says to add "a `GetList` case in <see cref="PlayField" />". `GetList` is in
  `FieldEntities` (FieldEntities.cs:153); `PlayField` has no `GetList`. The `cref` resolves,
  because `PlayField` exists — so the compiler is silent and only the sentence is wrong. The
  remarks also do not mention the `Add` overload (`FieldEntities.cs:206-336`) or
  `AreEnemiesGone` (:127), both of which a new kind needs.
  Why: by this codebase's own severity table a false comment is Major, not Minor, because these
  remarks are the map. This particular sentence is the instruction a contributor follows while
  adding a robot, and it sends them to the wrong 887-line file and tells them the job is finished
  when two registrations remain.
  Fix: repoint the `cref` at <see cref="FieldEntities" /> and list all five `FieldEntities` edits
  — list property, `GetList` case, update order, draw order, `Add` overload — plus the
  `AreEnemiesGone` decision, or the `CountsForWaveClear` row once that exists.
```

```
[Major] SOLID-A + SOLID-L (extends code-review-issues.md F04)
  src/Robotron2084/Level/RobotKindInfo.cs:12 — `LaserHit` is
  `Action<PlayField, IEntity, Direction8>`, so every row that needs a capability must recover it
  by cast, and seven sites do: `target.Require<Hulk>()` at RobotKinds.cs:62,
  `target.Require<IRemovable>().Kill()` at :121, :131 and :142, and `PlayField.cs:631` and :649.
  Why: F04 asked for the hard casts to become pattern matching with a clear exception, and
  `EntityExtensions.Require<T>` did that — the message now names the type, which is the standard's
  stated fallback, so the casts themselves are not the finding. The finding is one level up: a
  parameter typed `IEntity` that every implementation must narrow is SOLID-A's definition of a
  leak, because the signature promises less than the body needs and the compiler cannot check the
  gap. A future row whose `Require<IRemovable>()` is not satisfied by its kind fails at runtime,
  in play. Nothing in the suite asserts that each row's cast is satisfiable for the kind it is
  attached to; `PlayFieldRobotRegistryTests` pins order, score, sound and wave counts, not the
  capability.
  Fix: give the capability to the row as data instead of recovering it by cast — an
  `Action<PlayField, IRemovable, Direction8>` for removable kinds, and a knockback-shaped
  delegate for the hulk, so `Hulk` need not appear in `Level` at all. If `IEntity` is kept, add
  the missing assertion to `PlayFieldRobotRegistryTests`: for every row, the entity its kind puts
  on the field satisfies whatever `Require<T>` its `LaserHit` performs.
```

```
[Major] SOLID-I src/Robotron2084/Entities/IEntity.cs:41 and all 22 implementations — no entity
  reads `gameTime`. **Confirms the 2026-10-11 Major: still open; only the wording was ever at
  stake.** The probe over `Entities/*.cs` returns the signature line as the only `gameTime`
  reference in every file. `Electrode`, `ScoreBurst`, `SkullMarker` and `RescueScoreMarker` ignore
  `field` as well.
  Why: SOLID-I — "Parameters documented as 'Unused (kept for the uniform shape)' are not allowed.
  Delete them", and "a type whose `Update(GameTime, PlayField)` uses neither argument … is in the
  wrong interface". Re-spelling the tag as "Not used." does not change the finding: the interface
  promises a clock no implementation reads, while TIME-1 mandates the clock-unit accumulator
  instead. Twenty-two classes and their tests carry a parameter that means nothing, and a reader
  reasonably assumes the argument matters somewhere.
  Fix: needs the author's decision on interface shape before any code moves (see question 1). The
  2026-10-10 and 2026-10-11 reviews both propose a tick-driven `Update()` for entities that count
  their own ticks, with the `PlayField` passed only to those that ask it questions. Whichever
  shape is chosen, it is one commit set per PROC-4, and it is the widest structural repair
  available: it touches every entity and every entity test at once.
```

```
[Major] TIME-3 src/Robotron2084/Entities/Grunt.cs:106, BerzerkRobot.cs:95, Player.cs:95 —
  `_random = random ?? new Random()`. **Confirms the 2026-10-11 Major: unchanged.**
  Why: TIME-3 — "No hidden randomness … `random ?? new Random()` defaults and `new Random()` at
  call sites make behaviour unrepeatable." A grunt's first wait, a Berzerk robot's aim and the
  player's jitter come from an unseeded source unless the caller remembers to pass one, so the
  production path is not reproducible and a test that forgets the argument silently stops testing
  anything fixed. `Enforcer` and `Spheroid` take a required `Random`, which proves the default is
  not needed — it is three entities diverging from five siblings.
  Fix: drop the `?? new Random()` and the `?` on the parameter at the three sites, and pass the
  field's seeded `Random` from `PlayField` and the spawners. `ScriptedRandom` already exists in
  `tests/` for the test side.
```

```
[Major] CMT-8 src/Robotron2084/Robotron2084.csproj — `GenerateDocumentationFile` is not set, so
  the `dotnet_diagnostic.CS1591.severity = error` line in `.editorconfig` can never fire.
  **Confirms the 2026-10-11 Major: unchanged — the probe still reports 102 `CS1570` errors, in the
  same 21 files.** 20 of them are `Level/`: WaveMaterialisation.cs (12) and
  GruntSpeedProgression.cs (12) worst, then FamilyList.cs (8), then WaveTable, WaveSurvivors,
  WaveStartSequence, TransportImage, ScoreValues, RobotTransporter, RobotKindInfo, PlayerSlot,
  PlayerAppear, LevelParameters, LevelParameterGenerator, LaserWallFlares, LaserWallFlare,
  GameSession, FinalScore, DifficultyTuning and BozoMode (4 each), plus
  Audio/Synthesis/CrowdRoarSound.cs (2). `tools/RobotronSoundPlayer/RobotronSoundPlayer.csproj:9`
  has the flag ON; the main project does not.
  Why: CMT-8 says "`GenerateDocumentationFile` must be on, so the compiler enforces all of this."
  With it off, the standard's largest section is unenforced by construction — which is how the
  missing summaries, the broken `cref`s and these 102 malformed remarks got in and stayed. In 20
  of the 21 files the shape is one `</item>` and one `</list>` pasted after a prose paragraph
  that has already closed its list (see Level/ScoreValues.cs:12-16), so the generated
  documentation for most of `Level/` is broken and the ROM provenance those remarks record is
  unreadable downstream. This is the root cause the clean-code-gist review identified at its
  "Always find root cause — Fail" row, still unfixed.
  Fix: `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in `Directory.Build.props`,
  so it applies to `src/` and `tests/` together. Clear the 102 `CS1570` errors first — mechanical:
  keep the prose paragraph inside `<remarks>` after the `</list>` and delete the stray
  `</item>`/`</list>`, and close the `<summary>` at CrowdRoarSound.cs:303. The 592 missing
  summaries and 6 dead `cref`s from the 2026-10-11 review are the rest of the same set and need
  the author's answer on folder order (question 2).
```

---

## Minor

```
[Minor] CON-1 + NAM-9 src/Robotron2084/Entities/{Grunt,BerzerkRobot,Brain,Gorf,Prog,Tank}.cs —
  six classes declare their interfaces without `IEntity`, relying on `IExplodable : IEntity`
  (Grunt.cs:25, BerzerkRobot.cs:28, Gorf.cs:34, Prog.cs:29, Tank.cs:27, Brain.cs:40); the other 17
  list `IEntity` first (Enforcer.cs:33 — `IEntity, IExplodable, IRemovable`).
  Why: two spellings of one inheritance list, in sibling files. A reader scanning the class
  declarations to answer "is this an IEntity?" must know that `IExplodable` already implies it,
  and the natural reading of the first six is that they are something other than entities. NAM-9
  is the whole-codebase version of this: a name should work the same way in every file.
  Fix: pick one. Dropping the redundant `IEntity` from the 17 is the smaller change and states
  the hierarchy once, at `IExplodable`.
```

```
[Minor] STR-5 + TEST-1 src/Robotron2084/Entities/Grunt.cs:207 — `IsFalling()` is `public`, its
  partner `BeginFall` (:215) is `internal`, and `IsFalling` has no caller outside `Grunt.cs`
  except `tests/…/Entities/GorfTests.cs:214, 220, 222 and :235`.
  Why: TEST-1 — test hooks are `internal`, labelled "(test hook)", and grouped last. A public
  query with no production caller reads as part of the entity's contract, so the next change
  treats it as one. The visibility split from `BeginFall` is the surprise: the command is
  internal, the question about it is public.
  Fix: `internal bool IsFalling()`, summarised "(test hook)", moved to sit with
  `WalkAnimationFrameNumber` and `GetAnimationFrameIndex` at the end of the class (STR-5: test
  hooks last, and together within their visibility).
```

```
[Minor] NUM-1 src/Robotron2084/Entities/Grunt.cs:233 and :245 — `MoveDelayBeats * 7 / 8` uses bare
  7 and 8, and `WaveSpeedTick(int floorBeats, int stepBeats = 4)` carries a magic default of 4.
  Why: NUM-1 makes every literal other than `0`, `1`, `-1` and `2`-when-halving a named constant
  whose summary says where it comes from. The 7/8 ratio is the ROM's — the remarks at :235
  describe the arcade's rounding — so it wants a name and an address, not a bare fraction. The
  `= 4` default is a tuning number sitting in a signature rather than in `Tuning/` or
  `GruntSpeedProgression`, which is where the wave's speed steps are otherwise decided, so a
  tuning change means editing a method declaration.
  Fix: `SpeedUpRatioNumerator = 7` and `SpeedUpRatioDenominator = 8` with the ROM cross-reference,
  and move the 4 into `GruntSpeedProgression` (or `SpawnTuning`) as a named constant, dropping the
  parameter default.
```

```
[Minor] STR-8 src/Robotron2084/Entities/Grunt.cs:165 — the grunt reads
  `GorfTuning.FallPixelsPerTick` to move itself.
  Why: STR-8 — "a calculation that only uses another type's sizes" is feature envy, and the rule's
  test is whether tuning this grunt's fall means opening another robot's file. It does. The remark
  at :214 already explains that the fall is the author's own addition for Gorf's drop (§138.2), so
  the coupling is intentional — but the tuning still sits on the wrong class.
  Fix: move the constant to `Grunt` (or to `Tuning/GruntTuning`, which does not yet exist) and say
  in its remarks that the fall comes from Gorf dropping the grunt.
```

```
[Minor] STR-3 / LSP src/Robotron2084/Entities/Hulk.cs:339 — `LifeState => EntityLifeState.Alive`
  is a constant, and the breach propagates into a tautology:
  `Level/Collisions/HulkVsHumanCollisionRule.cs:20` filters
  `entities.Hulks.FirstOrDefault(candidate => candidate.IsAlive() && scene.Touches(candidate, human))`,
  and `IsAlive()` cannot be false for a hulk.
  Why: an `IEntity` whose lifecycle state is a constant is an LSP substitution risk — `EntityList`
  `PruneDead` (EntityList.cs:49) and `GetLiveCount` (:43) are quietly no-ops for hulks, which
  is correct today but is not stated. The dead filter is the reader-facing harm: it says "a hulk
  can be dead at this point" in a place where none can be, so the next change reasons from a state
  that never occurs. The unkillable hulk is the ROM's (`RobotKinds.cs:63` records `HULKIL`
  knocking back instead of killing), so the constant is right.
  Fix: delete `candidate.IsAlive() &&` from the rule, and add one line to `Hulk`'s remarks: hulk
  lifecycle queries are always `Alive`, so pruning and live-counting do not apply to the list.
```

```
[Minor] .editorconfig:23-24 and :26-27 — the CS1591 block, comment and all, is declared twice.
  Why: harmless to the build, but the file is being appended to rather than edited, which is the
  same drift as the three accumulator idioms and the two playfield facades: the last thing written
  wins, and nobody reconciles it. It also misleads — a reader may assume the duplication means two
  different rules and go looking for the difference.
  Fix: delete the second block.
```

---

## Notes

```
[Note] CON-1 + NAM-9 Entities/{BerzerkRobot,CruiseMissile,Grunt,Prog,Human}.cs vs
  Tuning/{Enforcer,Spheroid,Tank,TankShell,Quark}Tuning.cs — the beat interval is declared inside
  the entity by five kinds (BerzerkRobot.cs:48 = 4, CruiseMissile.cs:45 = 3, Grunt.cs:49 = 4,
  Human.cs:65 = 16, Prog.cs:45 = 3) and in `Tuning/` by five others (EnforcerTuning.cs:10 = 4,
  QuarkTuning.cs:18 = 4, SpheroidTuning.cs:11 = 3, TankTuning.cs:12 = 3,
  TankShellTuning.cs:28 = 2). Quark mixes both: a `Tuning/` value cached into a private static
  (Quark.cs:65).
  Why: NUM-1 permits both homes — "Tunable values live in `Tuning/`. Values fixed by the ROM may
  live in the class that uses them" — so this may be a real distinction rather than drift. Nothing
  in the code or the remarks says which side each of these ten intervals falls on, and the values
  overlap (4 in both camps, 3 in both camps), so the split is indistinguishable from accident.
  Question for you: is the rule "ROM-fixed stays in the entity, tuned goes to `Tuning/`"? If so, a
  sentence in the standard or in `Tuning/`, and each interval's remarks naming its source, would
  let a reader tell the two apart — and would settle the accumulator form of the second Major
  entry at the same time.
```

```
[Note] SOLID-O src/Robotron2084/Entities/IWaveStartRobot.cs — Grunt (:201), BerzerkRobot (:172),
  Brain (:420), Hulk (:403) and Tank (:357) implement `IWaveStartRobot`. Spheroid, Quark and Gorf
  come onto the field at the start of a wave through `SpheroidWaveSpawner`, `QuarkWaveSpawner` and
  `GorfWaveSpawner` and do not implement it; `Quark.cs:169` sets
  `_beatTimer = BeatIntervalClockUnits` in the constructor instead.
  Why: the standard's SOLID-O wants new kinds to need additions, not judgement calls. Whether a
  new kind should implement this interface depends on which ROM object list it belongs to, and
  that fact is recorded nowhere the author of a new robot would look. `Enforcer` correctly does
  not implement it (no `WaveCount` row — it arrives mid-wave, dropped by a spheroid), which shows
  the split is meaningful rather than an oversight.
  Question for you: is membership the object-list-1 versus object-list-2 split, or should every
  robot present at wave start implement it? One sentence in `IWaveStartRobot`'s remarks would
  settle it either way.
```

```
[Note] STR-8 + CON-1 Entities/{Grunt:201, BerzerkRobot:172, Brain:420, Hulk:403, Tank:357} — the
  five `BeginPlay` bodies are one formula, and the interval is spelt three ways inside it:
      _beatTimer = <interval> - field.GetClockUnitsToFirstBeat(LivePollRomFrames,
                                                              FirstBeatNapRomFrames);
  Grunt, BerzerkRobot and Brain are identical; Hulk passes `0` as the nap; Tank inlines
  `ArcadeClock.ToClockUnits(TankTuning.BeatIntervalRomFrames)` in place of the cached name.
  `LivePollRomFrames` and `FirstBeatNapRomFrames` are re-declared per entity (Grunt.cs:37 and :46,
  BerzerkRobot.cs:38 and :45, Brain.cs:52 and :61, Hulk.cs:52, Tank.cs:42).
  Why: STR-8 wants a rule that several owners share to sit somewhere once. The per-kind poll and
  nap values are genuine ROM differences and must stay per kind — that part is right — but the
  arithmetic around them is copy-pasted, and Tank's divergence is exactly the accumulator drift the
  second Major entry describes.
  Fix: one helper — `field.SetBeatTimerForFirstBeat(...)` taking the interval, poll and nap, or a
  small `WaveStartTicks` value type — called from all five. `WaveStartTicks` already exists in
  `tests/` and could be the starting point for the shape.
```

---

## What is good, and what was deliberately not flagged

The entity layer's real problems are about time and permission. Everything else about it is
unusually disciplined, and four things are worth recording as worth keeping, because a cleanup
pass is exactly when they get lost:

- **STR-9 is held without exception.** Not one `LifeState` comparison outside
  `EntityLifeExtensions`, across 28 call sites in `Entities/`, `Level/` and
  `Level/Collisions/`. State meaning lives in one place, as the rule intends.
- **STR-12 is held without exception.** Every collision rule returns a `...Result` record and
  changes nothing; only `CollisionResponder` responds. The `yield return` laziness that lets the
  owner act between two reports (`LaserCollisionRule`, `RobotVsElectrodeCollisionRule`,
  `HulkVsHumanCollisionRule`) is the subtle part of that rule and it is implemented correctly.
- **The `!this.IsAlive()` guard idiom is uniform across all 23 entity types**, in `Draw`, `Update`
  and `Kill`. This is the single most-repeated shape in the entity layer and nobody diverged.
- **`RobotKinds` order is pinned by the type system's weakest link, a named test.**
  `GetInfo` is `All[(int)kind]`, so the enum order and the array order must agree — and the array
  order is simultaneously the laser-resolution order, which the class remarks instruct the reader
  to care about. `PlayFieldRobotRegistryTests.EveryKindHasExactlyOneRow_InTheEnumsOrder` asserts
  both halves and carries a comment saying why. That is the right way to hold a fragile coupling:
  noted here so nobody "simplifies" the test.
- **ROM provenance is the codebase's real asset and it is systematically recorded** — original
  source label and disassembly address on essentially every constant and entity. This review's own
  `IEntity`/`IExplodable`/`IWaveStartRobot` remarks are an example of the standard being met.
- **The `Human` / `HumanKind` identifiers keep their spelling by the author's decision**
  (glossary.md), and `AreEnemiesGone`'s hulk, electrode, prog and cruise-missile exclusions carry
  a correct `WVCHEK` / `$2A73` provenance. Neither is a naming finding; `AreEnemiesGone` is
  flagged only for the hand-written list.
- **`FieldEntities`' 18 `Add` overloads** (`:206-336`) are three lines each of one `_list.Add` and
  read as repetition. They are not flagged: they are overload-by-parameter-type, which is how the
  class picks the right list at compile time without a cast, and any `Add<T>` generic would need
  the dispatch back. A source generator could remove the typing; that is a bigger decision than
  this review should make.
- **`Gorf` and `BerzerkRobot` are the author's own robots** and say so where it matters
  (`Grunt.cs:214`, `Gorf.cs:20`, `RobotKinds.cs:156`). They have no ROM fidelity test and none is
  owed.
- **Two earlier findings were re-verified and are left to their existing entries** rather than
  restated: `ScoreBurst.cs:109` and `StripEffect.cs:166` build a fresh `IntVector2` in `Position`
  while the other 21 entities return a stored field (NAM-10, recorded on 2026-10-11 — worth
  noting only that the odd behaviour out is two of 23, so it is drift and not a design choice).

**Where the findings concentrate, in one sentence:** everywhere the compiler can see a rule this
codebase is clean — no suppressions, no clock literals, no column literals, no broad catches, no
`CA1502` breaches, warnings as errors, `CS8618` as error, tests mirroring `src/` — and everywhere
it cannot, the findings are. `TreatWarningsAsErrors` is already ON; the single missing
`GenerateDocumentationFile` line is what stops the compiler seeing the standard's largest section,
and six of the findings above are downstream of it or of the same write-last-wins drift.

---

## Questions for the author

1. Should `GameTime` come out of `IEntity.Update`? No implementation reads it and TIME-1 already
   mandates the accumulator, so removal collapses the widest SOLID-I breach in the codebase — but
   it touches all 22 entities and their tests, so it needs its own commit set per PROC-4, and the
   replacement interface shape is your call, as the 2026-10-10 and 2026-10-11 reviews both left it.
2. `GenerateDocumentationFile` into `Directory.Build.props`: approve, and in which folder order?
   The 102 `CS1570` errors are mechanical and can go first, on their own; the 592 missing
   summaries and 6 dead `cref`s are a decision about how much to take in one set.
3. During the wave-start hold and the player-death freeze, **does the beat timer accumulate?**
   Per ROM object list — the list the grunt, hulk, brain, prog and tank share, versus `$9813`, the
   list the spheroid, enforcer, quark, spark and shell share. `arcade-fidelity-notes.md` :1830
   shows the brain's halted state is its own branch (`NAP 4`, `BITA #$7F`) rather than its running
   loop with a flag tested, which points towards "does not accumulate"; whether the `$9813` list
   does the same is what the two entities would have to disagree with. §122/A01 settled the tank's
   tread this way already.
4. Is the beat interval a ROM-fixed value in some entities and a tuning value in others (see the
   first Note)? If yes, which of the ten is which — so the split can be written down and the
   accumulator form settled with it.
5. Should `Spheroid`, `Quark` and `Gorf` implement `IWaveStartRobot` (see the second Note)? Is the
   split the object-list one?
6. `field.Player.Position` or `field.GetPlayerPosition()` — which becomes the only route? The
   answer decides whether STR-11 gains an exemption for plain data reads or 23 call sites move.
