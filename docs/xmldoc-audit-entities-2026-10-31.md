# XML documentation audit: the Entities folder (2026-10-31)

This is a work list for a fixer, in the shape of [terminology-duplicates-2026-10-31.md](terminology-duplicates-2026-10-31.md). It checks the
XML documentation in `src/Robotron2084/Entities/` against the rules in [coding-standards.md](coding-standards.md): CMT-3, CMT-4, CMT-8,
CMT-9, CMT-10, CMT-11, CMT-12, CMT-13, CMT-15, and SOLID-I. It started with `Strip.cs`, whose one-line summary gave no units, no origin and no
`<param>` text; that file is already fixed. This list is for the rest.

## How this list was made

1. A script read all 676 documentation comments in the folder and applied the mechanical checks: summary length, ROM terms in a summary,
   numbers in a summary, "picture" and "image", history words, a remarks block that names the original source but not the disassembly, "Not
   used" parameters, and numbers with no unit anywhere in the name or the docs.
2. Every hit was then read in place. Most were not real (see "Looked at and fine" at the end). What is left is below.
3. Nothing in the code was changed by this audit.

The folder is in better shape than the first count (about 130 hits) suggests: the compiler already enforces that every member has a
`<summary>` and that `<param>` tags match, and the entity classes mostly follow the plain-English rules.

## Status (same day, after the fixes)

| Item | Result |
|---|---|
| E01 `StripClip` | **Done.** Every value has a `<param>`; `MaxX` and `MaxY` are documented as the first pixel outside (confirmed against `StripEffect.IsInside`, where a strip may end at the edge but not pass it). The rename to `MaxXExclusive` is still the author's to decide. |
| E02 ROM in a summary | **Done.** The four summaries are plain English; the sources are in `<remarks>`. |
| E03 long summaries | **Done for 32 of 36.** `Human.BeatIntervalArcadeFrames` is a marked deliberate deviation and stays. `Human.Steps`, `Tank.PickDestination` and `LaserSlots.Slots` were rewritten by hand under E04 and E05. Ten summaries lost their point in the first mechanical split (for example `Gorf.Update`) and were rewritten by hand. |
| E04 numbers | **Done.** `Human.Steps`, `Prog.ReOffsetThreshold256`, `Tank.PickDestination` now describe the value, and `Tank.PickDestination` links the constant. `Spheroid.AccelYRandomRange` was checked and left: it states a relation, not a value. |
| E05 weak summaries | **Done.** `HumanKind.Mommy` and `Daddy`, `Gorf.ShowNextAnimationFrame`, `LaserSlots.Slots` (the caller sentence is gone). |
| E06 both sources | **Done for all 11.** Each now has an `Original source:` bullet and a `Disassembly:` bullet. |
| E07 "Not used" parameters | **Still open**: a code decision for the author. |

How the E06 disassembly bullets were found. The original `.ASM` files are not in the repo, so the original-source labels are the ones the docs already
gave; none was changed. For each disassembly address the routine was found in `asm/robomame.asm` by name and its code was read to confirm it
does what the method does:

| Member | Disassembly |
|---|---|
| `CruiseMissile.ActOnBeat` | `ANIMATE_CRUISE_MISSILE` `$20B0` (countdown to a new direction, then two moves) |
| `Enforcer.Kill` | `ENFORCER_COLLISION_HANDLER` `$1483` (frees the object and makes an explosion) |
| `Enforcer.ActOnBeat` | `ENFORCER_AI` `$139D` (countdown to a new spot, countdown to a spark) |
| `Enforcer.MoveOnce` | `MOVE_ALL_SPHEROIDS_ENFORCERS_SPARKS_AND_DRAW_IF_ABOVE_RASTER_BEAM` `$DCFF`, twin at `$DD90` |
| `Tank.ActOnBeat` | `ANIMATE_TANK` `$4D99` (fire countdown, then the move) |
| `Grunt.Kill` | `GRUNT_COLLISION_HANDLER` `$3A76` (explosion, removal, score, speed-up) |
| `Human.IsGraspable` | `REMOVE_FAMILY_LIST_ENTRY` `$021C`, called first by `FAMILY_MEMBER_SAVED_KILLED_OR_PROGGED` `$033D` |
| `HumanKind` | `INITIALISE_FAMILY_MEMBERS` `$02B2`, handlers at `$0330`, `$0335`, `$033A` |
| `PlayerLaser.Update` | `LASER_HIT_WALL` `$3389` ("when we get here, the laser has hit a border wall") |
| `Quark.StartFlee` | `$4C6F` (X speed 0, Y speed plus or minus `$0200`), running on into `MAKE_QUARK_VAMOOSE` `$4C7F` |
| `EntityLifeState.Dying` | `KILL_PLAYER` `$30EF`, `ELECTRODE_DEATH` `$3AE0`, and `StripEngine` |
| `StripEffect.GetFanForShot` | `$5C1F`, behind the jump at `$5B43` ("create an explosion") |
| `StripEffect.GetStrips`, `StripFanAxis` | the entry tables `$5B40`, `$F000`, `$4680` (as `StripEngine` already says; `$F000` is the start of the horizontal code's jump table, whose first entry is the reset jump) |
| `StripClip` | "not separately labelled" (the edges were not found as a named constant) |

The audit script was re-run after the fixes: summaries of three or more sentences went from 36 to 1, a ROM reference in a summary from 4 to 0, a
remarks block with one source from 11 to 0, and a number in a summary from 3 to 1 (the checked one above).

## 0. Rules for whoever fixes these

1. Each item is `READY` (the standards decide, so you may fix it) or `DECIDE` (the author must choose; do not start it).
2. **A comment change and nothing else.** Do not change code. Build with 0 warnings after each file; the doc rules are enforced by the compiler.
3. **Do not guess a ROM label.** CMT-11 says to search `ref/original-source` and `asm/robomame.asm`, and to write "not separately labelled" if
   nothing is found. Never copy a label forward.
4. Write as CMT-11 says: plain English, a 10-year-old's reading age, one or two short sentences in a `<summary>`; the how goes in `<remarks>`.
5. Entity docs say nothing about who calls them (the `PlayField`, the tests). The words of the project are in [glossary.md](glossary.md); do not
   invent a term (CMT-6).
6. Commit per folder, staging files by name. Line endings are CRLF.

---

# Part 1. READY

## E01. `StripClip` has no `<param>` text and no unit (the same fault as `Strip`)

`StripClip(int MinX, int MaxX, int MinY, int MaxY)` documents none of its four values. The summary says "in arcade pixels across and in rows
down", which mixes two names for the same unit. The code that makes it (`CreateFromPortPixels`) divides `Rectangle.Right` and `Rectangle.Bottom`,
which are the first pixel *outside* the rectangle, so `MaxX` and `MaxY` are **exclusive**, and nothing says so.

*Fix:* add `<param>` for each value: the unit (arcade pixels from the left or top of the screen) and whether it is included. Rewrite the summary
without "rows". The `ROM: RRF.ASM's playfield-edge constants` line goes in a remarks list as an `Original source:` bullet with a `Disassembly:` bullet
or "not separately labelled" (see E06).
*Also:* the name `MaxX` hides that it is exclusive. That is a naming question for the author (`MaxXExclusive`, as `NewDestinationBeatsMaxExclusive`
is named elsewhere): **DECIDE**.

## E02. A ROM reference inside a `<summary>` (CMT-10, CMT-11)

Four summaries end in "(ROM: X)". A summary names the thing in plain words; the source belongs in `<remarks>`.

| Member | Move out of the summary |
|---|---|
| `CruiseMissile.ActOnBeat` | `(ROM: CMISL)` |
| `Enforcer.Kill` | `(ROM: ENFKIL)` |
| `Enforcer.ActOnBeat` | `(ROM: ENFR1)` |
| `Tank.ActOnBeat` | `(ROM: the TANK process)` |

*Fix:* put each in an `Original source:` bullet in `<remarks>`, and add the `Disassembly:` bullet or "not separately labelled" after searching.

## E03. A summary that describes the mechanism (CMT-10, CMT-11)

The summary should say what the thing is or does in one or two short sentences; the steps go in `<remarks>`. These run to three or more
sentences, or over 230 characters, because they list the steps. Most are `Update` and `ActOnBeat`, which CMT-8 says must say what that class's
beat or draw actually does (so keep that sentence, and move the steps).

`Hulk`: the type summary, `Update`, `ActOnBeat`, `PickDirection`, `TurnToNewDirection`.
`Spheroid`: `Update`, `GetAxisAfterMove`, `ActOnEscapeBeat`, `ActOnSpinEndBeat`, `StartEscape`.
`Human`: `Update`, `ActOnBeat`, `Steps` (see E04).
`Quark`: `Update`, `MoveOnce`, `StartFlee`.
`Tank`: `Update`, `PickDestination` (see E04), `StepOrBounce`.
`Gorf`: `Update`, `DropGrunts`.
`BerzerkRobot`: `GetWalkSequenceTowards`, `ActOnBeat`.
`Player`: `Update`, `UpdateFiring`.
`Prog`: `GetRandomDirection`.
`TankShell`: `Update`, `ActOnBeat`.
`ScoreBurst.Update`, `Spark.Update`, `StripEffect` (type summary), `StripEffect.StepAppear`, `StripEffect.LifeState`,
`StripEffect._getCentreStripIndex`.

*Fix:* keep the first sentence, move the rest to `<remarks>`. Do not change the meaning. Read the code before moving a sentence, because a few
are not wrong, only long.

## E04. A number that lives somewhere else (CMT-4)

| Member | Says | Fix |
|---|---|---|
| `Human.Steps` | "4 steps for each of the 8 ways" | say "a few steps for each way", or name the two constants |
| `Prog.ReOffsetThreshold256` | "about 3 beats in 100" | say what the threshold is a fraction of, with the constant's name |
| `Tank.PickDestination` | "About 38 times in 100 it is where the player is" | refer to the constant by name, as CMT-4 asks |
| `Spheroid.AccelYRandomRange` | "twice the sideways number ... 2 pixels wide ... 1 pixel tall" | the relation is right; check it does not repeat a value that has its own constant |

## E05. Summaries that only repeat the name, or that give a caller

| Member | Problem | Fix |
|---|---|---|
| `HumanKind.Mommy`, `HumanKind.Daddy` | "Mommy." and "Daddy." say nothing (CMT-8: a value says what it means) | "The mother of the family, as the attract screen calls her" or similar, in the author's words (NAM-15) |
| `Gorf.ShowNextAnimationFrame` | "Shows the next animation frame." repeats the name | say what it does to the sprite that is drawn, not what it is called |
| `LaserSlots.Slots` | "Tests and debugging tools use this." is a caller, and a test hook (CMT-9) | say what the list holds; if only tests read it, it is a test seam (see section 8 of the standards) |

## E06. A remarks block that names the original source but not the disassembly (CMT-11)

CMT-11 asks for both sources, or "not separately labelled" if it searched and found none. These give one:

`Enforcer.MoveOnce`, `Grunt.Kill`, `Human.IsGraspable`, `HumanKind` (and its use of a bare "ROM:" line), `PlayerLaser.Update`,
`Quark.StartFlee`, `EntityLifeState.Dying`, `StripEffect.GetFanForShot`, `StripEffect.GetStrips`, `StripClip` (see E01),
`StripFanAxis` (its remarks say "ROM: each has its own code", which is the old bare form).

*Fix:* search `asm/robomame.asm` for each and write the label or address; if there is none, write "not separately labelled". **Do not guess.**
Rewrite the three bare "ROM:" remarks (`HumanKind`, `StripFanAxis`, `StripClip`) in the list form the other classes use.

---

# Part 2. DECIDE (not a documentation problem)

## E07. Twenty-seven documented parameters are "Not used" (SOLID-I)

Almost every entity's `Update(GameTime gameTime, PlayField playField)` documents `gameTime` as "Not used. The grunt counts ticks."
(21 entities), and six of them also document `playField` as "Not used" (`Electrode`, `RescueBonusMarker`, `ScoreBurst`,
`SkullMarker`, `TankShell`, `StripEffect`). SOLID-I says: "Parameters documented as 'Unused (kept for the uniform shape)' are not allowed. Delete them." A
comment cannot fix this; the fix is in the code, in `IEntity.Update` and every implementer and caller.

*Why it is here:* the docs are accurate, and the standard is not being met. Deleting `gameTime` would touch `IEntity`, 21 entities, the
`PlayField`, and many tests. The author should decide whether to do it, and whether `Update` becomes `Update(PlayField playField)` or
something else. Until then the "Not used" lines stay.

---

# Looked at and fine

So nobody re-reviews them.

- **16 members with no `<summary>`**: they are `<inheritdoc />` overrides (`EntityList`, the `GetExplosion…` methods). That is allowed.
- **"used to", "no longer", "previously"** (10 hits): they describe what a thing *is* ("can no longer be hit", "the family member it used to
  be"), not the history of the code. CMT-3 is about the change history.
- **"The playfield ... " (2 hits)**: `Brain.BeginReprogramming` and `StripEffect.StepAppear` describe their own behaviour; no caller is named.
- **`IEntity.Draw`**: "Draws the entity where it is now." is generic, but it is an interface, and CMT-12 says an interface states the promise.
- **Fields named for their unit**: `BeatIntervalArcadeFrames` and the `…ArcadeFrameTimer` fields have short summaries that are right, because the
  name carries the unit.
- **`Human.BeatIntervalArcadeFrames`** ("different from the arcade's on purpose. Do not change it back"): CMT-1 allows a deliberate deviation marked
  "do not revert without asking".
- **A stray space before a full stop** in the scanner output ("on its beat .") comes from the way the script strips a link; the source is fine.

## What the author is asked to decide

1. E07: whether to delete the unused `Update` parameters, and what `Update` then looks like.
2. E01: `MaxX` and `MaxY` are exclusive; name them so (`MaxXExclusive`) or leave the name and document it.
