---
name: in-game-test
description: Prepares, runs and closes an in-game KSP 1.12 test with a person at the keyboard, in a copy of the install, with build variants, a checklist and a clean-up. Use when a bug, a pull request or a feature needs hands on the game, rounds with build variants, or when the person says they will test it.
---

# In-game test with a person

For anything that needs eyes or hands (a window, a landing, how it feels). Headless runs (`moddev-headless`) and
scripted scenarios (`templates/test-driver/`) come first and prove what a log can prove; this is the part after them.

The commands `moddev-*` and the `templates/` folder come from [ksp-moddev](https://github.com/celino/ksp-moddev).
`moddev-checklist`, `moddev-logsummary` and `moddev-wait` are Python or shell scripts in its `bin/` and also run outside
the container. If the project has its own tools to build test installs or swap builds, use them for the same steps.

Ground rules:
- **Never touch the install the person plays on.** Test in a separate copy.
- **Close the game before changing any file** of the test copy (check that no KSP process is running).
- **The test save is always a fresh copy of a known one**, never the one left behind by an earlier round (a contract that
  was already complete made a first round worthless once).
- **Change one factor per round.** Give each variant a letter and write down what is different in it.
- **Ask before deleting** anything you did not create for this test.

## 1. Where to test
- **A lean copy** (the default when the bug is in one mod): stock KSP plus only the mod, its dependencies and what the
  save needs. Open the save once and check the log for missing parts before the person plays it.
- **A copy of the full mod list**, when the bug depends on the person's own combination. Say why you chose it.
- Known traps: some mods only start from the Space Center and can hang a save opened straight into flight. In a lean copy
  they are not there, so a bug may not show up there either.

## 2. Prepare
Keep one folder for the test, outside the game, with:
- `backup/`: every file of the test copy that a variant will replace, with its path inside the game;
- `RESTORE.txt`: how to undo everything by hand, including the files that did not exist before;
- `save-clean/`: the starting save, never played. Copy it into `saves/<test-name>/` before **each** round;
- `variants/A/`, `variants/B/`...: one build or cfg to compare in each;
- an empty `Screenshots/` in the game (move old ones to `backup/`), so whatever shows up belongs to this test.

## 3. The script for the person
- Steps in Markdown, one `## ` heading per step, turned into a page with `moddev-checklist steps.md`: the person marks
  ok / odd / broken / skipped, adds a note and pastes the results back.
- Each step says how to do it and why. Warn about the traps with `> ` lines (loading a save brings back its difficulty
  options: change them after loading).
- At the top: how to open the game, which save to load and which variant is active. Ask for the clock time when something
  looks wrong.

## 4. After each round (the person closes the game)
1. Copy `KSP.log`, `Player.log` and the screenshots to `rounds/<variant><n>/` **before** the game runs again, which
   overwrites the logs.
2. Put the next variant in place and copy `save-clean/` over the test save again.
3. `moddev-logsummary rounds/A1/KSP.log` for each round; `moddev-logsummary --compare rounds/A1/KSP.log
   rounds/B1/KSP.log` for what changed. `--since` takes the time the person wrote down.

## 5. Read and record
Read their notes, the log summaries and the screenshots (one at a time, shrunk first: a picture stays in the context
until the end). Separate what belongs to the mod, to KSP and to other mods. A mechanism that is not shown by a log or a
test is a **hypothesis**: say so and propose the next test. Re-classify an item when "broken" turns out to be expected
behaviour. Write the analysis with the numbers and the log lines that matter.

## 6. Clean up
After the analysis is written down: follow `RESTORE.txt`, move the test save to the backup, return the old screenshots,
then delete the script and your copies of the test screenshots. Say what is left in place on purpose (for example the build under test,
until the pull request goes out).
