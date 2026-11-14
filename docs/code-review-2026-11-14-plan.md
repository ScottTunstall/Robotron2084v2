# Code review plan — 14 November 2026

This is the procedure and the sweep list for a full review of `src/Robotron2084`. It is written so that it can be
handed to a reviewer with no memory of this session and no other tooling than `bash`, `grep`, `awk` and `dotnet`.
It follows `docs/coding-standards.md` Part 2 and cites rule IDs throughout.

Pair it with `docs/code-review-2026-11-14.md`, which is the findings produced by running it today.

## 1. Scope

```bash
cd /c/Source/Robotron2084v2
git log --oneline --since=2026-11-01 | wc -l                  # 166 commits since the last full ledger
git log --name-only --since=2026-11-01 --format= | grep '^src/' | sort | uniq -c | sort -rn | head -40
git diff --name-only HEAD~40 -- src                            # or the range under review
git ls-files --others --exclude-standard 'src/*.cs'            # untracked new code
```

Today's scope was the whole of `src/Robotron2084` (338 files) because nothing was uncommitted, with the deep read
aimed at the files with the most commits since 1 November: `Level/WaveParameterGenerator.cs`, `RobotronGame.cs`,
`Level/PlayField.cs`, `Entities/Gorf.cs`, `Tuning/GorfTuning.cs`, `Level/PlayerSlot.cs`, `Graphics/TextureSet.cs`,
`Level/WaveParameters.cs`, `Level/DifficultyTuning.cs`, `AttractMode/*`, `Input/InputBinding.cs`.

**Rule of thumb:** a file touched since the last ledger is guilty until read. A file untouched since the last
ledger only gets the mechanical scans and any cross-cutting check that names it.

## 2. Baseline

```bash
dotnet build Robotron2084.slnx -c Debug   2>&1 | tail -5
dotnet build Robotron2084.slnx -c Release 2>&1 | tail -5
dotnet test Robotron2084.slnx 2>&1 | tail -5
```

Today: both builds clean, 948 passed, 103 skipped, 0 failed. Record the numbers; a green build proves nothing about
the standard (see §3.1 — the build is green precisely because it ignores a whole class of defects).

## 3. Mechanical scans

### 3.1 The doc-comment lint that the build never runs

`Directory.Build.props` sets `TreatWarningsAsErrors=true` and `src/Robotron2084/Robotron2084.csproj` never sets
`GenerateDocumentationFile`. So the compiler never parses a single `///` comment. Turn it on, warnings off, and it
finds every malformed block, every missing summary, every `<param>` left behind by a rename and every `<see cref>`
that no longer resolves. **This is the highest-yield check in the repo and it is not run.**

```bash
dotnet build src/Robotron2084/Robotron2084.csproj -c Debug --no-incremental \
  -p:GenerateDocumentationFile=true -p:TreatWarningsAsErrors=false -clp:NoSummary > /tmp/xml.log 2>&1
for c in CS1591 CS1570 CS1572 CS1573 CS1574 CS1571 CS1734; do
  printf '%s %s\n' "$c" "$(grep -c "$c" /tmp/xml.log)"
done
grep -E 'CS(1572|1573|1574|1571|1734)' /tmp/xml.log | sed -e 's#\[C:.*##' -e 's#.*Robotron2084.##' | sort -u
grep CS1591 /tmp/xml.log | sed 's#(.*##' -e 's#.*Robotron2084.##' | sort | uniq -c | sort -rn | head -20
```

| Code | Means | Rule |
| --- | --- | --- |
| CS1570 | doc comment is not well-formed XML | CMT-4, CMT-11 |
| CS1591 | public member has no `<summary>` | NAM-20, CMT-1 |
| CS1572 | `<param>` names a parameter that no longer exists | CMT-4 |
| CS1573 | a real parameter has no `<param>` | NAM-20 |
| CS1574 | `<see cref>` resolves to nothing | CMT-4, cref-ledger |
| CS1571 | the same `<param>` twice | CMT-4 |
| CS1734 | `<paramref name="x">` for a parameter that does not exist | CMT-4 |

### 3.2 Disassembly citations (CMT-11, CMT-11a, CMT-11b)

`asm/robomame.asm` writes an instruction line as `7C6F: BD D0 75  JSR $D075` and an equate as `LABEL EQU $5F99`.
A cited address is verified when it appears as a `$XXXX` token, or opens an instruction line, or lies inside a line's
byte span. A cited label must appear in the file at all, whatever the case.

`/c/tmp/cr/cite_audit5.pl` (kept in `tools/review-scans/` with this plan) walks every doc block, splits it into
`Original source:` and `Disassembly:` spans, and reports:

- **A** — a block that cites `Original source:` with no `Disassembly:` bullet. CMT-11 says both, always.
- **B** — inside a `Disassembly:` span, an address that is not in the disassembly.
- **C** — inside a `Disassembly:` span, a `<c>LABEL</c>` that is not in the disassembly.
- **E** — inside a `Disassembly:` span, no address at all (CMT-11b: with no label, cite the address).

```bash
perl tools/review-scans/cite_audit5.pl $(git ls-files 'src/Robotron2084/*.cs' | grep -v obj)
```

**`Audio/**` is excluded from B, C and E.** Its source is the sound board (`VSNDRM3.SRC`) and its addresses are in
the sound ROM; neither file is in this repository, so those citations cannot be checked here. Say so in the report
instead of guessing.

To settle a single label: `grep -ic '\bRTORG\b' asm/robomame.asm` — zero means the label is not the disassembly's,
whatever any document says.

### 3.3 Size and shape (STR-1a, STR-2a)

```bash
for f in $(git ls-files 'src/*.cs'); do n=$(grep -cE '\S' "$f"); [ "$n" -gt 500 ] && echo "$n $f"; done | sort -rn
```

For methods, brace-match from the signature and count non-blank lines inside the braces. Do not count a
`record Foo(...)` or a field initialiser as a method — that mistake cost a false "187-line method" during this
review. CA1502 already fails the build above complexity 10 (`CodeMetricsConfig.txt`), so complexity needs no scan.

### 3.4 Naming and idiom

```bash
# NAM-4 — a bool-returning method that is not a question (the rule bans Can…, Try…, Fits…, Overlaps…)
grep -rnE '^\s*(public|internal)\s+(static\s+)?bool\s+[A-Za-z_]+\s*\(' --include=*.cs src \
  | grep -vE '\bbool\s+(Is|Has)[A-Z]'
# NAM-4/NAM-27 — a bool that cannot be set from outside and does not read as a predicate
grep -rnE '^\s*(public|internal)\s+(static\s+)?bool\s+[A-Za-z_]+\s*(\{|=>)' --include=*.cs src \
  | grep -vE '\bbool\s+(Is|Has)[A-Z]' | grep 'private set'
# NAM-26 — the abbreviations the standard names by name, as declarations. (A plain word grep gives 573 hits and is
# useless; match declarations instead. Four hits today: InputBinding.cs:91 is a GamePadState, the rest are counters.)
grep -rnE '\b(GamePadState|PlayField|Palette|GameSession)\s+(pad|field|palette|session)\b' --include=*.cs src
grep -rnE 'var (pad|field|palette|session) =' --include=*.cs src
# CON-1 — hex written two ways, and citations marked with markdown instead of <c>
grep -rnE '(///|//).*0x[0-9A-Fa-f]{2,}' --include=*.cs src
grep -rnE '^\s*///.*`' --include=*.cs src
# CMT-2 — a notes reference without the section sign
grep -rnE 'notes [0-9]' --include=*.cs src | grep -v 'notes §'
```

### 3.5 Clean today (re-run to confirm, do not assume)

NUM-8 trig, NUM-7 public static arrays, TIME-3 `new Random()`, ERR-1 broad `catch`, `#pragma`/`SuppressMessage`/
`NoWarn`, NAM-14 `post` for electrode, NAM-24 `roll` for a random number, NAM-12 American spelling, one type per
file (STR-3).

## 4. Read each file in scope, in this order

1. **Correctness.** Does the code do what its comments claim? Then check the claims against `asm/robomame.asm`
   byte for byte — not against another comment. A ROM range is only right if the metadata entries chain: e.g. the
   four laser sprites are `03 01 35 BE`, `01 06 35 C1`, `03 06 35 C7`, `03 06 35 D9` at `$35AE`, `$35B2`, `$35B6`,
   `$35BA`, which is 3 + 6 + 18 + 18 bytes, so the pixels run `$35BE`–`$35EA`.
2. **SOLID and STR.** Who owns each fact? A value computed in one place and re-computed in another is two owners
   (`MaxDropsX2` → `(MaxDropsX2 + 1) / 2` in `WaveParameters.CreateFromWave` and again in `DifficultyTuning`). A
   record with 26 bare `int` parameters filled by 25 positional arguments is one argument swap away from a silent
   bug.
3. **Numbers.** Every literal either names itself or quotes its ROM source. Watch for a unit word that is not a
   unit (`ReturnDropIntervalFifths … / 5`) and for the same fact held as a `const` in one class and a bare number
   in another (frame counts in `TextureSet` against `Gorf`/`BerzerkRobot`).
4. **Naming.** Run §3.4 over the file, then read for the harder cases: a name that says the opposite of what the
   code does, a query that also changes something, three names at three levels for one question.
5. **Comments.** Every value in a comment gets checked. Stale comments are this codebase's most common defect.
6. **Consistency with its siblings.** Read the file next door in the same folder. If one documents its public
   properties and this one does not, that is the finding.

## 5. What not to flag

- ROM labels, addresses and `notes §NN` references — CMT-1 requires them.
- Deliberate deviations marked "do not revert without asking" (for example `Human.BeatIntervalArcadeFrames`). A
  deviation with **no** marking is a finding (see M-05 in the findings file).
- Byte tables copied from the ROM (`WaveTable`, `AttractMovieData`) whose summary gives the ROM address — data, not
  magic numbers.
- Anything `.editorconfig` or the analyzers already enforce.
- **The ten settable `Is…` bools** the author chose to leave alone — the attract movie's objects
  (`MovieObject.IsLaser`, `IsOnList`, `IsDead`, `IsMonoActive`, `IsShakingUp`), the sound board's memory
  (`BoardMemory.IsBackground1On`, `IsLaserBallBonusRepeating`, `IsOrganTuneNext`) and the CPU emulator — decision
  table in `docs/coding-standards.md`. They are exempt; do not spend a finding on them. **Read-only** bools are not
  exempt: NAM-4 says a bool with no setter (or a private one) is a predicate and keeps `Is` or `Has`.
- `TryParse` and its family: the .NET idiom outranks the letter of NAM-4 here. Raise it once as a decision, not
  twelve times as findings.
- Findings already open in `docs/code review 1-NOV-2026.md`, `docs/code-review-2026-10-30.md`,
  `docs/terminology-duplicates-2026-10-31.md` and `docs/cref-ledger-2026-10-31.md`. Re-verify them, and report one
  as carried over only if it is still true. As of today these are still open: B-01/B-02 (the invincibility aid is
  on in the shipped game), M-01 to M-03, M-06 to M-09, M-13, M-15 to M-18, m-01 to m-04, m-06 to m-12, N-01 to N-07.

## 6. Report

Format is fixed by Part 2:

```
[<Severity>] <RULE-ID> <path>:<line> — <one sentence of what is wrong>
Why: <what breaks, or what the reader is misled into believing>
Fix: <the change, specific enough to type>
```

Severity: **Blocker** do not ship; **Major** wrong or misleading, including every stale or false comment, because a
comment is where the ROM mapping lives; **Minor** style or shape that costs a reader time; **Note** something the
author must decide. Finish with one paragraph: count at each level, the finding that matters most, and the open
questions.
