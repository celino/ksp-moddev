# moddev-logsummary

Turns a `KSP.log` or a `Player.log` (often tens of megabytes) into a short Markdown summary: what threw, which mod it
came from, how often, and what failed to load. It only reads the log. It needs Python 3 and nothing else, so it also
runs outside the container (`bin/moddev-logsummary`).

```
moddev-logsummary [--since HH:MM:SS] [--top N] [--mod REGEX]... [--known FILE [--show-known]] KSP.log
moddev-logsummary --compare [--all] [--since HH:MM:SS] [--top N] [--known FILE] A/KSP.log B/KSP.log
```

`moddev-logsummary --help` prints a short version of this page.

## Which option do I need?

| you want to | use |
|---|---|
| know what is wrong in a log | no options |
| ignore what happened before a moment ("it broke around 21:30") | `--since 21:30:00` |
| see only the exceptions that involve one mod | `--mod RoverScience` |
| stop seeing noise you have already checked | `--known known.txt` |
| check what a known file is hiding | compare the output with and without `--show-known` |
| see more (or fewer) rows | `--top 50` |
| see what a change did | `--compare before.log after.log` |
| see even the small differences between two runs | add `--all` |

## Which log

| log | where (Linux) | timestamps |
|---|---|---|
| `KSP.log` | the KSP folder | yes |
| `Player.log`, and `Player-prev.log` for the session before | `~/.config/unity3d/Squad/Kerbal Space Program/` | no |

Without timestamps the `first` column is empty, the time span says so, and `--since` is refused.

## Reading the output

First, the header:

| line | meaning |
|---|---|
| `time span` | first and last timestamp that were counted |
| `reached main menu` | `yes` and the time of the first scene change into the main menu, or `no`. It comes from the "Scene Change" lines, so with `--since` after that moment it says `no` |
| `scene changes` | each kind of change, with `×N` when it happened more than once |
| `exceptions` | total, and how many distinct rows; also the total and distinct `[ERR]` lines |

Then the tables, each sorted by count and cut at `--top` rows. `none` means nothing was counted.

| table | one row is | columns |
|---|---|---|
| Exceptions by type and first mod frame | one exception type thrown at one place | `where` is the first stack frame that is not from Unity, Mono, `System` or the log handlers. If every frame is one of those, it is the top frame; `?` means there is no stack |
| Exceptions inside GameEvents handlers | one event, handler and exception | `thrown at` is the top frame: inside Unity it often means the handler's object was already destroyed (an event nobody removed) |
| `[ERR]` lines | messages that are equal after masking | numbers show as `N`, addresses and long hexadecimal ids as `#`, long lines are cut |
| Assemblies not found or not loaded | one loader message | often only an optional dependency that is not installed |

`first` is the time the row first appeared. KSP writes an exception inside a GameEvents handler twice; it is counted
once, in the handlers table. The "exceptions" total is the sum of the two exception tables, before `--top` cuts them.

A summary with no exceptions says only that the log has none in the part that was read. It does not say the mod works.

## Options

### `LOG`

One log, or two with `--compare`. Bytes that are not valid UTF-8 are replaced, not an error.

### `--since HH:MM:SS`

Skips everything before the first line stamped at that time or later. Write two digits each (`09:05:00`, not `9:05`).
It applies to both logs with `--compare`.

Limits: the log has times but no dates, so in a session that crossed midnight, `--since 00:10:00` matches the first
line of the evening before and skips nothing. It does not work on `Player.log`.

### `--top N`

How many rows each table shows, and how many lines "Lines matching --mod" shows (the **last** N). With `--compare` it
limits the "What changed" table and says how many rows were left out. Default 15; it must be 1 or more. It never changes
the totals.

### `--mod REGEX` (repeatable)

Narrows the summary to one subject. `REGEX` is a Python regular expression that ignores case. With several `--mod`,
matching any one of them is enough.

- The two exception tables show only the exceptions whose **message or stack trace** matches. Any frame counts, so a
  mod deeper in the stack is found even when `where` names another one.
- A new header line says how many matched: `exceptions matching --mod: 2 of 40`.
- A new section, **Lines matching --mod**, lists the log lines of any level whose **message** matches. It does not
  search stack frames, so it can show 0 lines while the tables show exceptions.
- Everything else stays complete: the totals, the `[ERR]` table and the assemblies table.
- It has no effect with `--compare`.

A mod often logs under more than one name (folder, DLL, namespace, log tag): `--mod RoverScience --mod RoverSci`.
`0 of N` means no exception mentions the pattern. It does not prove the mod had no part in them.

### `--known FILE`

Hides the entries you have already checked, so what is left is what is new. The file has one Python regular expression
per line:

```
# optional dependency we don't install
ADDON BINDER: Cannot resolve assembly: SomeOptionalMod
Cannot find a PartModule of typename 'KethaneWetMassIndicator'
(?i)texture '.*' not found
```

- A line that **starts** with `#` is a comment, and empty lines are skipped. There are no comments at the end of a line:
  there, `#` is part of the regex. Spaces around a line are removed.
- It is **case-sensitive** (unlike `--mod`). Start a line with `(?i)` to ignore case.
- It is searched in the **first line** of `[ERR]` and `[EXC]` entries (and, in `Player.log`, of exception and
  "Exception handling event" lines), never in the stack trace. Other levels are never hidden.
- Match the text as it is in the log, not the masked text of the table: real numbers, not `N`.
- Hidden entries leave the totals and every table, including the assemblies and "Lines matching --mod". A single-log
  summary ends with how many were hidden.
- Works with `--compare`, for both logs; the comparison does not print the hidden count.

Keep the patterns narrow. A line such as `NullReferenceException` hides new bugs together with the old one.

### `--show-known`

Turns `--known` off: every entry is counted and shown as usual, with no mark on the ones the file matches. It lets you
keep the rest of a command unchanged and compare the two outputs. Without `--known`, it does nothing.

### `--compare A B`

What changed from log A (usually before) to log B (after). It prints a small table with main menu, exceptions, `[ERR]`
lines and assemblies for both, then **What changed**: every row whose count differs, with its count in A and in B.
Exceptions come first, then assemblies, then `[ERR]` lines; inside each group, the biggest change first.

- `A` = 0: new in B. `B` = 0: it went away.
- An exception is "the same row" when type and place are the same. If your change moved the frame where it is thrown,
  it shows as one row gone and one new.
- Errors logged every frame differ between any two runs, because one lasted longer. When both counts are above zero and
  differ by **less than 15%**, the row is only counted, in a note under the table.
- Scene changes are not compared. Check that both runs went through the same scenes, or the difference is not your
  change.

### `--all`

With `--compare`, lists the differences under 15% in the table too. Without `--compare`, it does nothing.

## Exit status

| code | meaning |
|---|---|
| 0 | the summary was printed. **The log may be full of exceptions:** the code does not say whether it is clean |
| 1 | a log or the `--known` file could not be read, or a regex in that file is invalid |
| 2 | wrong command line: unknown option, wrong number of logs, invalid `--mod` regex, `--top` or `--since`, or `--since` on a log without timestamps |

## For AI assistants

- Run this instead of reading a whole log. Open the log itself only for the lines around a row of the summary, with a
  limit.
- Run it without `--mod` first: the cause is often in another mod, or in an `[ERR]` line above the exception.
- Do not use the exit status as pass or fail. Read the totals and the rows, or compare against a run without the change.
- Report what you filtered: `--since`, `--known` and `--top` all leave things out.
- Counts of errors logged every frame follow how long the game ran. Compare whether they are there, not the exact number.
