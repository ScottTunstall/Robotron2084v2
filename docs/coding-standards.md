# Coding standards and code review rules

These rules are for everyone who writes or reviews code in this repository, people and AI agents alike.
Part 1 is the standard. Part 2 is a review procedure built on it, written so it can be used as a
code-review skill. Every rule has an ID so a review finding can cite it (`NUM-2`, `CMT-3`).

The rules restate and extend the author's directives in `docs/arcade-fidelity-notes.md` §99, §109, §111-114,
§120 and §121. Where a rule comes from a directive, the section is given.

**The rule above every other rule (§114): readability for humans.** If a reader needs three comments to
follow ten lines, the code is wrong, not the reader. When a rename and a comment would both fix a confusion,
rename. When a constant and a comment would both explain a number, write the constant.

---

# Part 1: The standard

## 1. Vocabulary: one word per concept

A reviewer should reject a new synonym for any term in this table.

### Time

| Term | Meaning | Identifier suffix |
|---|---|---|
| **port tick** | One call of `Update`: 1/60 s, fixed timestep | `...Ticks` |
| **ROM frame** | One arcade vblank, the unit the ROM's `NAP n` counts in. 1 ROM frame = 6/5 port ticks | `...RomFrames` (never `...RomTicks`) |
| **clock unit** | The whole-number unit both fit into: a port tick is 5, a ROM frame is 6 (`ArcadeClock`) | `...ClockUnits`, or a field named `_...Timer` whose summary says "clock units" |
| **beat** | One pass of an entity's own update routine, every N ROM frames | `...Beats` |

Never call the clock unit "fifths", "sixths" or "6ths". All three have been used, for the same thing.

### Space

| Term | Meaning | Identifier suffix |
|---|---|---|
| **arcade pixel** | A pixel of the ROM's 304x256 screen | `...ArcadePixels` |
| **column** | The ROM's horizontal address unit: **2 arcade pixels** (video memory is `column*256 + row`, 2 px per byte, §113) | `...Columns` |
| **row** | The ROM's vertical address unit: 1 arcade pixel | `...Rows` |
| **spec pixel** | A pixel of spec.txt's 320x200 layout, the input of `ScreenSize.Scaled` | `...SpecPixels` |
| **port pixel** | A pixel of the 640x400 canvas the game draws into | `...PortPixels` |
| **canvas pixel** | A window pixel after the canvas is scaled to fit | (only in `Presentation`) |
| **subpixel** | 1/256 of a pixel, the ROM's 16-bit coordinate fraction | `...Subpixels` |

"Column" in the sense of a table's layout column ("5 per column") must be named something else, e.g. `ListColumn`.

### Game terms

| Use | Not | Meaning |
|---|---|---|
| **animation frame** | art, image, frame (alone) | One picture of an entity's animation (§121). "Frame" alone means a ROM frame. |
| **picture** | sprite (in prose) | The ROM's word for a bitmap it blits. |
| **palette slot** / **slot** | colour index, colour | One of the 16 palette entries (0-9 static, 10-15 cycling). |
| **strip explosion** | explosion (ambiguous) | The RRDX2 fan-apart death effect. |
| **appear** / **materialise** | spawn-in, warp-in | The wave-start strips converging onto a robot. |
| **death burst** | score burst | The spheroid/quark `CIRKP` effect (pending the author's decision). |
| **reprogramming** | conversion, mutation | A brain turning a human into a prog (ROM `BMUT`). |
| **family** / **human** | civilian, humanoid | Mikey, Mom and Dad. |
| **laser** (the player's) / **spark**, **shell**, **cruise missile** (enemy shots) | bullet, projectile, missile (alone) | |
| **Shoot** | Aim, Fire (as a direction) | The second stick. "Fire" is the act of creating a laser. |
| **Kill** | Deactivate, Destroy, Remove | Taking an entity off the field. |

## 2. Structure and responsibility (§114, §120, §121)

**STR-1. One reason to change per class.** If a class summary needs "and" to describe what it does, split it.
Warning signs: more than ~400 lines, more than ~15 fields, or `// ---- section ----` comments that divide it
into jobs. `PlayField`, `GameplayConstants` and `SpriteSet` are the counter-examples.

**STR-2. One reason to change per method, and a verb phrase that names it.** A method whose body needs comment
headers ("// 1. ...", "// 2. ...") is several methods. Cyclomatic complexity above 15 is a **build error**
(CA1502). Split the method. Never suppress the rule.

**STR-3. One top-level type per file (§121).** This includes records, enums and small structs. A nested type is
allowed only when it is private and used by its enclosing type alone. `PlayField.LaserWallFlare` is internal
and exposed through a property, so it breaks this rule.

**STR-4. Dependencies point inward.** `Core` depends on nothing in the game. `Rendering` does not depend on
`Hud` or `States`. Entities do not reach into `States`. A new `using` that points outward needs a reason in
the review.

**STR-5. Member order in a class:** constants → static fields → instance fields → constructor(s) → public
properties → public methods (`Update` before `Draw`) → private helpers in the order the public methods call
them → `CurrentAnimationFrame` → test hooks, last and together.

**STR-6. A registry must be the only place.** If a type claims "add a row here and nothing else"
(`RobotKinds`), then nothing else may list the kinds by hand. A hand-written list elsewhere is a bug waiting to
happen, even when it is right today.

## 3. Naming (§114)

**NAM-1. Methods are verb phrases in the domain's own words**: `RollOffsets`, `PickDirection`,
`AdvanceReprogramming`. Never `Process`, `Handle` or `Do` alone, never `Step1`, and never a bare verb with no
object (`Burst`, `Dispatch`).

**NAM-2. Numeric identifiers carry their unit** when the unit is not obvious, using the suffixes in section 1:
`StepPeriodRomFrames`, `JitterColumns`, `_velocitySubpixels`. A name that says one unit while holding another is
a defect: `StepColumns` holding port pixels, `...RomTicks` holding beats.

**NAM-3. A name says what the value is, not what it once was or how it is used.** `HulkSpeed` for a delay
(where bigger is slower) is wrong. `_wallPalette` for the whole palette is wrong.

**NAM-4. Booleans read as predicates**: `IsHitStopActive`, `HasMen`, `CanFireShell`. A state that is true
*while held* is `...Held`, not `...Pressed`.

**NAM-5. No abbreviations** except `X`/`Y`, `Rom`, well-known ones (`Id`, `Hud`), and ROM labels quoted as ROM
labels. `Len`, `Dur`, `Pc`, `Rprog`, `gw`/`gh` and `reDir` are not allowed.

**NAM-6. Single letters only as loop indices** or as the lambda parameter of a one-line LINQ call.

**NAM-7. Near-synonyms must mean different things.** If two members are called `IsFrozen` and `RobotsFrozen`,
the difference must be visible in the names.

**NAM-8. After a rename, the old word is gone everywhere**: identifiers, comments, crefs, test names, file
names. Check with a solution-wide search and `git status` (§121: a stale untracked file survived a rename and
compiled).

## 4. Numbers and units (§112, §113)

**NUM-1. No magic numbers.** Every literal other than `0`, `1`, `-1` (and `2` when halving) is a named constant.
Its `<summary>` says **what it is** and **where it comes from**: a ROM routine and address, a measurement, or
"the author's tuning". Tunable values live in `Tuning/`. Values fixed by the ROM may live in the class that
uses them.

**NUM-2. The clock is never written as `5` and `6`.** Use `ArcadeClock.UnitsPerPortTick` and
`ArcadeClock.UnitsPerRomFrame` (or `RomFrameTimer`). Never copy them into a private constant.

**NUM-3. The column is never written as `2`.** Use `ScreenSize.Columns(n)` / `ArcadePixelsPerColumn`.

**NUM-4. Unit conversions go through named helpers** (`ScreenSize.Scaled`, `ScreenSize.ArcadePixels`,
`ScreenSize.Columns`, `ArcadeClock.Units`), never through inline arithmetic like `* ScreenSize.SpecScale`,
`>> 8` or `* 256`. Name the subpixel scale once (`SubpixelsPerPixel`).

**NUM-5. Store values in the unit the source gives them**, and convert at the use site. Do not pre-multiply
into port pixels: `TankBirthOffsetX = 8`, meaning "2 columns in port pixels", is the thing to avoid.

**NUM-6. Derive, don't restate.** `640f / 304f` is `ScreenSize.Width / ArcadeScreenWidth`. A number that can
be computed from other constants must be.

**NUM-7. Public static arrays are read-only in fact, not just in name.** Expose ROM tables as
`IReadOnlyList<T>` or `ImmutableArray<T>`, because a `static readonly int[]` can still be written to by any
caller.

## 5. Time and state

**TIME-1. One timer idiom.** Periodic work uses the clock-unit accumulator (add `UnitsPerPortTick`, compare to
`Units(period)`, subtract and carry). Do not introduce `TimeSpan`/`GameTime` timers in gameplay code.
`Player`'s start grace is the exception to fix, not to copy.

**TIME-2. `PortTicks(romFrames)` truncates** and fires up to a tick early. Use it only for one-shot display
durations, and say so in the caller's comment.

**TIME-3. No hidden randomness.** Gameplay classes take a `Random` in their constructor. `random ?? new Random()`
defaults and `new Random()` at call sites make behaviour unrepeatable. Tests pass a seeded `Random`.

**TIME-4. Each field has one meaning.** A counter bumped in two places for two different reasons is a bug
(`Tank._animationTicks`).

## 6. Comments and documentation (§109, §111)

**CMT-1. The code explains the system. Comments say only what the code cannot:**
- the ROM cross-reference: `// ROM: RRC11.ASM ENFSHT` or `ROM: R5 $4F8C` (mandatory where a method maps to a
  ROM routine, §109.7);
- arithmetic whose operands do not show the reason (the explosion's diagonal lean, a fixed-point shift);
- *why* a surprising choice was made, when the answer is not "the ROM does it" (e.g. a deliberate deviation,
  marked "do not revert without asking").

**CMT-2. A comment must not restate the line below it.** `_frameStep = 0; // restart the walk` is noise.

**CMT-3. No history in code.** No dates, playtest rounds, author quotes, "used to", "the old guard", "this
fixed the bug where...", or "was wrong". The code says what is true now. The story belongs in git history
and in the notes. A short `(notes §NN)` pointer is allowed when the reasoning is long.

**CMT-4. No values that live elsewhere.** Do not write a constant's value, a key binding, a score or a count
into a comment ("flies at 12 px/tick", "WASD", "(25 points)", "40 in all"). Name the constant, which cannot go
stale.

**CMT-5. No plan or milestone references** ("Phase 11.3", "PHASE D", "plan 9.1", "M4"). Say what the thing is.

**CMT-6. No invented vocabulary (§111).** Use the ROM's names for ROM things (`SPWAKE`, `NAP`) and the
glossary's words for everything else. "Wake up" is out; the periodic activation is a **beat**.

**CMT-7. No tombstones.** Do not write "(No XyzBlinkTicks: ...)" about code that does not exist. If the fact
matters, put it in the summary of the member that behaves that way ("dies at once; no death animation").

**CMT-8. Every member has a `<summary>` (§109.1)**: public, internal and private members, constructors
included. `<param>` tags match the real parameters exactly: no stale tags, none missing. `cref`s resolve.
One `<summary>` per member. `GenerateDocumentationFile` must be on, so the compiler enforces all of this.

**CMT-9. A "test hook" label must be true.** If production code calls a member, it is not a test hook. Rename
it and document its real use.

**CMT-10. TODOs are tracked.** "Not built yet" and "open item" text in a comment must name the tracking item
(handoff row, notes section). If the thing has been built, delete the text.

## 7. SOLID in this codebase

**SOLID-S.** See STR-1 and STR-2.

**SOLID-O.** New robot kinds, new states and new sounds should need additions, not edits spread across the
codebase. A `switch` over a kind enum outside its registry (`PlayField.ListOf`) is acceptable only if it is
the single mapping, and its exception message says where to add the new case.

**SOLID-L. No downcasts from an interface to a concrete type** in callbacks (`((Hulk)target)`,
`((IRemovable)target)`). If a callback needs a capability, the type system should guarantee it. Where that
is not possible, pattern-match and throw an `InvalidOperationException` that names the type.

**SOLID-I. Do not implement interface members you ignore.** A type whose `Update(GameTime, PlayField)` uses
neither argument, or whose `Position`/`Bounds` mean nothing, is in the wrong interface. Parameters documented
as "Unused (kept for the uniform shape)" are not allowed. Delete them.

**SOLID-D. Depend on the narrowest thing that works.** Do not add new static mutable state or service
locators (`Sound`, `DevKeys.AttractFastForward` are the existing ones to shrink, not grow). Pass
collaborators in through the constructor. Read hardware input only through `IPlayerInputSource` or an
injected snapshot, not `Keyboard.GetState()` inside a state.

## 8. Test seams and dead code

**TEST-1. Test hooks are `internal`, labelled "(test hook)" in their summary, and grouped last in the class
(STR-5).** One idiom per need: position overrides are `internal void TeleportTo(...)`, never an
`internal set` on a public property.

**TEST-2. Production defaults never favour tests.** A constructor parameter like
`playerInvincibleForTesting = true` means every production caller that forgets it gets test behaviour. Test-only
switches have no default, or default to production behaviour.

**TEST-3. Temporary switches are marked temporary and tracked.** They are in a dev-only place (a dev key, a
`#if DEBUG` branch, a settings flag), and each one names its tracking item.

**TEST-4. Tests mirror `src/`.** A test for `Rendering/GamePalette` lives in `tests/.../Rendering/`. Test file
and method names describe behaviour, never plan phases (`PlayFieldPhaseETests` is the counter-example).

**TEST-5. Shared setup goes in a builder** (`PlayFieldBuilder`), not a private `CreateField` copied into each
test file.

**DEAD-1. Delete unused code.** That covers unused types, members and constants, "legacy" fields kept "for
anything still reading them", and parameters kept for API compatibility in an application with no external
API. Git remembers them.

**DEAD-2. No speculative generality.** Do not add a CSV override, a "future simultaneous mode" field or an
extension point before there is a use for it. Selectable features that are not built must not be selectable.

## 9. Errors, formatting, consistency

**ERR-1. Catch only what you can handle**: `IOException`, `JsonException`, `FormatException`. Never catch
`Exception` just to return a default, because a silent fallback that later overwrites the user's file is data
loss.

**FMT-1. One declaration per line.** A `///` comment starts its own line and sits directly above its member.
No blank line directly after an opening brace. Use target-typed `new()` or `var` consistently within a file.

**CON-1. One idiom per concept across `src/` (§114).** If one entity guards `Update` with `!= Alive`, they all
do (unless the entity has a `Dying` state). If the timer unit is a constant in one file, it is the same constant
in every file. Before writing a new helper, search for an existing one that does the same job.

**CON-2. Same name, same meaning; different meaning, different name.** Two constants with the same value for
the same ROM string (`GameOverTextColumn` / `GameOverMessageColumn`) are one constant.

## 10. Process

**PROC-1. Not a licence to churn (§114).** Do not reformat or sweep-rename code without a reason. Any file you
open for another reason gets these rules applied while it is open.

**PROC-2. A clean build and green tests are necessary but not enough.** Neither can see stale comments,
leftover files or dead code (§120, §121). Run the scans in Part 2.

**PROC-3. Behaviour changes are separate commits** from refactors, each with a test that fails before and
passes after.

---

# Part 2: Code review procedure (for use as a review skill)

## When to use

Use this for reviewing a diff, a branch or a set of files in this repository. The target is the changed code
**plus the rest of every file it touches**, because PROC-1 applies the standard to any file that is open.

## Steps

1. **Establish scope.** List the changed files (`git diff --name-only <base>`) and include untracked files
   (`git status --porcelain`). An untracked `.cs` file compiles, so review it too.
2. **Build and test**: `dotnet build Robotron2084.slnx -c Debug`, the same with `-c Release`, then
   `dotnet test Robotron2084.slnx`. Record any failure as a finding. Do not stop the review.
3. **Run the mechanical scans** below on the scoped files, and on `src/` for rename checks. Every hit is either
   a finding or has a one-line justification.
4. **Read each changed file top to bottom** against Part 1, in this order: correctness (does it do what its
   summary says?), SOLID and structure (STR, SOLID), numbers and units (NUM, TIME), naming (NAM, glossary),
   comments (CMT), consistency with sibling files (CON; open at least one sibling, e.g. another entity,
   and compare).
5. **Check every comment and summary in the changed regions against the code it describes**, value by value.
   Stale comments are this codebase's most common defect. Confirm that numbers in prose match constants, keys
   in prose match bindings, "not built yet" is still true, and crefs point at things that exist.
6. **Check the claims made by registries and "single source of truth" comments** (STR-6): search for hand-written
   lists that duplicate them.
7. **Write the findings** in the format below. Rank them most severe first.

## Mechanical scans

Run these from the repository root (Git Bash). Scope them to changed files where you can.

```bash
# One top-level type per file (STR-3), §121
for f in $(git ls-files 'src/*.cs' 'tests/*.cs'); do
  n=$(grep -cE '^(public|internal)\s+((sealed|abstract|static|readonly|partial|record)\s+)*(class|record|enum|interface|struct)\b' "$f")
  [ "$n" -gt 1 ] && echo "$f: $n types"; done

# Clock literals (NUM-2)
grep -rnE '(\+=|-=) 5;|\* 6\b|>= 6\b|< 6\b|= 6;|SixthsPer|Fifths|_sixths|_fifths' src --include=*.cs

# Column literal and hand-rolled scaling (NUM-3, NUM-4)
grep -rnE 'Scaled\(2\b|Scaled\(2 \*|\* ScreenSize\.SpecScale|/ 256\b|>> 8\b|<< 8\b' src --include=*.cs

# Unit-suffix mistakes (NAM-2)
grep -rnoE '\w*RomTicks\w*' src tests --include=*.cs

# History, plan and milestone references (CMT-3, CMT-5)
grep -rnE "M4\b|M5\b|round [0-9]|Phase [0-9]|PHASE [A-Z]|plan [0-9]|author's report|\(author, 20|playtest 20|used to |The old |was wrong" src --include=*.cs

# Stale vocabulary after renames (NAM-8): add the old word of any rename in the diff
grep -rnwE 'Art|IArtSource|ArtRect|WalkArtIndex|wake' src tests --include=*.cs

# Suppressions (never allowed)
grep -rnE '#pragma warning disable|SuppressMessage|NoWarn' src tests --include=*.cs --include=*.csproj --include=*.props; grep -nE 'NoWarn' Directory.Build.props .editorconfig

# Hidden randomness and static state (TIME-3, SOLID-D)
grep -rnE 'new Random\(\)|\?\? new Random' src --include=*.cs
grep -rnE 'public static \w+ \w+ \{ get; set; \}' src --include=*.cs

# Broad catches (ERR-1)
grep -rnE 'catch \(Exception\)|catch\s*$' src --include=*.cs

# "Test-only" members used by production code (CMT-9): list them, then grep each name in src/
grep -rnE 'Test-only|test hook' src --include=*.cs

# Test-favouring defaults (TEST-2)
grep -rnE 'ForTesting\s*=\s*true' src --include=*.cs

# Untracked leftovers
git status --porcelain | grep '^??'
```

## Severity

| Level | Meaning | Examples |
|---|---|---|
| **Blocker** | Wrong behaviour, data loss, or a production default that favours tests | a counter bumped twice; `catch (Exception)` that overwrites a save; `ForTesting = true` in a production path |
| **Major** | Violates SOLID/STR in new code, or a comment/summary that states something false | new god-class growth; a hard cast; a stale value in a summary; a new magic number |
| **Minor** | Naming, consistency, formatting, missing unit suffix | `RomTicks` for frames; summary on a field's line |
| **Note** | A question for the author, or a pre-existing issue next to the change | an unexplained asymmetry that may be the ROM's |

A stale or false comment is **Major**, not Minor. In this codebase the comments are how the ROM mapping is
recorded, and a wrong one misleads the next change.

## Finding format

One entry per finding:

```
[<Severity>] <RULE-ID> <path>:<line> — <one-sentence defect>
  Why: <the concrete consequence or the reader's confusion, in one or two sentences>
  Fix: <the specific change: the rename, the constant name, the sentence to delete>
```

Example:

```
[Major] CMT-4 src/Robotron2084/Entities/Enforcer.cs:14 — remark says the grow-up is "40 in all"; the constant is 45.
  Why: the prose restates a value and has already drifted from EnforcerGrowUpRomFrames.
  Fix: replace "five pictures of 9 frames (40 in all)" with "EnforcerGrowUpRomFrames, in steps of EnforcerGrowStepRomFrames".
```

End the review with a one-paragraph summary: how many findings at each level, the most important one, and any
question that needs the author.

## What not to flag

- ROM labels, addresses and `notes §NN` references. These are required (CMT-1).
- Deliberate deviations marked "do not revert without asking" (e.g. `Human.StepPeriod`), unless the marking is missing.
- Byte tables copied from the ROM (`WaveTable`, `AttractMovieData`), as long as they have a summary giving the ROM
  address. They are data, not magic numbers.
- Style choices the `.editorconfig` and analyzers already enforce. The build reports those.
