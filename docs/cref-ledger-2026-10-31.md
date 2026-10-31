# Broken cref audit: `<see cref>` links that do not resolve (2026-10-31)

This is a work list for a fixer, in the shape of
[xmldoc-audit-entities-2026-10-31.md](xmldoc-audit-entities-2026-10-31.md). It lists every `<see cref="…" />` in
`src/Robotron2084/` that the compiler cannot bind. The rules it applies are
[coding-standards.md](coding-standards.md) **CMT-8** ("`cref`s resolve", and "`GenerateDocumentationFile` must be on, so
the compiler enforces all of this"), **NAM-8** ("after a rename, the old word is gone
everywhere: identifiers, comments, **crefs** …") and the release check in §"Before a release" ("crefs point at things that
exist").

Every one of these is a comment that points at something that is not there. Each link is dead in the IDE and in the
generated `Robotron2084.xml`, so a reader following a link from a doc comment lands nowhere.

> **Update, same day (after the fixes).** A01–A30, B01–B03 and C02–C06 are **done**: 38 of the 39 links now bind.
> **C01 is still open**, because it is `DECIDE`: the author has to say whether the sentence means
> `SpriteCollision.GetShape` or `SpriteMask.Overlaps`. The items below are left as they were written, because they are the
> record of what was found.
>
> How it was checked: the build command in §0 now prints one `CS1574` only, C01; the tests pass (807 passed, 1 skipped —
> the pre-existing skip for the ROM that is not in `ref/rom`); and the edit was compared line by line against the state
> before it, which shows **33 changed lines, every one of them a `///` comment line** — no code line moved, and the CRLF
> line endings of the repo were kept. Not committed yet: the working tree already held unrelated uncommitted work, so the
> three group commits (A, B, C) are still to be made on a clean tree.

## How this list was made

1. The project was built with documentation generation forced on, because `GenerateDocumentationFile` is **off** in
   `Directory.Build.props` — in a normal build the compiler never binds a single `cref`, which is how these links were
   allowed to rot:

   ```
   dotnet build src/Robotron2084/Robotron2084.csproj -c Debug --no-incremental \
     -p:GenerateDocumentationFile=true -p:TreatWarningsAsErrors=false -p:NoWarn=1591 -v:n
   ```

   `--no-incremental` is needed: without a real recompile no `CS1574` is emitted at all.
2. Every `warning CS1574` was collected: **39 broken links in 26 files** (no `CS1580`, `CS1658` or ambiguous-`cref`
   warnings). `tests/` and `tools/` were built the same way and are clean.
3. Each hit was read in place and the right target was found by reading the class that owns the name, and by checking the
   member list in the generated `src/Robotron2084/bin/Debug/net10.0/Robotron2084.xml`.
4. Nothing in `src/` was changed by this audit. The "Should point to" column was **proved**, not guessed: all 39
   replacements were applied in a scratch copy of the repo and rebuilt the same way — `CS1574` went to **0** and the build
   succeeded.

Line numbers are from the working tree on 2026-10-31. Find each place by the quoted text, not by the number.

## Why they broke

| Cause | Links |
|---|---|
| The timer fields were renamed `…Timer` → `…ArcadeFrameTimer` (the NAM-26 change recorded in
  [refactoring-ledger.md](refactoring-ledger.md) §2026-10-31, which lists exactly these fields); the doc comments were left
  behind — a NAM-8 break. | 30 |
| The namespace `Robotron2084.FieldEntities` was retired: entities live in `Robotron2084.Entities`, and `FieldEntities` is now a class in `Robotron2084.Level`. | 3 |
| The linked member was renamed, or was never named that. | 6 |

---

## 0. Rules for whoever fixes these

1. **Each item has a status.** `READY` means the standards decide, so you may fix it. `DECIDE` means the **author** must
   pick the target; do not start it.
2. **A `cref` change and nothing else.** Do not reword the sentence, do not touch code, do not reformat lines you did not
   need to change.
3. **One commit per group** (A, B, C), with the group name and the item IDs in the message, e.g.
   `crefs: bind the renamed timer fields (A01-A30)`.
4. **Check with the compiler**, not by eye:

   ```
   dotnet build src/Robotron2084/Robotron2084.csproj -c Debug --no-incremental \
     -p:GenerateDocumentationFile=true -p:TreatWarningsAsErrors=false -p:NoWarn=1591 -v:n | grep "warning CS1574"
   ```

   The group is done when this prints nothing, and the tests still pass.

---

## A. Timer fields renamed to `…ArcadeFrameTimer` — **READY · done** (30 links)

The field in the doc comment does not exist; the class holds the `…ArcadeFrameTimer` name (the rename is in
[refactoring-ledger.md](refactoring-ledger.md) §2026-10-31, "every object is named with its whole type name"). All 30 are
one text substitution inside `cref="…"` on the line named.

| ID | File | Line | Broken `cref` | Should point to |
|---|---|---|---|---|
| A01 | `src/Robotron2084/Entities/BerzerkRobot.cs` | 14 | `_beatTimer` | `_beatArcadeFrameTimer` |
| A02 | `src/Robotron2084/Entities/Brain.cs` | 19 | `_beatTimer` | `_beatArcadeFrameTimer` |
| A03 | `src/Robotron2084/Entities/Brain.cs` | 20 | `_reprogramTimer` | `_reprogramArcadeFrameTimer` |
| A04 | `src/Robotron2084/Entities/CruiseMissile.cs` | 17 | `_beatTimer` | `_beatArcadeFrameTimer` |
| A05 | `src/Robotron2084/Entities/Electrode.cs` | 16 | `_shrivelTimer` | `_shrivelArcadeFrameTimer` |
| A06 | `src/Robotron2084/Entities/Enforcer.cs` | 17 | `_beatTimer` | `_beatArcadeFrameTimer` |
| A07 | `src/Robotron2084/Entities/Enforcer.cs` | 18 | `_moveTimer` | `_moveArcadeFrameTimer` |
| A08 | `src/Robotron2084/Entities/Enforcer.cs` | 18 | `_growTimer` | `_growArcadeFrameTimer` |
| A09 | `src/Robotron2084/Entities/Gorf.cs` | 16 | `_beatTimer` | `_beatArcadeFrameTimer` |
| A10 | `src/Robotron2084/Entities/Gorf.cs` | 16 | `_animationTimer` | `_animationArcadeFrameTimer` |
| A11 | `src/Robotron2084/Entities/Grunt.cs` | 14 | `_beatTimer` | `_beatArcadeFrameTimer` |
| A12 | `src/Robotron2084/Entities/Hulk.cs` | 16 | `_beatTimer` | `_beatArcadeFrameTimer` |
| A13 | `src/Robotron2084/Entities/Human.cs` | 17 | `_beatTimer` | `_beatArcadeFrameTimer` |
| A14 | `src/Robotron2084/Entities/Player.cs` | 18 | `_deathTimer` | `_deathArcadeFrameTimer` |
| A15 | `src/Robotron2084/Entities/Prog.cs` | 17 | `_beatTimer` | `_beatArcadeFrameTimer` |
| A16 | `src/Robotron2084/Entities/Quark.cs` | 17 | `_beatTimer` | `_beatArcadeFrameTimer` |
| A17 | `src/Robotron2084/Entities/Quark.cs` | 18 | `_moveTimer` | `_moveArcadeFrameTimer` |
| A18 | `src/Robotron2084/Entities/RescueBonusMarker.cs` | 17 | `_lifeTimer` | `_lifeArcadeFrameTimer` |
| A19 | `src/Robotron2084/Entities/ScoreBurst.cs` | 14 | `_stepTimer` | `_stepArcadeFrameTimer` |
| A20 | `src/Robotron2084/Entities/SkullMarker.cs` | 14 | `_lifeTimer` | `_lifeArcadeFrameTimer` |
| A21 | `src/Robotron2084/Entities/Spark.cs` | 13 | `_beatTimer` | `_beatArcadeFrameTimer` |
| A22 | `src/Robotron2084/Entities/Spark.cs` | 14 | `_moveTimer` | `_moveArcadeFrameTimer` |
| A23 | `src/Robotron2084/Entities/Spark.cs` | 14 | `_lifeTimer` | `_lifeArcadeFrameTimer` |
| A24 | `src/Robotron2084/Entities/Spheroid.cs` | 16 | `_beatTimer` | `_beatArcadeFrameTimer` |
| A25 | `src/Robotron2084/Entities/Spheroid.cs` | 17 | `_moveTimer` | `_moveArcadeFrameTimer` |
| A26 | `src/Robotron2084/Entities/StripEffect.cs` | 12 | `_stepTimer` | `_stepArcadeFrameTimer` |
| A27 | `src/Robotron2084/Entities/Tank.cs` | 15 | `_beatTimer` | `_beatArcadeFrameTimer` |
| A28 | `src/Robotron2084/Entities/Tank.cs` | 15 | `_growTimer` | `_growArcadeFrameTimer` |
| A29 | `src/Robotron2084/Entities/TankShell.cs` | 14 | `_beatTimer` | `_beatArcadeFrameTimer` |
| A30 | `src/Robotron2084/Entities/TankShell.cs` | 14 | `_moveTimer` | `_moveArcadeFrameTimer` |

`Human.cs:18` already links `_firstStepTicksRemaining` correctly, which is the proof that a `cref` to a private field of
the same class does bind: these 30 fail only because the name is the old one.

## B. The retired namespace `Robotron2084.FieldEntities` — **READY · done** (3 links)

| ID | File | Line | Broken `cref` | Should point to |
|---|---|---|---|---|
| B01 | `src/Robotron2084/Core/ArcadeFrameTimer.cs` | 39 | `Robotron2084.FieldEntities.IEntity.Update(Microsoft.Xna.Framework.GameTime, Robotron2084.Level.PlayField)` | `Robotron2084.Entities.IEntity.Update(Microsoft.Xna.Framework.GameTime, Robotron2084.Level.PlayField)` |
| B02 | `src/Robotron2084/Graphics/TextureSet.cs` | 178 | `Robotron2084.FieldEntities.CruiseMissile` | `Robotron2084.Entities.CruiseMissile` |
| B03 | `src/Robotron2084/Tuning/CollisionSizes.cs` | 83 | `FieldEntities.Prog.GetExplosionBounds()` | `Robotron2084.Entities.Prog.GetExplosionBounds` |

B03 needs the whole namespace: `CollisionSizes.cs` has no `using Robotron2084.Entities;`, so `Prog.GetExplosionBounds`
alone would not bind. The generated XML has the member as `M:Robotron2084.Entities.Prog.GetExplosionBounds`, so the empty
argument list is dropped.

## C. The linked member was renamed, or was never that name — 6 links (C02–C06 done, C01 open)

| ID | Status | File | Line | Broken `cref` | Should point to |
|---|---|---|---|---|---|
| C01 | **DECIDE · open** | `src/Robotron2084/Graphics/BlitterDraw.cs` | 60 | `SpriteMask.Overlap` | `Robotron2084.Graphics.SpriteCollision.GetShape` — that is the pixel-perfect path that calls `GetDrawnBounds` (line 21 of `SpriteCollision.cs`). If the author means the mask comparison instead, the member is `Robotron2084.Graphics.SpriteMask.Overlaps`, which takes the drawn bounds from its caller and does not read `GetDrawnBounds`. |
| C02 | **READY · done** | `src/Robotron2084/Graphics/TextureSet.cs` | 331 | `TitleWordmarkCore` | `TitleWordmarkCoreTexture` |
| C03 | **READY · done** | `src/Robotron2084/Graphics/TextureSet.cs` | 332 | `TitleWordmarkRim` | `TitleWordmarkRimTexture` |
| C04 | **READY · done** | `src/Robotron2084/Graphics/TextureSet.cs` | 339 | `TitleWordmarkCore` | `TitleWordmarkCoreTexture` |
| C05 | **READY · done** | `src/Robotron2084/Level/GameSession.cs` | 65 | `Controls` | `ControlSettings` — the sibling property `GameSession.ControlSettings` (line 37, type `Robotron2084.Input.ControlSettings`), which the sentence compares the game settings with. There is no `Controls` in the class. |
| C06 | **READY · done** | `src/Robotron2084/Program.cs` | 17 | `DisplayInfo` | `Robotron2084.Core.DisplayInfo` — the name alone cannot bind: `Program.cs` has no `using Robotron2084.Core;`. |

---

## Found on the way, and not in this list

The same build emits other doc-comment warnings. They are not `cref`s, so they are not listed item by item, but they are
recorded here so the next audit does not have to re-find them.

- **`CS1570` (XML comment is not well formed): 19 comments, 37 warning lines.** In 18 of them an `<item>` closes a
  `<remarks>` (a `<list type="bullet">` opened with no `<list type="bullet">` close, so `</item>` ends the wrong element):
  `Level/BozoMode.cs:20`, `Level/GruntSpeedProgression.cs:29`, `:286`, `:321`, `Level/LaserWallFlare.cs:16`,
  `Level/PlayerAppear.cs:23`, `Level/PlayerSlot.cs:21`, `Level/RobotKindInfo.cs:22`, `Level/RobotTransporter.cs:26`,
  `Level/ScoreValues.cs:14`, `Level/WaveMaterialisation.cs:39`, `:235`, `:407`, `Level/WaveParameterGenerator.cs:23`,
  `Level/WaveParameters.cs:24`, `Level/WaveStartSequence.cs:27`, `Level/WaveSurvivors.cs:19`, `Level/WaveTable.cs:23`.
  The other is `Audio/Synthesis/CrowdRoarSound.cs:337` ("Expected an end tag for element 'summary'").
- **`CS1572` / `CS1573` (`<param>` tags do not match the parameters): 5 methods.**
  `Graphics/WilliamsLogoBorder.cs:87` (`logo` → `logoSpriteMask`), `Level/PlayfieldWall.cs:31` (`cycle` →
  `wallColourCycle`), `Level/SpawnPlacement.cs:85`, `:107`, `:116` (`point` → `avoidedPosition`).
- **`CS1591` (no XML comment on a public member): 295 members.** Biggest groups: `Tuning/HudLayout` (29),
  `Hud/HighScoreTableLayout` (29), `Graphics/TextureSet` (17), `AttractMode/MovieAnimation` and
  `AttractMode/MovieDescriptors` (16 each), `Tuning/CollisionSizes` (16), `Tuning/PlayerTuning` (15); the rest are spread
  over `Tuning/`, `States/`, `Input/`, `Core/` and `Palette/`. `GenerateDocumentationFile` being off means nothing
  enforces it.

## The hole that let these in

`GenerateDocumentationFile` is off, so `TreatWarningsAsErrors` never sees a doc warning: not one `CS1574`, `CS1570`,
`CS1572` or `CS1591` can fail a build today. NAM-8 says the old word must go everywhere including `cref`s, and CMT-8 says
`cref`s resolve — and CMT-8 already requires `GenerateDocumentationFile` to be on, which `Directory.Build.props` does not
do. Turning it on would have caught all 39 at
the commit that renamed the timers; it would also turn the 295 `CS1591` gaps into build failures, so it needs those
summaries first, or `CS1591` in `NoWarn`.
That is an author decision, so it is not an item here.
