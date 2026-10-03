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

The glossary is [glossary.md](glossary.md). It is part of the solution, so code and docs can link to it.
A reviewer should reject a new synonym for any word in it, and should reject a `<summary>` that needs the
glossary to be understood unless it links there (CMT-11).

## 2. Structure and responsibility (§114, §120, §121)

**STR-1. One reason to change per class.** If a class summary needs "and" to describe what it does, split it.
Warning signs: more than ~15 fields, or `// ---- section ----` comments that divide it into jobs.
`SpriteSet` is the counter-example.

**STR-1a. A class longer than 500 lines is doing too much.** Count every line of the file, blank lines and
comments included. This is a hard cap, not a guideline: past it, find the second job the class has taken on and
move it into a class of its own (STR-1), with its own file (STR-3). Do not get under the cap by deleting
documentation, by squeezing lines together, or by making the class `partial` across several files. A file that
is only data (a ROM table, a list of content paths) is the one exception, and its summary must say it is data.

**STR-2. One reason to change per method, and a verb phrase that names it.** A method whose body needs comment
headers ("// 1. ...", "// 2. ...") is several methods. Cyclomatic complexity above 10 is a **build error**
(CA1502). Split the method. Never suppress the rule.

**STR-2a. A method longer than 50 lines must be shortened.** Count the lines between its opening `{` and its
matching closing `}`, leaving out the lines that are blank. Comment lines count. This is a hard cap, not a
guideline: a method can be low-complexity and still be a wall of easy-to-read but repetitive lines that hides
the shape of what it does. Extract named helpers (STR-7) until it fits, even when nothing here is complex
enough to trip CA1502. Do not get under the cap by joining statements onto one line.

**STR-3. One type per file, and no nested types.** A class, record, enum, struct or interface gets its own file,
named after it, even a small helper type used by only one other class. Do not nest a type inside another
class — pull it out and give it its own file, with `internal` or `private`-equivalent visibility if it should
not be part of the public surface. `PlayField.LaserWallFlare` is the counter-example to fix, not to copy.

**STR-4. Dependencies point inward.** `Core` depends on nothing in the game. `Rendering` does not depend on
`Hud` or `States`. Entities do not reach into `States`. A new `using` that points outward needs a reason in
the review.

**STR-5. Member order in a class:** constants → static fields → instance fields → constructor(s) → public
properties → public methods (`Update` before `Draw`) → private helpers in the order the public methods call
them → `CurrentAnimationFrame` → test hooks, last and together.

**STR-6. A registry must be the only place.** If a type claims "add a row here and nothing else"
(`RobotKinds`), then nothing else may list the kinds by hand. A hand-written list elsewhere is a bug waiting to
happen, even when it is right today.

**STR-7. One level of abstraction per method (SLAP).** Every statement in a method's body should read at the
same altitude. A method that mixes a high-level step ("fire if due", "move the object") with the raw detail
underneath it ("if `field.Wall.Intersects(...)`, mirror `_step.X`") is really two methods: extract the detail
into a well-named helper and call it, so the body reads as a short list of steps at one level, each one a call
to something whose name says what it does. This is the same instinct as STR-2, seen from the reader's side:
if you have to ask "wait, what level are we on?" partway through a method, split it.

**STR-8. No feature envy: a rule lives with the data it is about.** A method that mostly reads or changes
another object's fields belongs on that object. The signs are a `static` or helper method that takes one object
and asks it several questions (`IsGraspable(Human human)` belongs on `Human`), a loop that reaches into each
item to test or count it (`CountLive(list)` belongs on the list), a calculation that only uses another type's
sizes (`Tank.GetPositionInside`, `Quark.GetStartPosition`), and a fact about a kind written as a `switch` outside
its registry (STR-6). Move the method to the type whose data it uses and leave the caller a one-line call. What
stays in a coordinating class such as `PlayField` is only what needs two or more different objects at once: the
order of the phases, and the rules between entities (who kills whom, who is drawn first).

**STR-9. A check on state is a method that says what it is for.** Never compare an entity's `LifeState` (or any
other state) with an enum value outside the type that owns it. Ask `entity.IsAlive()`, `IsDying()` or `IsDead()`
(`EntityLifeExtensions`), or add a method named for the question (`human.IsGraspable()`, `brain.IsReprogramming`).
The comparison then lives in one place, the call site reads as intent, and a change to what a state means is made
once. Inside an entity's own class, ask the same methods on `this`. Setting a state (`LifeState = ...`) stays
inside the entity that owns it.

**STR-10. Tell, don't ask.** Tell an object what to do; do not ask it for its data and then decide for it. Code
that reads several things from another object, makes a decision and writes the result back (`if (x.A > 0 &&
!x.B) x.C = ...`) belongs inside that object as one method (`x.Update()`, `x.Catch(target)`). Callers give the object
what it needs as arguments and let it work. A query is fine when the answer is the point (`IsAlive()`,
`GetNearestSlot()`), but a query used only to choose which command to send is the smell. This works with STR-8
(feature envy) and STR-9 (state checks): the rule and the data live together, and the caller issues one command.

**STR-11. Law of Demeter: one dot.** A method talks to its own members, its parameters, the objects it makes, and
the things it is directly given. It does not reach through one object to another: `field.Entities.GetList(kind).Entities`,
`field.Wall.Intersects(box)` and `sprites.Blitter.DrawSprite(...)` all call a method on something got from something
else. If a statement has a second dot after a property or a call, it is a smell. Fix it one of two ways: give the
near object a method that does the whole job (`field.HitsWall(box)`, `field.GetEntities(kind)`), or pass the
collaborator in as a parameter. A short-lived local that only renames a parameter's collaborator is not a fix.
- **Exempt:** reading plain data (`point.X`, `box.Bounds.Width`, `parameters.GruntCount`), one fluent chain on a single
  object (LINQ, builders), and static, namespace and enum paths (`ScreenSize.ToPortPixels`, `EntityLifeState.Alive`).
- **The price:** a thin delegating method on the near object. That is correct. It keeps callers from knowing the
  structure behind it, so the structure can change in one place.

## 3. Naming (§114)

**NAM-1. Methods are verb phrases in the domain's own words**: `RollOffsets`, `PickDirection`,
`AdvanceReprogramming`. Never `Process`, `Handle` or `Do` alone, never `Step1`, and never a bare verb with no
object (`Burst`, `Dispatch`).

**NAM-2. Numeric identifiers carry their unit** when the unit is not obvious, using the suffixes in section 1:
`BeatIntervalRomFrames`, `JitterColumns`, `_velocitySubpixels`. A name that says one unit while holding another is
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

**NAM-9. Principle of least surprise; one word per concept, everywhere.** A name must mean the same thing, and
work the same way, in every file it appears in — not just within one class (CON-1, CON-2 give the sibling-file
version of this rule; this is the whole-codebase version). A reader who has seen a name once should be able to
predict what a same-named member elsewhere does, and a method should do what its name leads a reader to expect
and nothing more surprising than that. When the codebase already has a word for a concept (this document's
glossary, or the terminology ledger for ROM concepts), use that word rather than a fresh synonym.

**NAM-10. A property is a stored value; a computation is a method. This follows Microsoft's member design
guidelines and is not optional.** A property getter may do no more than read a field, or make one trivial
derivation from the type's own fields that costs about the same as a field read (`Width => Right - Left`).
Anything else is a method, named with a verb (NAM-13). It is a method when the getter:
- loops, counts, searches or uses LINQ over a collection (`GetLiveGruntCount()`, `GetActiveLasers()`);
- allocates a new collection, array, string or other object each call (`GetInitials()`, `GetDisplayName()`);
- converts one thing to another (`ToString`-like work, parsing, formatting, `ToDisplayName()`);
- calls into the operating system, the file system or any other slow or external thing;
- looks something up in another object, or calls another method that does real work;
- can return a different result on two calls with nothing changed in between, or has a side effect;
- can reasonably throw.
When in doubt it is a method. A property must never hide a cost from its caller.

**NAM-11. Use explanatory variables instead of compound expressions inline.** When a constructor call or method
call would take more than one computed argument — anything beyond a bare identifier, a constant, or a single
member access — extract each computed piece into a well-named local first, then pass the locals in. Do not
write `new IntVector2(delta.X / ApproachDivisor, delta.Y / ApproachDivisor)` and stop there if `delta` itself
was still an inline expression; name the pieces so the call reads as a short sentence, not an equation to
untangle. `Enforcer.RollVelocity`'s `IntVector2 delta = new(targetX - _position.X, targetY - _position.Y);`
followed by the velocity call is the pattern to copy.

**NAM-12. Use British English** in identifiers, comments, `<summary>` text, test names, docs and user-visible
text: `colour`, `centre`, `initialise`, `materialise`, `behaviour`, `grey`, `serialise`, `normalise`. The
exceptions are names we do not own: a framework or library member keeps its own spelling (MonoGame's `Color`,
`Rectangle.Center`, `SpriteBatch`), and so does a ROM label quoted from the source. Where our own name has to
sit next to a framework one, ours is British (`SlotColour` returning a `Color`). A rename to British spelling
follows NAM-8: the American spelling is gone everywhere, not only from the declaration.

**NAM-13. Method names start with the verb Microsoft's guidelines give them.**
- **Fetch a value:** `Get…`: `GetElectrodeVariant(wave)`, not `ElectrodeVariantForWave(wave)`. A fetch that can
  fail is `TryGet…(…, out T value)`. A search that may find nothing is `Find…`.
- **Convert one thing to another:** `To…`: `ToPortPixels(specPixels)`, `ToClockUnits(romFrames)`,
  `ToPortTicks(romFrames)`. Never `Scaled`, `Units` or `…For…`.
- **Make a new object:** `Create…`: `CreateDefaults()`, `CreateFromWave(…)`, `CreateKey(…)`. Use `From…` or
  `New…` only where the framework itself does and `Create…` would read as something else (`Parse` stays
  `Parse`). If `Create…` would mislead, as `CreateFactory` suggested a factory object, pick a name that says
  what is created (`CreateWithFactoryScores`).
- **Work something out that is not a plain fetch:** `Compute…` (`ComputeMaxIntegerScale`).
A method with no parameters that only returns a value is almost always a property: check NAM-10 before keeping
it a method.

**NAM-14. It is an electrode, never a post.** The original source calls the obstacle a "post" (`PSTKIL`,
`PKPROC`), but this port has chosen **electrode**: the class is `Electrode`, and every name we write uses it
(`GetElectrodeVariant`, `ElectrodeSlotByWaveMod10`). "Post" appears only inside a quoted ROM label or a comment
that says which ROM routine it maps to. This is a decision of the author, and it overrides the terminology
ledger's "original word wins" for this one concept. Do not reopen it.

**NAM-15. Name every character as the ATTRACT MODE names it.** The attract screen introduces the cast as
**Mommy**, **Daddy**, **Mikey**, **Grunt**, **Hulk**, **Spheroid**, **Quark**, **Enforcer**, **Tank**, **Brain**,
**Prog** and **Cruise Missile**, and the author adds **Electrode**. Those are the words in identifiers, test names,
summaries, comments and docs: `HumanKind.Mommy`, `MommyCount`, `DaddyAnimationFrames`, never `Mom`, `Dad`,
`Mum`, `Mummy`, `Post`, `Circle` or `Square`. A ROM label or message quoted from the source keeps its own
spelling (`MOM`, `DAD`, `PSTKIL`, the attract text "SPHEREOID"). The plural is the ordinary one (Mommies,
Daddies, Electrodes, Brains, Grunts, Progs). A new character gets the name the attract screen gives it; if it
has none, ask the author (CMT-6). Content file names follow the same rule (`Sprites/Mommy_1`).

**NAM-16. The gap between beats is an interval.** The time from one beat to the next is the **interval**:
`BeatIntervalRomFrames` in ROM frames, `BeatIntervalClockUnits` in clock units, and `_beatTimer` for the timer that
counts up to it. Never call it a period, delay, rate, sleep or step, and never call a beat a "step" or a "turn"
(a hulk's or a human's beat is a beat; its steps are the moves inside it). The same word names any other repeating
gap: a flicker, a flash, a cycle (`SparkFrameIntervalRomFrames`, `CycleIntervalClockUnits`), and a gap counted in beats
(`fireIntervalBeats`). Where the ROM supplies only part of the interval, such as the frames a brain sleeps after its
beat, that value is a **wait** (`beatWaitRomFrames`), and the interval is the wait plus the frame the beat takes.

**NAM-17. A class is named for what it holds.** A class that holds rules is `...Rules`, and one rule is
`...Rule` (`CollisionRules`, `ICollisionRule`, `LaserCollisionRule`), never "phases", "passes" or "steps". In the
same way a list of strategies is `...Strategies`, a table of values is `...Tables`, a set of tuning numbers is
`...Tuning`, and a registry of rows is named for the rows (`RobotKinds`). When a class is renamed because its
contents are not what its name says, rename its interface, its members, its test classes and its file too (NAM-8).

**NAM-18. A method name says what the method does, and does not repeat the class name.** The class already says
what the thing is, so the method names only the action: `Robot.Move()`, not `Robot.MoveRobot()`; `Grunt.Kill()`,
not `Grunt.KillGrunt()`; `FamilyList.GetNearestSlot()`, not `FamilyList.GetNearestFamilySlot()`. The caller reads
`robot.Move()` and nothing is lost. The name must still be specific enough to say its purpose on its own: no bare
`Process`, `Handle`, `Do` or `Run`. If the action cannot be named without the noun, the method is probably on the
wrong class (STR-8).

## 4. Numbers and units (§112, §113)

**NUM-1. No magic numbers.** Every literal other than `0`, `1`, `-1` (and `2` when halving) is a named constant.
Its `<summary>` says **what it is** and **where it comes from**: a ROM routine and address, a measurement, or
"the author's tuning". Tunable values live in `Tuning/`. Values fixed by the ROM may live in the class that
uses them.

**NUM-2. The clock is never written as `5` and `6`.** Use `ArcadeClock.UnitsPerPortTick` and
`ArcadeClock.UnitsPerRomFrame` (or `RomFrameTimer`). Never copy them into a private constant.

**NUM-3. The column is never written as `2`.** Use `ScreenSize.Columns(n)` / `ArcadePixelsPerColumn`.

**NUM-4. Unit conversions go through named helpers** (`ScreenSize.ToPortPixels`,
`ScreenSize.ToPortPixelsFromColumns`, `ArcadeClock.ToClockUnits`), never through inline arithmetic like `* ScreenSize.SpecScale`,
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

**TIME-2. `ToPortTicks(romFrames)` truncates** and fires up to a tick early. Use it only for one-shot display
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

**CMT-6. Comments are written in plain English, and no vocabulary is invented without the author's consent
(§111).** For a ROM thing, use the original source's own name where one exists (check
`ref/original-source`, then `asm/robomame.asm`); otherwise use the word [glossary.md](glossary.md) already
gives it. "Wake up" is out; the periodic activation is a **beat**. Do not coin a new term, abbreviation or
label for a concept that has none yet — flag it to the author and use their word, rather than inventing one
and writing it into a comment as if it were established.

**CMT-7. No tombstones.** Do not write "(No XyzBlinkTicks: ...)" about code that does not exist. If the fact
matters, put it in the summary of the member that behaves that way ("dies at once; no death animation").

**CMT-8. Every member has a `<summary>` (§109.1)**: public, internal and private members, constructors
included. `<param>` tags match the real parameters exactly: no stale tags, none missing. `cref`s resolve.
One `<summary>` per member. `GenerateDocumentationFile` must be on, so the compiler enforces all of this.
`Update` and `Draw` are never exempt, on any class — every one of them gets a `<summary>` saying what that
class's beat or draw actually does, not a generic "Updates the entity"/"Draws the entity". Enum members are
members too: every value in every enum gets its own `<summary>` saying what that value means, not just the
enum type itself.

**CMT-9. A "test hook" label must be true.** If production code calls a member, it is not a test hook. Rename
it and document its real use.

**CMT-10. TODOs are tracked.** "Not built yet" and "open item" text in a comment must name the tracking item
(handoff row, notes section). If the thing has been built, delete the text.

**CMT-11. A `<summary>` is written in plain English, at about a 10-year-old's reading age.** Say what the
thing does in everyday words — no jargon, no ROM terminology, no code identifiers — one or two short sentences.
If a glossary word cannot be avoided, the summary says what it means in passing and links to
`docs/glossary.md`; it never leaves the reader to look it up on their own.
Where the member maps to a ROM routine, its `<remarks>` gives BOTH sources, each on its own bullet or line:
- the original arcade source: the `.ASM` filename and the routine's label (`ref/original-source`);
- this repo's own disassembly: the label or memory address in `asm/robomame.asm`, or a plain "not separately
  labelled" if it searched and found none — never guessed.
Verify both by searching the actual files; do not copy a label forward without checking it still exists. This
does not replace CMT-1's inline ROM cross-reference comment on ordinary (non-doc) code — where both apply,
follow this rule for the `<summary>`/`<remarks>` block and CMT-1 for any inline comment elsewhere in the body.

**CMT-12. An interface is a contract: its docs say what, never how.** A member's `<summary>` on an interface
or an abstract member describes what the caller gets and can rely on — the inputs, the outputs, the
guarantees — and nothing about any one implementer's internals. Do not write "counts down a beat timer and
steps toward the player" on `IEntity.Update`; that belongs on `Grunt.Update`. If a sentence on an interface
member would only be true for some implementations, it does not belong there at all.

**CMT-13. Say "animation frame" or "sprite", never "picture".** A drawing of a character is an **animation frame**, in
summaries, remarks, comments and identifiers alike (`...AnimationFrame`, not `...Picture`). Do not write
"picture", "art" or "image" for it, and do not use bare "frame", which means a ROM frame. "Sprite" is
fine, since everyone knows it: use it for a character's drawings in general, and "animation frame" when it matters
which one. A thing
with one drawing, such as a shell, has one animation frame. The ROM's own labels (`OPICT`, `PGXPIC`, `RWDP1`)
stay as they are, because they are quoted from the source, but the words around them are ours. Where the
ROM numbers its drawings from 1, say so and name the index base once, e.g. "walk animation frame 1 to 4".

**CMT-14. Say which unit a ROM distance is in, and check it.** The ROM counts X in **columns** (2 arcade
pixels each) and Y in **rows** (1 arcade pixel). A constant copied from a ROM listing is named and documented in
the unit the ROM used, and its `<remarks>` quotes the instruction (`ADDA #8`) and the disassembly address. If the
port deliberately uses a different value, the `<remarks>` says so and says why; if it does not, the constant is a
bug. Never label a column count as arcade pixels.

**SOLID-S.** See STR-1 and STR-2.

**SOLID-O.** New robot kinds, new states and new sounds should need additions, not edits spread across the
codebase. A `switch` over a kind enum outside its registry (`PlayField.ListOf`) is acceptable only if it is
the single mapping, and its exception message says where to add the new case.

**SOLID-L. No downcasts from an interface to a concrete type** in callbacks (`((Hulk)target)`,
`((IRemovable)target)`). If a callback needs a capability, the type system should guarantee it. Where that
is not possible, pattern-match and throw an `InvalidOperationException` that names the type.

**SOLID-A. Abstractions must not leak.** An interface's shape must not force a caller to know, or an
implementer to expose, a detail that is not part of what the interface promises — a concrete type peeking
through a generic parameter, a "just for one implementer" flag on a shared method, a return type that only
makes sense for one caller. If one implementation needs something the others don't, that need stays inside
it; it does not become a wart on the interface everyone else has to carry. See also CMT-12: a leak often
shows up first as a doc comment that has to say "except when...".

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

**FMT-2. Ternary operators only for very simple expressions.** A ternary is fine when the condition and both
branches are plain values or a single short call: `isRight ? 1 : -1`, `pad == 1 ? padTwo : padOne`. If the
condition is compound, a branch does arithmetic or calls something with arguments of its own, the ternary is
nested, or the line has to wrap, write an `if` with an early return (FMT-3) or a well-named helper instead. Always favour
readability over cleverness.

**FMT-3. Early returns, not `else`.** When one branch of an `if` finishes the method's work, return from it and
let the other path carry on unindented. Do not write `if (x) { ...; return; } else { ... }`, or
`if (x) { A } else { B }` where `A` could end with `return`. Guard clauses go at the top of the method.
Keep `else` only when both branches are short and neither one ends the method's work, e.g. setting a value
that the code below then uses.

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

**PROC-4. Atomic commits, one per folder.** A commit holds one logical change. When that change touches several
folders, commit each folder on its own: one commit for `docs`, one for each folder under `src/Robotron2084`
(`Entities`, `Level`, `Tuning` and so on), one for each test folder, one for each tool.
- **The message describes that folder's part.** The subject says what changed there, in the imperative
  ("Name beat gaps intervals in Tuning"). The body says why, and names the main types or rules affected. Never
  reuse one generic message across the folders.
- **Stage by path, never the whole tree.** Add the files you changed by name. Do not sweep in edits you did not
  make. If a folder holds someone else's uncommitted work that cannot be separated from yours, say so in the body.
- **Unrelated changes are separate commits**, even inside one folder (PROC-3).
- **The set must end green.** A folder commit in the middle of a set may not build on its own, because a rename
  crosses folders. The last commit of the set must build with no warnings and pass every test. Commit the folder
  that the others depend on first.
- **Do not push** unless asked.

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
# One top-level type per file, no nested types (STR-3), §121
for f in $(git ls-files 'src/*.cs' 'tests/*.cs'); do
  n=$(grep -cE '^(public|internal)\s+((sealed|abstract|static|readonly|partial|record)\s+)*(class|record|enum|interface|struct)\b' "$f")
  [ "$n" -gt 1 ] && echo "$f: $n types"; done
grep -rnE '^\s+(private|public|internal|protected)(\s+\w+)*\s+(sealed\s+)?(class|record|struct)\s' src tests --include=*.cs

# Class length over 500 lines, every line counted (STR-1a)
for f in $(git ls-files 'src/*.cs' 'tools/*.cs'); do
  n=$(wc -l < "$f"); [ "$n" -gt 500 ] && echo "$f: $n lines"; done

# Method length over 50 lines, blank lines left out (STR-2a): a rough brace-depth scan, not exact —
# check any hit by eye.
for f in $(git ls-files 'src/*.cs' 'tests/*.cs' 'tools/*.cs'); do
  awk -v file="$f" '
    /^[[:space:]]*(public|private|internal|protected|static)[^=;{}]*\)[[:space:]]*$/ { sigline=$0; sigat=NR; next }
    sigat && /\{/ && !inbody { depth=1; start=sigat; inbody=1; len=0; next }
    inbody {
      if ($0 !~ /^[[:space:]]*$/) len++
      o=gsub(/\{/,"{"); depth+=o
      c=gsub(/\}/,"}"); depth-=c
      if (depth<=0) {
        if (len-1>50) printf "%s:%d: %d lines: %s\n", file, start, len-1, sigline
        inbody=0; sigat=0
      }
    }
  ' "$f"
done

# Clock literals (NUM-2)
grep -rnE '(\+=|-=) 5;|\* 6\b|>= 6\b|< 6\b|= 6;|SixthsPer|Fifths|_sixths|_fifths' src --include=*.cs

# Column literal and hand-rolled scaling (NUM-3, NUM-4)
grep -rnE 'ToPortPixels\(2\b|ToPortPixels\(2 \*|\* ScreenSize\.SpecScale|/ 256\b|>> 8\b|<< 8\b' src --include=*.cs

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
- Deliberate deviations marked "do not revert without asking" (e.g. `Human.BeatIntervalRomFrames`), unless the marking is missing.
- Byte tables copied from the ROM (`WaveTable`, `AttractMovieData`), as long as they have a summary giving the ROM
  address. They are data, not magic numbers.
- Style choices the `.editorconfig` and analyzers already enforce. The build reports those.
