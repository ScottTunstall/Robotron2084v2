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

## 0. Faithfulness to the arcade

**FID-1. The arcade is the specification.** Where the arcade does something, the port does the same thing: the same
counts, the same timings, the same order of events, the same random rolls, and the same bugs. "Close enough" is a defect.
Before you write or change behaviour, read what the arcade does in `ref/original-source` and in `asm/robomame.asm`, and
write code that does that. If the two sources disagree, or neither says, stop and ask the author; do not guess, and do
not copy a number forward from a comment, a test or the notes without checking it against the sources (CMT-11).

**FID-2. A timing is counted from the arcade's own event, in the arcade's own unit.** Find the ROM event the count
starts at (a `NAP`, a `MAKP`, `CLR STATUS`) and start the port's count at the port's version of that event, not at
whatever moment is handy, such as when an object is made. Count it in arcade frames or beats on the clock-unit timer
(TIME-1). A loop that counts down from 18 and tests on its first pass takes 17 sleeps, not 18: work the count through.

**FID-3. A test of arcade behaviour pins the arcade's value and says where it comes from.** It names the ROM routine
and the address, and it checks the tick before as well as the tick of the event, so that being one tick out fails. A
test that was written to agree with the code proves nothing; when the code is found to differ from the arcade, the
test is wrong too and is corrected with it.

**FID-4. Anything that is not the arcade says so.** A deliberate departure is marked "do not revert without asking" in
the code, has a section in `docs/arcade-fidelity-notes.md` that gives the author's reason, and is behind a setting where
the arcade's behaviour can be had back. A port-only addition (Gorf, the Berzerk robot, eight-digit scores) says it is
port-only in its summary. A known difference that has not been fixed yet is written in the notes as "still not the
arcade", so that it is not mistaken for a decision.

**FID-4a. Gorf and the Berzerk robot are not in the arcade game.** Robotron 2084 (the arcade machine this port is based on) has neither. Gorf (its attract-mode sprites and paths, `GorfPath`, `Gorf`) and the Berzerk robot (`BerzerkRobot`, its sprites and animation) were added by the port, and they are not arcade behaviour. Their code, comments and tests must say so, under FID-4, and must never cite the arcade as their source. Do not describe either as arcade-accurate, and do not let a fidelity test pin them to a ROM value.

**FID-5. Faithfulness comes before tidiness.** If a clean-code rule and the arcade's behaviour pull in different
directions, keep the behaviour and find another way to make the code clean. A refactor never changes what the game does
(PROC-3).

## 1. Vocabulary: one word per concept

The glossary is [glossary.md](glossary.md). It is part of the solution, so code and docs can link to it.
A reviewer should reject a new synonym for any word in it, and should reject a `<summary>` that needs the
glossary to be understood unless it links there (CMT-11).

## 2. Structure and responsibility (§114, §120, §121)

**STR-1. One reason to change per class.** If a class summary needs "and" to describe what it does, split it.
Warning signs: more than ~15 fields, or `// ---- section ----` comments that divide it into jobs.
`TextureSet` is the counter-example.

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

**STR-2b. Every line of a method is at the same level of abstraction.** A method reads as a short list of steps that all
sit at one level, each a call that names what it does. Do not mix a high-level step (`ResolveCollisions()`) with
low-level detail (a bit mask, an index into a table, a raw loop over a list, a pixel offset) in the same body: lift the
detail into a helper with a name (STR-7) so the method tells the story and the helper holds the working. A method that
makes a decision and also does the arithmetic behind it is two methods. When you read a body and some lines are about
*what* the method does and others about *how* a step is done, the *how* lines belong in a helper. This is the test
STR-2a's length cap is a rough guard for: a short method can still break it.

**STR-3. One type per file, and no nested types.** A class, record, enum, struct or interface gets its own file,
named after it, even a small helper type used by only one other class. Do not nest a type inside another
class — pull it out and give it its own file, with `internal` or `private`-equivalent visibility if it should
not be part of the public surface. `PlayField.LaserWallFlare` is the counter-example to fix, not to copy.

**STR-4. Dependencies point inward.** `Core` depends on nothing in the game. `Rendering` does not depend on
`Hud` or `States`. Entities do not reach into `States`. A new `using` that points outward needs a reason in
the review.

**STR-5. Member order in a class:** constants → static fields → instance fields → constructor(s) → public
properties → public methods (`Update` before `Draw`) → internal properties → internal methods → private helpers in
the order the public methods call them → `CurrentAnimationFrame`. Order by visibility first: never put a private or
internal member between public ones. Test hooks go last and together within their visibility.

**STR-5a. Keep related members together.** Inside each visibility group, members that are conceptually related sit
side by side: the frame loop (`Update`, `Draw`), the questions about the wave, the questions about the player, the
spawns, scoring, sound, and so on. A reader should find everything about one idea in one place, not scattered by the
order it was written in. When you add a member, put it beside its relatives, not at the end.

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
the things it is directly given. It does not reach through one object to another: `playField.FieldEntities.GetList(kind).Entities`,
`playField.PlayfieldWall.Intersects(box)` and `textureSet.BlitterDraw.DrawSprite(...)` all call a method on something got from something
else. If a statement has a second dot after a property or a call, it is a smell. More than one dot means the class knows too much about
the inside of another object, and will break when that inside changes. Fix it one of two ways: give the
near object a method that does the whole job (`field.HitsWall(box)`, `field.GetEntities(kind)`), or pass the
collaborator in as a parameter. A short-lived local that only renames a parameter's collaborator is not a fix.
- **Exempt:** reading plain data (`point.X`, `box.Bounds.Width`, `waveParameters.GruntCount`), one fluent chain on a single
  object (LINQ, builders), and static, namespace and enum paths (`ScreenSize.ToPortPixelsFromArcadePixels`, `EntityLifeState.Alive`).
- **The price:** a thin delegating method on the near object. That is correct. It keeps callers from knowing the
  structure behind it, so the structure can change in one place.

**STR-12. A rule reports; the owner responds.** A class that detects a situation (a collision rule, a validator) returns a
**result** that says what it found, and never changes the object that called it or tells it what to do. The owner takes each
result and decides what it means. Give the rule a read-only view of what it may ask (`ICollisionScene`), and give the owner the
code that responds (`CollisionResponder`), so a response can change without a rule changing and a rule can be tested by
reading what it reports. Name the report `...Result` (`LaserHitResult`), and the thing that responds `...Responder`.
Where the owner must respond between two reports (a laser is spent before the next robot is tried), the rule returns its
results lazily (`yield return`) so each response happens before the rule goes on.

**STR-13. The playfield is the orchestrator.** `PlayField` is the one place that coordinates the objects in a wave, and
control flows one way: down from the playfield. It tells each entity when to update and draw, tells the rules when to check
for collisions, and acts on what they report. It owns the order things happen in. In the other direction, the objects it
orchestrates (entities, rules, spawners, the contact test) **never tell the playfield what to do**. An entity asks the
playfield a question or asks it to make something (STR-10, `field.HitsWall(box)`); a rule only reports a result
(STR-12); neither commands it, decides its order, or changes its state. If an object seems to need to command the
playfield, it should return a result and let the playfield decide. This keeps the order and the game's response in one
place, so they can change without touching the objects.

**STR-14. Declare a variable as close to its first use as possible.** Declare each one on the line that first gives it a value, not in a block of locals at the top of the method. A variable needed in two separate parts of a method is declared before the first of them.

## 3. Naming (§114)

**NAM-1. Methods are verb phrases in the domain's own words**: `SetRandomOffsets`, `GetRandomDirection`,
`TakeReprogramStep`. Never `Process`, `Handle` or `Do` alone, never `Step1`, and never a bare verb with no
object (`Burst`, `Dispatch`).

**NAM-2. Numeric identifiers carry their unit** when the unit is not obvious, using the suffixes in section 1:
`BeatIntervalArcadeFrames`, `JitterColumns`, `_velocitySubpixels`. A name that says one unit while holding another is
a defect: `StepColumns` holding port pixels, `...ArcadeTicks` holding beats.

**NAM-3. A name says what the value is, not what it once was or how it is used.** `HulkSpeed` for a delay
(where bigger is slower) is wrong. `_wallPalette` for the whole palette is wrong.

**NAM-4. Booleans read as predicates**: `IsHitStopActive`, `HasLives`. A method that returns a `bool` starts with
`Is` or `Has`: `IsInsideX`, `IsOverlappingLivingElectrode`, `HasFreeSlot`. Never `Can…`, `Try…`, `Fits…` or
`Overlaps…`. A state that is true *while held* is `...Held`, not `...Pressed`.
A bool **property that code outside the class can set** is named as C# WinForms names its own (`Visible`, `Enabled`, `Checked`,
`ReadOnly`): no `Is` (`Sound.Enabled`, `MovieObject.BrainVisible`; NAM-27). A bool that cannot be set from outside (no setter or a
private setter), a method, a parameter and a local is a predicate and keeps `Is` or `Has`. So `IsEnabled` is `Enabled` if it is settable, and
stays `IsEnabled` if it is read-only.
**Existing exceptions, left as they are by the author's decision:** ten public settable bools that start with `Is` are not renamed. They are
`MovieObject.IsLaser`, `IsDead`, `IsOnList` and `IsMonoActive`; `AttractObjectMachine.IsShakingUp` and `IsAlive`;
`BoardMemory.IsBackground1On`, `IsLaserBallBonusRepeating` and `IsOrganTuneNext`; and `Mc6800Cpu.IsInterruptRequested`. Do not rename them
to match this rule, and do not copy the form for a new property: a new settable bool follows the rule.

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
**The author's two tests for this rule:** a reader must be able to **guess the type of a parameter from its name** (NAM-19,
NAM-26) and to **guess what a method does from its name** (NAM-1, NAM-13, NAM-18, NAM-23). If the guess would be wrong, or
would need IntelliSense, the xmldoc or a look at the body, the name is wrong and is to be changed; the comment does not
make up for it. Most of the naming rules in this section are this one rule applied to a particular case.

**NAM-10. A property is a stored value; a computation is a method. This follows Microsoft's member design
guidelines and is not optional.** A property getter does one thing: it returns a value that is stored in a field, or in an auto-property.
**A calculated value is a method, never a property**, however small the calculation. `Width => Right - Left`,
`HasLives => Lives > 0` and `ExtraLifeEveryPoints => ExtraLifeEvery * 1000` are `GetWidth()`, `HasLives()` and
`GetExtraLifeEveryPoints()`: the parentheses tell the caller that something is worked out each time it asks. A
`bool` answer worked out from other members is a question, so it is a method named as one (`IsDying()`,
`IsAlive()`). Anything else that a getter might do is also a method, named with a verb (NAM-13). It is a method when
the getter:
- works a value out from other members, with arithmetic, a comparison or a call (the rule above);
- loops, counts, searches or uses LINQ over a collection (`GetLiveGruntCount()`, `GetActiveLasers()`);
- allocates a new collection, array, string or other object each call (`GetInitials()`, `GetDisplayName()`);
- converts one thing to another (`ToString`-like work, parsing, formatting, `ToDisplayName()`);
- calls into the operating system, the file system or any other slow or external thing;
- looks something up in another object, or calls another method that does real work;
- can return a different result on two calls with nothing changed in between, or has a side effect;
- can reasonably throw.
When in doubt it is a method. A property must never hide a cost from its caller. A property is not made a method
by hiding the calculation in a field that is updated elsewhere: that is TIME-4's one-meaning rule broken, and the stored
value can go stale.

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
sit next to a framework one, ours is British (`PaletteIndexColour` returning a `Color`). A rename to British spelling
follows NAM-8: the American spelling is gone everywhere, not only from the declaration.

**NAM-13. Method names start with the verb Microsoft's guidelines give them.**
- **Return a value:** every method that returns something other than a `bool` starts with `Get…`, whether it reads
  a stored value, works something out, finds something or picks at random: `GetElectrodeVariant(wave)`,
  `GetAimedSpeed(gap, shellSpeed)`, `GetRandomDirection(player)`. Not `ElectrodeVariantForWave(wave)`, `Compute…`,
  `Find…`, `Pick…` or `Next…`. A fetch that can come back empty returns `null`, or is guarded by a `Has…` method;
  there is no `TryGet…(…, out T value)`. The one exception is `Create…` below.
- **Do not write what LINQ already does.** A method that would take the same arguments and give the same answer as a
  LINQ operator (`Any`, `All`, `Count`, `First`, `Last`, `Single`, `Min`, `Max`, `Sum`, `Where`, `Select`,
  `Contains` and the rest) is not written. The caller uses the LINQ operator: `list.Any(grunt => grunt.SteppedThisUpdate)`,
  `list.Count(entity => !entity.IsDead())`, `list.Last()`. A collection class that holds its items in a list gets
  these for free by being an `IEnumerable<T>`; it does not add a `GetLast()`, a `GetLiveCount()` or an `Any(…)` of
  its own. A method that gives a domain meaning to a query over a collection it owns (`HasGruntStepped`,
  `IsClearOfElectrodes`) is fine, and is named by the rules above.
- **Convert one thing to another:** `To…`: `ToPortPixelsFromArcadePixels(arcadePixels)`, `ToClockUnits(arcadeFrames)`,
  `ToPortTicks(arcadeFrames)`. Never `Scaled`, `Units` or `…For…`.
- **Make a new object:** `Create…`: `CreateDefaults()`, `CreateFromWave(…)`, `CreateKey(…)`. Use `From…` or
  `New…` only where the framework itself does and `Create…` would read as something else (`Parse` stays
  `Parse`). If `Create…` would mislead, as `CreateFactory` suggested a factory object, pick a name that says
  what is created (`CreateWithFactoryScores`).
A method with no parameters that only returns a value is almost always a property: check NAM-10 before keeping
it a method.

**NAM-14. It is an electrode, never a post.** The original source calls the obstacle a "post" (`PSTKIL`,
`PKPROC`), but this port has chosen **electrode**: the class is `Electrode`, and every name we write uses it
(`GetElectrodeVariant`, `ElectrodePaletteIndexByWaveMod10`). "Post" appears only inside a quoted ROM label or a comment
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
`BeatIntervalArcadeFrames` in arcade frames, and `_beatArcadeFrameTimer` for the `ArcadeFrameTimer` that says when it is over. Never call
it a period, delay, rate, sleep or step, and never call a beat a "step" or a "turn"
(a hulk's or a human's beat is a beat; its steps are the moves inside it). The same word names any other repeating
gap: a move, a grow step, an animation frame (`MoveIntervalArcadeFrames`, `GrowIntervalArcadeFrames`,
`AnimationIntervalArcadeFrames`), a flash, a cycle (`CycleIntervalClockUnits`), and a gap counted in beats
(`fireIntervalBeats`). Where the ROM supplies only part of the interval, such as the frames a brain sleeps after its
beat, that value is a **wait** (`beatWaitArcadeFrames`), and the interval is the wait plus the frame the beat takes.

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

**NAM-19. A name tells you what it holds or does: no surprises.** This is for every name: methods, properties, fields,
parameters and local variables. A reader who sees only the name should be able to guess the type it holds, or what the
method does and gives back. A field called `_facing` that holds a `WalkSequence` is a defect, and so is a `Score` that is a
`ScoreBoard`. The glossary's unit suffixes (NAM-2) already do this for numbers; these do it for everything else:
- **A helper object is named for its class**, not for the thing it works on: `_highScoreStore`, `_gruntSpeedProgression`,
  `_frameAnimation`, `ObjectMachine`. Not `_highScores`, `_gruntSpeed`, `_frame`, `Objects`.
- **A collection is a plural noun** for what is in it (`_pendingRobots`, `_textCells`), and an array of flags says what is
  flagged (`_armedLines`, `_opaquePixels`). A single object is never a plural (`_boxes` for one `BoxContactTest`).
- **A number says what it counts or measures**: a place in a list ends in `Index`, a quantity in `Count`, a countdown in
  `...Remaining` or `...Left`, and anything with a unit takes the unit (NAM-2). `_animationFrameIndex`, not
  `_animationFrame` or `_rotation`; `_stepCount`, not `_step`. A direction held as -1 or +1 says so (`_directionSignX`),
  because `_direction` is a `Direction8`.
- **Anything of type `Texture2D` ends in `Texture`, and an array or list of them in `Textures`**: `EnforcerTexture`,
  `_pointsTexture`, `animationFrameTexture`, `GruntAnimationFrameTextures`, `BerzerkRobotWalkLeftAnimationFrameTextures` (never a bare `Frames`: that is an arcade frame), `GetCurrentAnimationFrameTexture()`. A bare
  `Enforcer` is an enforcer, and a bare `animationFrame` is not obviously a texture. The word before `Texture` still says what is
  drawn on it (CMT-13: an animation frame, a glyph, the laser bar). A method that makes or loads one is a `Create…` or
  `Load…` (NAM-13) and is exempt. This is the author's rule of least surprise.
- **A rectangle ends in `Bounds`** and a point does not: `_playfieldBounds`, `_canvasBounds`.
- **A delegate starts with a verb**, as the method it stands for would: `_getTargetPosition`, not `_target`.
- **A boolean is a predicate** (NAM-4) in every kind of name, locals and parameters included: `isFireHeld`, `fansByRows`,
  `TankShellBugEnabled`. Not `fire`, `rows`, `TankShellBug`. A boolean must not read as a count or an object
  (`LasersFiredThisUpdate`, `_arcadeText`).
- **A method that returns a value starts with the verb NAM-13 gives it**, and a method that returns `bool` reads as a
  question (`IsFramePixelLit`, `WasPressed`). A noun alone (`Digits`, `Layout`, `Colours`) is not a method name.
- **One name, one type, everywhere** (NAM-9). If `_settings` is a `GameSettings` in one class it is not a
  `ControlSettings` in the next: call them `_gameSettings` and `_controlSettings`. The same goes for locals: `pad` is a
  `GamePadState`, so the number is `padIndex` and the flag is `hasPadBinding`.
- **Where a name of ours sits beside a type of the same name, that is allowed and wanted**: `ScoreBoard ScoreBoard`,
  `GameSettings GameSettings`.
A name that needs a comment to say what it holds is wrong; rename it (§114). The corrections made when this rule came in
are listed in [refactoring-ledger.md](refactoring-ledger.md), which is also a list of examples.

**NAM-20. A method that works on a collection names what the collection holds.** If a method loops over, counts,
searches, filters or changes a collection of things, and the class it is on is not itself a collection, the method's
name says what the things are. `CountRescue` does not say what was rescued; `CountRescuedFamilyMembers` does.
`ClearText` becomes `ClearTextCells` and `AnyMenLeft` becomes `AnyPlayerSlotHasLives`.
- **A class whose own name already says what the items are does not repeat it.** `GruntSpeedProgression.SpeedUp(grunts)`
is right, and `SpeedUpGrunts` says "grunt" twice. `RobotTransporter.Begin(robots)` and `AttractObjectMachine.MoveObjects()`
are right for the same reason (NAM-18: a method name does not repeat its class name). The reader gets the type from the class
name; the method name only needs to supply it where the class name does not (`PlayField.CountRescuedFamilyMembers`,
`AttractPageMachine.ClearTextCells`).
- **A collection class is exempt**, because the class name already says what it holds: `EntityList.UpdateAll()`,
  `FamilyList.AnyAvailable()`, `LaserSlots.GetActiveLasers()`, `HighScoreTable.CountInitials()`. A class that is only
  a bundle of collections (`FieldEntities`) counts as one.
- **A method that loops over its own private numbers, such as a sample buffer or a pixel array, is not covered.** The
  rule is for collections of domain things: entities, family members, players, robots, results.
- **The name uses the glossary's word for the thing** (NAM-9): a family member is a family member, never a human
  (glossary: *family member*).
- **A method that returns a collection, or takes one only to pass it on, is judged by what it does with it.** If the
  answer needs the type to make sense, the type is in the name.
- **Interface methods count too.** `ICollisionRule.Detect` loops over what is on the field and gives no hint what kind
  of thing it finds; see the refactoring ledger for why it was left for the author to decide.

**NAM-21. A relative term says what it is relative to.** Any name, comment or document that uses a word meaning
"measured from something else" must also say what that something else is. The words are `Offset`, `Delta`, `Relative`,
`Distance`, `Gap`, `Margin`, `Shift`, `Displacement`, `Before`/`After` (when used for a position) and the like. The
reader must never have to open the code to find out. Two parts:

- **An offset names both ends: what it is an offset OF, and what it is an offset FROM.** Write
  `AimOffsetFromTargetMaxExclusiveArcadePixels` (the hulk's aim, measured from the target's own coordinate), not
  `AimOffsetMaxExclusiveArcadePixels`. Likewise `SpawnOffsetFromPlayerColumns`, `PitchOffsetFromPatternStart`,
  `_wallRowOffsetFromScreenTop`. When the "of" is already obvious from the field or class it sits in, the "from" is
  still required.
- **When the name would become unreasonably long, the XML summary must say it in full**, naming both ends in words:
  "the aim, as arcade pixels away from the target's coordinate". A summary that only repeats the name ("the aim offset")
  does not count.

The same goes for a coordinate or position, which is always measured from an origin: say which one (`...FromScreenTopLeft`,
`...FromPlayfieldOrigin`) unless the whole type works in one frame that its summary names, and for a time (`...SinceWaveStart`,
`...UntilNextShot`), a count (`...BeforeReaim`) and a distance (`...ToNearestFamilyMember`). A bare `Distance`, `Offset`
or `Delta` is a finding. A name that says the "from" in its own words (`Jitter`, `Gap` between two named things) is
fine only when the two things are named in the same sentence of its summary.

**NAM-22. A retired word is not used.** The glossary's "Retired words" table lists words that were replaced and why:
`Spec...`, `...SpecPixels`, `ArcadePixelsPerColumn`, "nearly every tick", "picture" and the rest. Do not use one in a
name, comment or doc, and when a rename retires a word, add it to that table in the same change. The dated records
(`status.md`, the ledgers, `arcade-fidelity-notes.md`) keep their original words.

**NAM-23. Command–query separation: a method changes something or returns something, never both.**
- A **command** is a verb that returns nothing: `SetRandomVelocity`, `Fire`, `TakeStep`, `ReleaseVictim`.
- A **query** starts with `Get…` (`Is…` or `Has…` for a `bool`, NAM-4 and NAM-13) and changes nothing: not the object,
  not its arguments (no `ref` or `out` results), not anything else.
- A step that both changes something and answers a question is split into a query and a command, and the caller
  calls them in order: `var step = GetStepTowardTarget(target); TakeStep(field, step);`. A query that has two
  answers returns them together as a tuple (`GetAxisAfterMove` returns the new position and the remainder).
- A method that tries something and answers with a `bool` (`TryFire`) is split the same way: ask with
  `HasFreeSlot()`, then `Fire(…)`. A command that cannot do its job when it should not have been called throws
  `InvalidOperationException` and its docs say what to call first.
- Drawing a number from a `Random` is not counted as changing something, so a query may do it; its name says
  `Random` (`GetRandomDirection`, NAM-24).
- Factories are the one exception: `Create…` makes and returns a new object, and may be called any number of times.

**NAM-24. A random pick says Random, never Roll.** `GetRandomDirection`, `SetRandomVelocity`, `ThresholdRandomRange`,
`OffsetXRandomMax`. The words "roll", "re-roll", "Reroll" and `…RollSides` are retired (dice talk: the code draws a
random number). Use "random number" or "picks at random" in comments, and `…MaxExclusive` for the number a random pick must stay below (`Random.Next(max)` never returns it). **This is the author's own preference and applies everywhere the word could stand for a random
number**: identifiers (`RollUpTo`, `SearchFamilyListRollAtMost`), constants, parameters, locals, comments, test names and
test helpers ("choose every roll", "direction roll", "rolls a number"). Name the cut-off a random number is compared
with a `…Threshold` (`SearchFamilyListThreshold`: a hulk whose random number is at or below it searches the family list),
and a method that returns a random number is `GetRandom…` (`GetRandomBelow`). The word is fine where it means something
else: a tank "rolls" along, "scroll", "stroll", "hand-rolled".

**NAM-25. A parameter or variable is not named after something else that exists.** Least surprise: a reader who sees
`playfield` expects a `PlayField`, `player` a `Player`, `keys` the `Keys` enum, `effect` MonoGame's `Effect`, `sound`
the `Sound` service, `point` a `Point`. If the thing is not that, say what it is: `playfieldBounds` for the
`Rectangle`, `playerPosition` for the `IntVector2`, `playerSlot` for a `PlayerSlot`, `playerIndex` for the number,
`keyboardState`, `stripEffect`, `soundSequence`, `boardSound`, `avoidedPosition`. A variable named after its own type
is fine (`Grunt grunt`, `KeyboardState keyboardState`, `Texture2D texture`, `PlayField playField`), but the whole
type name is needed (NAM-26). This covers the
types the project declares and the framework types it uses, and applies to parameters, locals, loop variables and
lambda parameters.

The same goes for a bare `settings`, because the project has more than one kind: `GameSettings`, `ControlSettings`
and the sound tables' `NoiseSettings`. Name it for what it is: `gameSettings`, `controlSettings`, `noiseSettings`,
`singSettings`. A named argument uses the same name (`gameSettings: _gameSettings`).

**NAM-26. A name tells the reader the type without IntelliSense.** The author reviews code on paper, in a diff and in a
pull request, with no tooltip and no go-to-definition. For every parameter, local, field, property and record component,
the reader must be able to say what type it is from the name alone, and must not need the xmldoc to do so.
- **An object is named with its whole type name.** Lower camel case for a parameter, local or field: `PlayField playField`,
  `TextureSet textureSet`, `GamePalette gamePalette`, `GameSession gameSession`, `ICollisionScene collisionScene`,
  `IPlayerInputSource playerInputSource` (an interface drops its `I`), `WaveSpawnContext waveSpawnContext`. Pascal case for a
  property or record component: `PlayField PlayField`. A shortened form (`field`, `sprites`, `palette`, `session`, `context`,
  `scene`, `input`) is an abbreviation (NAM-5), and two different types can share it, so it is not allowed.
- **A narrower thing puts its qualifier in front of the whole type name**: `deadPlayerSlot`, `previousKeyboardState`,
  `phantomTargetPosition`.
- **A word that C# reserves, or gives a special meaning to, never names anything.** `field` is the one that matters most:
  since C# 14 it is a keyword inside a property accessor (it means the backing field), so a parameter called `field` is a
  trap as well as vague. The others are `value`, `record`, `file`, `init`, `required` and `scoped`. Say `playField`.
- **A name another rule already fixes is fine**, because the type can be told from it: `...Bounds` (a `Rectangle`, NAM-19),
  `...Position` (an `IntVector2`), `...Count`, `...Index`, a unit suffix (NAM-2), a predicate (NAM-4), `...Texture` and
  `...Textures` (a `Texture2D`, NAM-19), `random` (a `Random`) and `spriteBatch` (a `SpriteBatch`). A single-letter lambda
  parameter in a one-line LINQ call stays allowed (NAM-6).
- **Also fine:** a collection named for what it holds (`grunts` for an `IReadOnlyList<Grunt>`), and the `SoundTables` fields, which
  are the ROM labels' own names by the author's decision.
- **The test:** could someone reading this line on paper say the type? If they would have to look it up, use the type name.
- **Why:** this is the author's rule, born of reviewing without IntelliSense and finding `field` on a `PlayField` (see "Decisions and
  why"). Do not shorten a type name to save typing: the cost falls on the reviewer.

## Decisions and why

These are choices the author made about names, in the order they were made, with the author's own reason where the author
gave one. They are here so that a developer who finds a rule odd can see the pain behind it, and so nobody reopens them
without reading it. Where the author gave a decision but no reason, it says so: that is a gap for the author to fill, not a
sign that there was no reason.

Most of the decisions below are one principle applied to a particular name, so it comes first.

| Decision | The author's reason | Rule or record |
|---|---|---|
| **The code follows the principle of least surprise.** A reader can guess the type of a parameter from its name, and can guess what a method does from its name. | "The code should use principle of least surprise. I need to be able to guess the type of the parameter by its name. I need to be able to guess what a method does by its name too." | NAM-9 (the two tests are written there); NAM-26, NAM-19, NAM-13, NAM-27 |
| **Name every object with its whole type name** (`PlayField playField`, never `field`). | "In WaveSpawnContext there is a parameter named \"Field\" - but what field? Field is a reserved word when in lower case. It should be called PlayField so that its completely unambiguous. I should be able to work out the type of the parameter without looking at the xmldocs. This will aid me in code review where I may not have intellisense." | NAM-26 |
| **A palette entry is a palette index**, not a slot, in every name (`LaserPaletteIndex`, `GetPaletteIndexColour`). | Asked whether `LaserSlot` was the palette index; if so, "use PaletteIndex throughout system, e.g. LaserPaletteIndex". (The audit found `Slot` meant five things, and that `LaserSlot`, a colour, sat beside `LaserSlots`, the store of three lasers.) | Glossary; refactoring ledger 2026-10-31 |
| **No "Colour" next to "PaletteIndex"**: `GetLargeTitleLettersPaletteIndex`, not `GetLargeTitleLettersColourPaletteIndex`. | "As a Palette is colour oriented anyway." | Glossary |
| **No "roll" for a random number**, anywhere (`SearchFamilyListThreshold`, not `SearchFamilyListRollAtMost`). | "I do not like the term \"Roll\" for random numbers." | NAM-24; refactoring ledger 2026-10-31 |
| **A name says what the number is**: a `…MaxExclusive` or a `…Threshold`, not a `…RandomRange`. | "TargetRandomRange isn't a range though - its a maximum or a threshold. Pick either Max or Threshold (depending on what is better suited)." | NAM-24 |
| **Wave, not level**, for one round of the game. | The author decided and did not give a reason. (The audit found `LevelParameters`' own summary said "wave", and the arcade and the screen say WAVE.) | Glossary; refactoring ledger 2026-10-31 |
| **Lives, not men, ships, turns or mini man**, for what a player may lose. | The author decided and did not give a reason. (The audit found `BozoMode` passing `StartingLives` as `shipsPerGame` and `GetSpareMen()` as `spareMen` in one call.) | Glossary; refactoring ledger 2026-10-31 |
| **Max and Min, not Cap, Limit or Maximum**, for the biggest and smallest a number may be. | The author decided and did not give a reason. | Glossary; refactoring ledger 2026-10-31 |
| **The sound tables keep the ROM's names** (`CircleKill`, `SquareKill`, `PostKill`). | The author decided and did not give a reason. (They were already the ROM labels' own comments, so nothing was renamed.) | `SoundTables` summary; glossary |
| **Say "strip explosion" for the strips flying apart, and "strip effect" only for both kinds together** (`StripEffectTuning`, not `StripExplosionTuning`). | "Strip Explosion should be used - not strip effect, if it means the same thing." (They do not mean the same: `StripEffect` also does the appear, so the class keeps its name and the tuning class, which held both, took it.) | Glossary; refactoring ledger 2026-10-31 |
| **Rescue bonus** is the amount earned for saving a family member (`RescueBonusMarker`), not rescue score or points. | "D06 should be \"RescueBonus\" as Points may infer a 2D point on screen." | Glossary; refactoring ledger 2026-10-31 |
| **A bool that says whether something is visible is `IsVisible`** (or `IsPlayerVisible`, `isBrainVisible` when there is more than one thing to see). | "ShowingPoints should be IsVisible. Its a bool and so should follow standards ... Any bool that determines if something is visible or not should be named IsVisible." (The same message asked whether `Points` needs to be in the property names of `ScoreBurst`, since the class name already says it is about points. That question is open: see the refactoring ledger.) | NAM-27 |
| **The flash and number left by a spheroid or quark is a `ScoreBurst`**, not a death burst or a `PointsBurst`. | "Keep ScoreBurst." The author gave no reason. (Considered: `DeathBurst`, which the glossary had pencilled in, and `PointsBurst`, which reads like a burst at a 2D point.) | Glossary |
| **Anything of type `Texture2D` is named `{something}Texture`** (`_pointsTexture`, not `_pointsSprite`; arrays and lists end `Textures`). | "Can we have anything of type Texture2D named as {xyz}Texture? Texture2D _pointsSprite should be _pointsTexture. Again this is the principle of least surprise." | NAM-19; refactoring ledger 2026-10-31 |
| **`SpriteSet` becomes `TextureSet`** (with `ISpriteSource` becoming `ITextureSource`). | "I think SpriteSet isn't the right name, given it deals with Animation Frames." The author did not name a replacement; after seeing that about 20 of its 45 members are not animation frames (fonts, laser bars, logos) and that the names are all `…Texture(s)` now, they agreed to `TextureSet`. | NAM-19; refactoring ledger 2026-10-31 |
| **Sprite data comes from the ROM through the sprite extractor, never from bytes typed into the code.** The spare-life icon is the asset `Sprites/SpareLife.png`. | "Why do we have BuildSpareLifeTexture() - can't we extract the sprite data from the ROM like the rest?" (The extractor already had: the PNG was called `PlayerExtra.png` and nothing loaded it.) | Refactoring ledger 2026-10-31 |
| **`SpriteFactory` is `TextureFactory` and `SpriteContentPaths` is `TexturePaths`.** | "Yes, rename SpriteFactory and SpriteContentPaths to TextureFactory and TexturePaths." The author gave no further reason: it follows `TextureSet` and the `…Texture` rule. | Refactoring ledger 2026-10-31 |
| **A settable visibility flag is `Visible`; a read-only one is `IsVisible`** (the C# WinForms convention). | "If IsVisible is mutable it should be called Visible, in keeping with C# Winforms convention." | NAM-27; refactoring ledger 2026-10-31 |
| **Bool property names are consistent with C# WinForms**: settable is `Enabled` or `Visible`, never `IsEnabled`; read-only is `Is…` or `Has…`. | "If there's an IsEnabled property which is mutable, rename it to Enabled. Keep the property naming consistent with Winforms." (There is no `IsEnabled` in the code; the existing settable switches already say `Enabled`.) | NAM-4; NAM-27 |
| **Ten existing settable `Is…` bools are left alone** (the attract movie's objects, the sound board's memory, the CPU emulator). | "No leave them alone". The author gave no reason. (Several would read badly in the WinForms form: `IsLaser` as `Laser` looks like a laser object.) | NAM-4 |
| **Each fact is documented once, by its owner**: interfaces are contracts and never explain a concrete implementation; implementations say what they do and what they talk to, not who calls them (unless that is critical to use) and not how the objects they hold behave. | "Interfaces are contracts - they MUST NOT explain how the concrete implementation behaves. On the other hand, implementations are concerned ONLY with what they do and what they talk to (ie: what they are composed of or aggregated with), NOT who calls them (unless that is critical to use). Implementations should not talk about how contained/aggregated objects behave though, if the implementations change the comments will rot." | CMT-17 (draws together CMT-12, CMT-15 and CMT-16) |
| **Every text file is CRLF on disk**; the repository still stores LF. | "I need all files to be in windows format - CR LF". | `.gitattributes` (`eol=crlf`), `.editorconfig` |
| **`Human` is renamed to `FamilyMember`**, with `FamilyMemberKind`, the `…FamilyMemberResult` records and the file names. "Human" in a comment is changed only where it means the family member; where it means a person (the player, a reader), it stays. | "Human should be renamed to FamilyMember." The author chose the recommended scope: "Follow recommended and rename file names". The author gave no reason. | Refactoring ledger, 2026-10-31 |
| **No magic numbers, including `-1` and `1` as a direction's sign.** A sign is `DirectionSigns.Left`, `Right`, `Up` or `Down`. | "I don't like code like `_escapeDirectionSignX = -1`. It uses magic numbers. I want you to create a class with constants called DirectionSigns, where Left = -1, Right = 1, Up = -1, Down = 1. Also record in coding standards not to use magic numbers." The author gave no further reason. | NUM-1 |
| **A variable is declared as close to its first use as possible** (STR-14). | "Make it a coding rule that variables should always be declared as close to first use as possible." The author gave no reason. | STR-14 |
| **A method that only sets a position is `MoveTo`, not `TeleportTo`.** | "I do not like the name TeleportTo if all it does it set the position. Change it to MoveTo throughout codebase". The author gave no further reason. (`FamilyMember` already used `MoveTo`.) | Refactoring ledger, 2026-10-31 |
| **In the palette code, a change to the next colour is a "cycle", not a "step".** | "I think Cycle is the commonly used term in the system so use that if possible". The author gave no further reason. | Refactoring ledger, 2026-11-01 |
| **The presentation page's white colour run is the "white flash", not the "chase", and the reason for the name is written in the code.** | "Call it white flash. Make sure its documented why its named so". The author gave no further reason. | Refactoring ledger, 2026-11-01 |
| **Source references lead with the original `.ASM` file and routine label; the disassembly address or label comes second. Never a URL or a bare address.** | "the rules are that you add a xmldoc referencing the FILE NAME and FUNCTION NAME in the original source code", and "any comments should primarily reference the original asm code's label, and the equivalent memory address or label in my disassembly. This isn't usable ... because its not referencing the original source code". | CMT-11 |
| **xmldocs are plain English in proper XML, with no markdown tables, backticks or asterisks.** | "I do not like the xmldocs here. They look gibberish." The author gave no further reason. | CMT-10, CMT-11 |

If you are the author and want to add the story behind any row, add it in the second column in your own words.

**NAM-27. A bool that says whether something is visible is called `Visible` when code outside the class can set it, and `IsVisible` when it cannot.**
Not `Showing…`, `Shows…`, `…Shown`, `…Drawn` or `show…`.
- **A property with a setter that other classes can use is `Visible`**, with no `Is`, as C# WinForms names `Control.Visible` and
  `Control.Enabled` (`MovieObject.BrainVisible`). The code already names its other settable switches this way (`Sound.Enabled`,
  `GameSettings.BozoModeEnabled`).
- **Everything that is a question is `IsVisible`** (NAM-4): a property with no setter or a private setter (`ScoreBurst.IsVisible`), a method
  (`StripEffect.IsVisible()`, `PlayField.IsPlayerVisible()`), a parameter and a local (`isSpareLivesVisible`, `isAttractVisible`).
- **When there is more than one thing to see, the thing goes in front**: `IsPlayerVisible`, `BrainVisible`. Every visibility flag then ends in
  `Visible`, so a reader can find them all with one search.
- **Not covered:** a bool that picks *which* of two things is shown (`_isShowingArcadeText` chooses between the arcade's text and the
  port's), and a bool that says something has *happened* (`HasPlayerAppeared`).

## 4. Numbers and units (§112, §113)

**NUM-1. No magic numbers.** Every literal other than `0` and `1` (and `2` when halving) is a named constant, `-1` included. A direction's sign is `DirectionSigns.Left`, `Right`, `Up` or `Down`, never `-1` or `1`.
Its `<summary>` says **what it is** and **where it comes from**: a ROM routine and address, a measurement, or
"the author's tuning". Tunable values live in `Tuning/`. Values fixed by the ROM may live in the class that
uses them.

**NUM-2. The clock is never written as `5` and `6`.** Use `ArcadeClock.UnitsPerPortTick` and
`ArcadeClock.UnitsPerArcadeFrame` (or `ArcadeFrameTimer`). Never copy them into a private constant.

**NUM-3. The column is never written as `2`.** Use `ScreenSize.Columns(n)` / `ArcadePixelsPerByte`.

**NUM-4. Unit conversions go through named helpers** (`ScreenSize.ToPortPixelsFromArcadePixels`,
`ScreenSize.ToPortPixelsFromColumns`, `ArcadeClock.ToClockUnits`), never through inline arithmetic like `* ScreenSize.PortPixelsPerArcadePixel`,
`>> 8` or `* 256`. Name the subpixel scale once (`SubpixelsPerPixel`).

**NUM-5. Store values in the unit the source gives them**, and convert at the use site. Do not pre-multiply
into port pixels: `TankBirthOffsetX = 8`, meaning "2 columns in port pixels", is the thing to avoid.

**NUM-6. Derive, don't restate.** `640f / 304f` is `ScreenSize.Width / ArcadeScreenWidth`. A number that can
be computed from other constants must be.

**NUM-7. Public static arrays are read-only in fact, not just in name.** Expose ROM tables as
`IReadOnlyList<T>` or `ImmutableArray<T>`, because a `static readonly int[]` can still be written to by any
caller.

**NUM-8. No trigonometry, and movement is worked out in integers.** `Sin`, `Cos`, `Tan` and their inverses
(`Math`, `MathF` or `MathHelper`) are banned from the whole code base, tests and tools included. The author: *"BAN SIN
AND COS FROM THIS SOURCE CODE ENTIRELY. Everything should be done in integers."* A curve such as Gorf's hop is
whole-number arithmetic (`GorfPath.GetHopHeight`), as the arcade's own code is. `tools/lint-conventions.ps1` checks it.

## 5. Time and state

**TIME-1. One timer idiom.** Anything that happens every so many fiftieths of a second is timed by a
`Core/ArcadeFrameTimer`, called once per `Update`: `if (_beatArcadeFrameTimer.Tick(BeatIntervalArcadeFrames)) ActOnBeat(playField);`. An
entity never adds clock units itself. Do not introduce `TimeSpan`/`GameTime` timers in gameplay code.

**TIME-1a. The same idea has the same name in every entity.**

| Idea | Timer field | Interval constant | Method it calls |
|---|---|---|---|
| The entity's turn to move or act | `_beatArcadeFrameTimer` | `BeatIntervalArcadeFrames` | `ActOnBeat` |
| A move made every fiftieth of a second | `_moveArcadeFrameTimer` | `ArcadeClock.MoveIntervalArcadeFrames` | `MoveOnce` |
| A step of growing | `_growTimer` | `GrowIntervalArcadeFrames` | `TakeGrowStep` (and `IsGrowing`) |
| The next animation frame, when not on the beat | `_animationTimer` | `AnimationIntervalArcadeFrames` | `ShowNextAnimationFrame` |
| How long a short-lived thing lasts | `_lifeTimer` | `LifeArcadeFrames` | (it dies) |

A count of beats, steps or ticks left is `_<what>BeatsRemaining`, `_<what>StepsRemaining` or
`_<what>TicksRemaining`. A robot that waits for the wave to start gets its beat timer from
`PlayField.CreateFirstBeatTimer`.

**TIME-2. `ToPortTicks(arcadeFrames)` truncates** and fires up to a tick early. Use it only for one-shot display
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

**CMT-10. A summary says what the thing IS; the remarks say how it works and where it comes from.** The `<summary>` of a
type or member names it in plain words: what it is, or what it does for its caller. It does not describe the mechanism
(the counts, the states, the arithmetic, the order of steps). That goes in `<remarks>`, together with the
cross-references: the original source (`<item>Original source: ...</item>`, file and routine) and the disassembly
(`<item>Disassembly: ...</item>`, file and address or label), in the same list form the existing classes use.

For example, `Enforcer`'s summary says it is the arcade's enforcer robot. Its remarks say how it moves, fires and drops
sparks, with the ROM routine and the disassembly label. A summary such as "walks in eight directions, firing a spark
every 30 ticks" is wrong: the walk and the rate belong in the remarks, and the rate is a named constant (CMT-4). The
same holds for a method: its first sentence says what it is for, and its steps go in the remarks.

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
- the original arcade source: the `.ASM` filename and the routine's label, from github.com/historicalsource/robotron
  (it is not in this repo). The label comes first. A URL, or a bare address such as `$8A4F`, is not a reference;
- this repo's own disassembly: the label and memory address in `asm/robomame.asm`. The disassembly is NOT complete
  and is allowed to miss a label (see CMT-11b); then cite the memory address alone — never guessed.
Verify both by searching the actual files; do not copy a label forward without checking it still exists. This
does not replace CMT-1's inline ROM cross-reference comment on ordinary (non-doc) code — where both apply,
follow this rule for the `<summary>`/`<remarks>` block and CMT-1 for any inline comment elsewhere in the body.

**CMT-11a. Where the original source is, and which file holds what.** The original arcade source (not this repo's
disassembly) is at https://github.com/historicalsource/robotron. Clone it to a scratch folder and search it; never cite
a label from memory. A reference is the `.ASM` filename and the label (CMT-11), so look the label up. The files, and the
labels found in each so far (verified 2026-11-07):

| File | What is in it |
|---|---|
| `RRSCRIPT.ASM` | The attract movie. The page script `HISTO` and its reader `SPWAKE` (with `SCMTAB`, `CURSAP`, `CLRMP`, `NEWLNP`, `SCRPP`, `SLEPP`, `MESSP`, `ENDP`, `COLORP`, `GRNTME`, `DONE2P`); `GRPROC`, `GSTRTS`, `GS1`; the page-script actions `CURSAB` to `DONE2` and the constants `LEFT`, `TOP`, `TOPLEF`, `MESHIT`; the object-script opcodes (`SETXV` to `PDEAD`, table `SCTABL`); the process fields (`SCPTR`, `ODESC`, `OBJ`, `IMAG`, `LEV1`, `LEV2`, `LOOP`, `USER`); `OSTART`, `RIGFIR`, `LEFFIR`, `EXPP`, `MONOP`, `DYDX`, `ANATAB`, `ANA1`, the walkers `BRL`/`BRR`/`BRD`/`BRU`/`BRWALK`; `ACTHIT`; the descriptors (`MOMMY`, `BRAIN`, `SQUARE` and the rest) and the object scripts (`PLAYRR`, `FAMSCR`, `SCHULK`, `SCSC`, `SQP`, `TKP`, `ENP`, `DADD2`, `PSHAKE`, `POSTER`, `HELPME`) |
| `RRF.ASM` | The shared variables and constants: `XMIN`, `XMAX`, `KILLOF`, `BLKCLR`, `DMAON`, `TEXCOL`, `HUMANA`, `HLKANA`, `LASPIC`, `PR57V`, and the object fields `OBJX`, `OBJY`, `OX16`, `OY16`, `OXV`, `OYV` |
| `RRFRED.ASM` | The message numbers (`MMOM` 115 to `NULMES` 126) |
| `RRET.ASM` | The message text (`PMOM` "MOMMY", `PHULK` "INDESTRUCTABLE HULK" and the rest) |
| `RRS22.ASM` | The object mover `OPB80` and `OPRC80` |
| `RRB10.ASM`, `RRH11.ASM`, `RRG23.ASM` | The brain, the hulk and the grunt/wave code (see the entity docs) |

**CMT-11b. The original source is complete; the disassembly is not.** The author's disassembly (`asm/robomame.asm`) is
unfinished, and it is fine for it to lack a label. When a routine or table has no label there, cite its memory address
(for example "the unlabelled loop at `$79DD`") — do not write "not separately labelled", and do not leave the bullet
out. Only a name that is not in the ROM at all (a constant or field that exists only in the original's header files, such
as `XMIN` or `OXV`) has no address; say "no address in the disassembly (a name of the original source only)". The original
source is the authority for names and meaning; the disassembly only adds the address.

**Both sources are always cited (CMT-11).** Every `Original source:` bullet has a `Disassembly:` bullet next to it. Search
`asm/robomame.asm` by address as well as by name before you decide a routine is missing from it: the disassembly names its routines differently from the original (for example the original's
`SPWAKE` is the unlabelled loop at `$79DD`, and its `PR57V` is `BLIT_LARGE_CHARACTER` at `$6023`). The attract movie in
the disassembly (verified 2026-11-07):

| Disassembly | Address | Original |
|---|---|---|
| `STORYLINE_SCRIPT` | `$7FA3` | `RRSCRIPT.ASM` `HISTO` |
| unlabelled page-script loop | `$79DD` (started at `$79D4`), jump table `$7A08` | `SPWAKE`, `SCMTAB` |
| `CLEAR_AREA_READY_FOR_TEXT_PRINTING` | `$7A9C` | `CLRMP` |
| `SET_TEXT_COLOUR_FROM_SCRIPT` | `$7A5A` | `COLORP` (`TEXCOL`) |
| `PRINT_CHARACTER_NAME_FROM_SCRIPT` | `$7A60` | `MESSP` |
| `SET_X_COORD_TO_20_AND_ADD_11_PIXELS_TO_Y_COORD` | `$7AB6` | `NEWLNP` |
| `CREATE_14_GRUNTS_FOR_DEMO` | `$7A29` | `GRNTME`, `GRPROC` |
| `STORYLINE_CONCLUDES` | `$7A84` | `ENDP` |
| `INTRODUCE_STORY_CHARACTERS` | `$7AC1` | `SCRPP` |
| `INTRODUCE_GAME_CHARACTER_FROM_SCRIPT` | `$7B0F` | `OSTART` |
| `INVOKE_ACTION_FROM_CHARACTER_INTRO_SCRIPT`, opcode table | `$7B39`, `$7B58` | the object-script reader, `SCTABL` |
| opcode routines | 0 `$8DA0`, SETOB `$7D31`, SETIM `$7D4B`, walks `$7D19` to `$7D2B`, SETPOS `$7CC1`, SETXV `$7CB2`, SETYV `$7CB9`, CYCLE `$7C8F`, REST `$7C87`, DIE `KILL_OBJECT` `$7C81`, EXP `$7C6F`, JUMP `$7C68`, HIB `$7C54`, REBORN `$7C5C`, FORK `$7C4E`, LFIRE `$7C1F`, RFIRE `$7C26`, LOOPER `$7C37`, GHOST `EXECUTE_CHARACTER_STORYLINE` `$7C05`, SETRP `$7CCB`, MONO `$7B90`, RPROG `$7CD9` | the opcodes `SETOB` to `PDEAD` |
| unlabelled brain walkers | `$7D76` to `$7DDC` | `BRL` to `BRWALK` |
| unlabelled step routine | `$7DDF` | `DYDX` |
| unlabelled animation frame table | `$7DEF` | `ANATAB` |
| unlabelled family and hulk walker | `$7DF2` to `$7E40` | `ANAL` to `ANASUB` |
| walk tables | family `$03CF`, hulk `$01CC` | `HUMANA`, `HLKANA` |
| unlabelled descriptors | from `$7E41` | `MOMMY`, `BRAIN` and the rest |
| `MAKE_ENEMY_EXPLODE` | `$5C1F` | the explosion `EXP` calls |
| message pointer table | `$6377` | the message numbers in `RRFRED.ASM`, text in `RRET.ASM` |
| `BLIT_LARGE_CHARACTER` | `$6023` | `PR57V` (`RRF.ASM`) |

Add a row or a label here the first time you verify a new one, so that no one has to ask again.

**CMT-12. An interface is a contract: its docs say what, never how.** A member's `<summary>` on an interface
or an abstract member describes what the caller gets and can rely on — the inputs, the outputs, the
guarantees — and nothing about any one implementer's internals. Do not write "counts down a beat timer and
steps toward the player" on `IEntity.Update`; that belongs on `Grunt.Update`. If a sentence on an interface
member would only be true for some implementations, it does not belong there at all.

**CMT-13. Say "animation frame" or "sprite", never "picture".** A drawing of a character is an **animation frame**, in
summaries, remarks, comments and identifiers alike (`...AnimationFrame`, not `...Picture`). Do not write
"picture", "art" or "image" for it, and do not use bare "frame", which means an arcade frame. "Sprite" is
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

**CMT-15. A field's docs say what it is for, never how it is used.** The `<summary>` of a field or constant states its purpose (what the value is, and its unit) and, in the `<remarks>`, its relation to the original source or the disassembly (routine, address, instruction). It never says which code reads or changes it, or how: not "is added to", "is taken off", "counts up to this", "is compared with", "decides how long", "is picked at random when". Saying how a field is used lets the field dictate to the method that uses it, and the doc goes stale when that method changes. A reader finds the use with the IDE. This replaces the old rule that a constant's docs must name the field it is added to or compared with.

**CMT-16. An orchestrator's docs say what it does, not how the things it runs work inside.** A class that drives other objects (the playfield driving the entities, a state driving the playfield, a list driving its items) documents its own part: what it calls, when, in what order, and when it does not. It must not describe how the called object works inside: its fields, its timers, its counting, its private steps. Write "on each tick the playfield calls each character's `Update`", not "each character keeps a timer and adds to it". How an object works is documented on that object, once, where it cannot go stale. This is CMT-12 seen from the caller's side: the interface says what is promised, the orchestrator says what it asks for, and only the implementer says how.

**CMT-17. Documentation has one owner for each fact: the interface owns the contract, the implementation owns what it does and what it talks to.**
- **An interface says what, never how** (CMT-12). It must not explain how any concrete implementation behaves.
- **An implementation says what it does and what it is built from.** It documents its own behaviour, and the things it talks to: what it is
  composed of or holds a reference to (`ArcadeFrameTimer`, `PlayField`, a list of strips), and what it asks of them.
- **An implementation does not say who calls it,** or when or how often, unless that is critical to using it correctly. "Call this once to start
  the reprogramming" is guidance and stays; "the playfield calls this on every tick" is a fact about someone else's code, and goes stale when that
  code changes (see CMT-15 for fields and CMT-16 for a class that drives others).
- **An implementation does not say how the objects it holds behave inside.** It says what it asks of them, not how they work. If the contained
  object changes, a comment that described its insides is now wrong, and nobody knows it is wrong.

The test: if a class you only *use* changed how it works, would this comment become false? If so, the comment is in the wrong place. Move the
sentence to the class that owns the fact, or delete it.

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
locators (`Sound`, `DeveloperKeys.AttractFastForward` are the existing ones to shrink, not grow). Pass
collaborators in through the constructor. Read hardware input only through `IPlayerInputSource` or an
injected snapshot, not `Keyboard.GetState()` inside a state.

## 8. Test seams and dead code

**TEST-1. Test hooks are `internal`, labelled "(test hook)" in their summary, and grouped last in the class
(STR-5).** One idiom per need: position overrides are `internal void MoveTo(...)`, never an
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

**FMT-2. A ternary (`? :`) is allowed only as the whole value of a simple `return`.** Allowed: `return isRight ? 1 : -1;`
and `return pad == 1 ? playerTwoGamePadState : playerOneGamePadState;`. The condition is one name, one short comparison, or one `Is…`/`Has…`
call. Each branch is a name, a literal or a constant: no arithmetic, no calls, no slicing. Everywhere else, write an `if`. That means never:
- on the same line as an `if`: `if (a) return b ? c : d;`
- as an argument, inside a string, or inside another expression
- nested, or with `&&`/`||` in the condition
- with a branch that does arithmetic or calls something with arguments
- on a line that has to wrap

When a ternary is not allowed, name the parts (NAM-11): put each condition in a `bool` that says what it means, then
use `if` and early returns (FMT-3). The reader should never have to work out which `?` goes with which `:`.
```csharp
// Hard to follow: two decisions on one line.
if (Math.Abs(gapX) >= Math.Abs(gapY)) return gapX >= 0 ? WalkSequence.Right : WalkSequence.Left;

// Clear: each decision has a name and its own line.
var isSidewaysFurther = Math.Abs(gapX) >= Math.Abs(gapY);
if (isSidewaysFurther)
{
    var isPlayerToTheRight = gapX >= 0;
    if (isPlayerToTheRight) return WalkSequence.Right;

    return WalkSequence.Left;
}
```

**FMT-2a. Clever, compact code that is hard to read is not acceptable.** Shorter is not better. If a reader has to
stop and work out what a line does, rewrite it with explanatory variables (NAM-11) and one step per line (STR-7).
This applies to ternaries, chained `&&`/`||`, nested calls, and `switch` expressions that hide logic in their arms.

**FMT-2b. Complex expressions are forbidden: name every piece.** An expression that slices, spreads, indexes or does
arithmetic inside another expression is complex, and is not allowed in a `return`, an argument or an initialiser. This
includes collection expressions that mix spreads and slices, index arithmetic inside a slice, and a call whose argument
is itself a call or an expression with an operator. Give each piece a local variable whose name says what it is, one per
line (STR-7, NAM-11), and then combine the named pieces.

Not allowed:

```csharp
return [..script[..index], ..replacement, ..script[(index + marker.Length)..]];
```

Allowed:

```csharp
var endOfMarker = index + marker.Length;
var bytesBeforeMarker = script[..index];
var bytesAfterMarker = script[endOfMarker..];
return [..bytesBeforeMarker, ..replacement, ..bytesAfterMarker];
```

A bare identifier, a constant or a single member access may stay inline. If you are unsure whether an expression is
complex, it is: name the pieces.

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

**UI-1. Anything that can be toggled or stepped can be changed with LEFT and RIGHT.** Where a page lets the user
switch a value, or step it up or down (CLASSIC / EXTENDED, an on/off setting, a number), the arrow keys LEFT and RIGHT and
a controller's pad LEFT and RIGHT change it, as they do on the GAME ADJUSTMENT page. A hotkey or ENTER may do it as well,
but never instead. LEFT goes one way and RIGHT the other; for a two-way switch, either changes it. A page that already
uses LEFT and RIGHT for something else (the developer's page-stepping keys on the attract screens) must say so where it
is handled.

**AUD-1. When ATTRACT MODE SOUND is off, the attract pages are silent: no sound effects and no speech.** The setting is on the
GAME ADJUSTMENT page. While an attract page is showing (the title, the story, the demo and its wave-clear screen, and the
high scores) and the setting is off, every request for a sound effect, a speech line or the wave-end music is ignored. The
app shell sets `Sound.AttractMuted` and `Speech.AttractMuted` every tick, and each service checks its flag before it does
anything. A new way of making a noise (a new service, a new kind of sound) must check the same flag, or it breaks this
rule. A real game, and the pages that are not attract pages (the menu, the settings, the developer tools and its tests),
keep their sound.

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

**PROC-5. Refactoring and bad code are tracked.** When code is changed because it broke this standard, and not because
the game's behaviour had to change, the change is written down in [refactoring-ledger.md](refactoring-ledger.md), in the
same set of commits. This covers a sweep of renames, a split class, repaired comments, and any batch of code that was
written badly and had to be put right, whoever or whatever wrote it. An entry says:
- **what was wrong**, with the rule it broke and how it was found;
- **how it got in**, where the git history shows it. If it was not traced, say "not traced". Do not guess;
- **what was changed**, as a table of old name to new name, so a reader of an old note, handoff or commit can find the
  thing under its new name;
- **what was left alone and why**, so the next reader does not have to find it again.
The older notes and handoffs are a record of what was true when they were written. Do not rewrite them after a rename
(NAM-8 is for the code, the tests and the living docs); the ledger is what joins the old names to the new ones. One small
fix made while a file is open for another reason (PROC-1) needs no entry.

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
grep -rnE 'ToPortPixelsFromArcadePixels\(2\b|ToPortPixelsFromArcadePixels\(2 \*|\* ScreenSize\.PortPixelsPerArcadePixel|/ 256\b|>> 8\b|<< 8\b' src --include=*.cs

# Unit-suffix mistakes (NAM-2)
grep -rnoE '\w*ArcadeTicks\w*' src tests --include=*.cs

# A method that returns a value but does not start with Get or Create (NAM-13), and a bool method that does not
# start with Is or Has (NAM-4). Give the folder to check; each should print nothing. A hit named like a LINQ operator
# (Any, All, Contains, Count, First, Last, Single, Min, Max, Sum, Where, Select) is a method to delete (NAM-13).
D=src/Robotron2084/Entities
grep -rnP --include=*.cs '^ {4}(?:public |internal |private |protected )(?:static )?(?!void\b|bool\b|readonly\b|const\b|class\b|sealed\b|abstract\b|record\b|struct\b|enum\b|interface\b|event\b|new\b|override\b|virtual\b|static\b)[\w<>\[\]?,.() ]+? (?!Get|Create)[A-Z]\w*\(' $D
grep -rnP --include=*.cs '^ {4}(?:public |internal |private |protected )(?:static )?bool (?!Is|Has)\w+\(' $D

# A method that hands a result back through ref or out (NAM-23). Each hit should be a command split in two.
grep -rnP --include=*.cs '^(?!\s*//)[^/]*\b(?:ref|out)\s+(?:var\s+|[\w<>?\[\]]+\s+)?_?\w' $D
# A Get… or Is… method must not write to a field (NAM-23). There is no safe grep for this: read the body of every
# value-returning method you add or change.

# A name that is another thing's name (NAM-25): the usual offenders, each should print nothing. There is no complete
# grep for this: when you add a parameter, check its name against the type names (`ls src/Robotron2084/**/*.cs`).
grep -rnE --include=*.cs '\b(IntVector2|Rectangle|int|var) (player|playfield|point)\b|\bKeyboardState keys\b|\b(var|StripEffect) effect\b|\bSoundSequence sound\b' src
# ...and a bare settings (a PlayerControls named controls is fine), which should also print nothing
grep -rnwE --include=*.cs 'settings' src | grep -v '///'

# A shortened name for an object (NAM-26): each should print nothing. The list is the usual offenders; the rule is the
# test, so also read the parameter list of every method you add.
grep -rnE --include=*.cs '\b(PlayField (field|_field)|TextureSet (sprites|_sprites)|GamePalette (palette|_palette)|GameSession (session|_session)|FieldEntities entities|WaveSpawnContext context|IPlayerInputSource input|ICollisionScene scene)\b' src tests tools
# ...and a parameter called field, which is a keyword inside a property accessor since C# 14
grep -rnE --include=*.cs '\b[A-Z]\w*(<[^>]+>)? field\b[,)]' src tests tools | grep -v '///'

# A Texture2D (or an array of them) whose name does not end in Texture or Textures (NAM-19): each should print nothing.
# A method that makes or loads one (Create..., Load...) is exempt.
grep -rnE --include=*.cs '\bTexture2D(\[\])?\??\s+_?\w*\s*[=;,){]' src tests tools | grep -vE 'Textures?\s*[=;,){]' | grep -vE '\(.*Texture2D'

# A visibility flag that is not called IsVisible (NAM-27): each should print nothing, except `_isShowingArcadeText`, which chooses between two sets of text
grep -rnE --include=*.cs '\bbool\??\s+_?(show|shows|showing|is(Showing|Drawn|Shown)|\w*Showing|\w*Shown)\w*\b|\bIsDrawn\b|\bShowing[A-Z]' src tests tools

# A ternary anywhere but the whole value of a return (FMT-2): each hit is a violation to rewrite with named bools
# and ifs. A return line that is left out of this list still needs a look: its condition and branches must be plain.
grep -rnE --include=*.cs '\S \? [^:]+ : ' src | grep -v -E '^\S+:[0-9]+:\s*return [^?]* \? [^?:]+ : [^?:]+;\s*$' | grep -v -E '///|\?\? |\?\.'
# ...and a ternary that wraps onto several lines (its second line starts with ?)
grep -rnE --include=*.cs '^\s+\? ' src

# The word roll (NAM-24)
grep -rniE --include=*.cs '\b\w*(re-?)?roll\w*\b' $D | grep -v -i -E 'scroll|control|corollary'

# History, plan and milestone references (CMT-3, CMT-5)
grep -rnE "M4\b|M5\b|round [0-9]|Phase [0-9]|PHASE [A-Z]|plan [0-9]|author's report|\(author, 20|playtest 20|used to |The old |was wrong" src --include=*.cs

# Stale vocabulary after renames (NAM-8): add the old word of any rename in the diff
grep -rnwE 'Art|IArtSource|ArtRect|WalkArtIndex|wake' src tests --include=*.cs

# Retired words (NAM-22): the glossary's "Retired words" table, as a search
grep -rnE 'SpecScale|SpecWidth|SpecHeight|SpecPixels|ArcadePixelsPerColumn|nearly every tick|spec-px|spec pixel' src tests --include=*.cs

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

# Names that hide their type (NAM-4, NAM-19): booleans that are not predicates, and names used for
# more than one type. Check each hit by eye.
grep -rnP '^\s+private (static )?(readonly )?bool _(?!is|has|can|was|should|are|have)' src --include=*.cs
grep -rhoP '^\s+private (readonly )?[\w<>\[\]?,]+ _\w+' src --include=*.cs | awk '{print $NF, $(NF-1)}' | sort -u | awk '{n[$1]++; t[$1]=t[$1]" "$2} END{for (k in n) if (n[k]>1) print k":"t[k]}'

# Relative terms with no "from" (NAM-21): every hit needs the thing it is relative to in the name or the summary
grep -rnE 'w*(Offset|Delta|Displacement|Relative)w*' src tests --include=*.cs | grep -v "From"

# Calculated properties (NAM-10): every hit is a property that must become a method
grep -rnP '^s+(public|internal|private|protected) (static |override )*[w<>[]?,.() ]+ [A-Z]w* =>' src --include=*.cs

# Law of Demeter (STR-11): a call made through another object's property
grep -rnoP '(?<![\w.])_?[a-z]\w*\.[A-Z]\w*\.[A-Z]\w*(?=\()' src --include=*.cs

# No trigonometry (NUM-8): there must be no hits
grep -rnE '\b(Math|MathF|MathHelper)\.(Sin|Cos|Tan|Asin|Acos|Atan2?|SinCos)\b' src tests tools --include=*.cs

# Untracked leftovers
git status --porcelain | grep '^??'
```

## Severity

| Level | Meaning | Examples |
|---|---|---|
| **Blocker** | Wrong behaviour, data loss, or a production default that favours tests | a counter bumped twice; `catch (Exception)` that overwrites a save; `ForTesting = true` in a production path |
| **Major** | Violates SOLID/STR in new code, or a comment/summary that states something false | new god-class growth; a hard cast; a stale value in a summary; a new magic number |
| **Minor** | Naming, consistency, formatting, missing unit suffix | `ArcadeTicks` for frames; summary on a field's line |
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
  Why: the prose restates a value and has already drifted from EnforcerGrowUpArcadeFrames.
  Fix: replace "five pictures of 9 frames (40 in all)" with "EnforcerGrowUpArcadeFrames, in steps of EnforcerGrowStepArcadeFrames".
```

End the review with a one-paragraph summary: how many findings at each level, the most important one, and any
question that needs the author.

## What not to flag

- ROM labels, addresses and `notes §NN` references. These are required (CMT-1).
- Deliberate deviations marked "do not revert without asking" (e.g. `Human.BeatIntervalArcadeFrames`), unless the marking is missing.
- Byte tables copied from the ROM (`WaveTable`, `AttractMovieData`), as long as they have a summary giving the ROM
  address. They are data, not magic numbers.
- Style choices the `.editorconfig` and analyzers already enforce. The build reports those.
