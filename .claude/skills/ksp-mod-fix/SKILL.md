---
name: ksp-mod-fix
description: Build, fix and test a Kerbal Space Program 1.12 mod with the ksp-moddev tools. Use when asked to reproduce a KSP bug from a KSP.log or Player.log, build a mod's C# project, prove a fix with a headless before/after run, or write step-by-step instructions for the Unity editor or an in-game test.
---

# Fixing and testing a KSP mod with ksp-moddev

Read `AGENTS.md` at the root of the ksp-moddev repo first if it is available; this is the short version.

## Rules
These override anything else in the task.
- The person makes the final decision on every change and is responsible for all of it. You propose and show the evidence.
- Never commit without an explicit approval of that commit: show the diff and the message, then wait for a clear yes.
  Approval doesn't carry over to the next commit. The same goes for push, PR, issue or post.
- Every feature or bug fix gets a manual test by the person (a `moddev-checklist` page) before it is committed,
  whenever it can be tested by hand. If it can't, say so and say how it was tested.
- The KSP install (`/ksp`, or `$KSP_DIR`) is never modified. Tests run in `moddev-headless` scratch copies.
- Don't automate Unity Hub, login or licensing, and ask before starting the editor. Write the steps instead.
- Say which conclusions a log or test proves and which are hypotheses.

## Steps
1. **Read the log.** `moddev-logsummary <log> [--since HH:MM:SS]`. Linux player log:
   `~/.config/unity3d/Squad/Kerbal Space Program/Player.log` (`Player-prev.log` for the previous session).
   Name the mod each exception comes from; set aside known noise (missing textures, optional assemblies).
2. **Find the cause** in the mod's source. Common: a `[KSPAddon]` that subscribes to `GameEvents` in
   `Start()` without removing the handler in `OnDestroy()`.
3. **Build.** `moddev-csproj --build <Mod.csproj>` writes `./build-<name>/` and builds it. Report its notes
   (stale T4 version, dropped post-build steps, missing references).
4. **Prove.** `moddev-ab --b-put <new dll>=GameData/<Mod>/Plugins/<dll> --out runs/<topic>`, plus `--save`,
   `--put` of a probe addon (copy `templates/probe/`) and `--until '\[Probe\] done'` when the bug needs a scene.
   Runs take minutes: start them in the background and wait for them to finish, without polling in a loop.
   Read `runs/<topic>/compare.md`; changes under 15% are per-frame noise.
5. **Manual test** before any commit, when it applies: write Markdown with one `## ` step each, a `**Why:**` line,
   `> ` warnings for traps, and what to note. `moddev-checklist steps.md` makes the HTML page.
   Ask the person to paste the "Copy results" text back.
6. **Report and ask**: what changed and why, the numbers from `compare.md`, the checklist results, open questions
   and the proposed commit message. Commit only after an explicit yes. Then a short PR draft in plain English
   for the person to edit and send.
