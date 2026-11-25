---
name: in-game-test
description: Prepares, runs and closes an in-game KSP test with a person at the keyboard, using ksp-profile and ksp-test (or plain copies of the install). Use when a bug, a pull request or a feature needs hands on the game, rounds with build variants, or when the person says they will test it.
---

# In-game test with a person

For anything that needs eyes or hands (a window, a landing, how it feels). Headless runs (`moddev-headless`,
`templates/test-driver/`) come first and prove what a log can prove; this is the part after them.

Tools: `ksp-profile` (a lean install with only the mods of the test) and `ksp-test` (backups, save copy, build variants,
rounds, clean-up). Both print `help` and `help COMMAND`. If they are not installed, the same steps work with copies
of the install and of the save made by hand. Say so and keep the backup rule.

Ground rules:
- **Never touch the install the person plays on.** Test in a separate copy. Every command that changes files needs KSP
  closed; the tools refuse otherwise.
- **The test save is always a clean, known copy** (`--save <folder>`), never a "live" save left behind by earlier rounds
  (a contract that was already complete made round A worthless once).
- **Change one factor per round.** Give each variant a letter and write down what is different in it.

## 1. Where to test
- **A profile** (the default when the bug is in one mod): `ksp-profile create <topic> --mod <ckan-id>... --save <save> --start ksc|flight --with-save-mods`,
  then `ksp-profile check-save <topic>` must report 0 missing parts. Mods that are not in CKAN: `--add <folder>`.
- **The full install**, when the bug depends on the person's own mod combination. Say why you chose it.
- Known traps: mods that only start from the Space Center can hang a save opened straight into flight; some mods hang
  saving with a kerbal on EVA. In a profile they are not there.

## 2. Prepare (in the topic's folder)
```
ksp-test prepare <topic> --game <install> --swap <file-that-changes>... [--new <new-file>] --save <save> --name <test-name> --scene ksc|flight
ksp-test variant add A --file <relative-path>=<build/A/file>      # one per build or cfg to compare
ksp-test variant use A
```
`prepare` backs up what it will change and writes a `RESTORE.txt` with the manual undo. `variant use` restores the clean
save copy every time, so each round starts from the same state.

## 3. The script for the person
- Steps in Markdown, turned into a page with `moddev-checklist`: ok / odd / broken / skipped plus a note.
- Each item says how to do it and why. Warn about the traps (loading a save resets the difficulty options; change them
  after loading).
- At the top: the command that opens the game, and which variant is active.

## 4. After each round (the person closes the game)
```
ksp-test round <variant><n>       # keeps KSP.log, Player.log, screenshots and summary.txt in rounds/<name>
ksp-test variant use <next>       # swaps the build and restores the clean save
```
With a profile, `ksp-profile run` already keeps the log and runs `verify`. If `verify` warns that a shared file was
written, stop and check the install.

## 5. Read and record
Read their notes, `rounds/*/summary.txt` and the screenshots (one at a time, shrunk first: a picture stays in the context
until the end). Separate what belongs to the mod, to KSP and to other mods. A mechanism that is not shown by a log or a
test is a **hypothesis**: say so and propose the next test. Re-classify an item when "broken" turns out to be expected
behaviour. Write the analysis with the numbers and the log lines that matter.

## 6. Clean up
`ksp-test clean` puts the files back, moves the test save to the backup and returns the old screenshots. Delete the script
and the screenshots once the analysis is written down. `ksp-profile remove <topic>` when the work is done.
