# One bug, start to finish: an AI assistant and ksp-moddev

This is how a real fix went, with the numbers from the logs. The mod is
[RoverScience Continued](https://github.com/linuxgurugamer/RoverScience-Continued),
the assistant is Claudinho (Claude, by Anthropic), and the human is me. It shows
what the tools in this image are for, and where a person still has to be in the loop.

<!-- TODO: link the pull request once it is open -->

## The symptom

After a session of my career game, `Player.log` was huge. The first thing the
assistant ran was the log summary (columns trimmed here):

```sh
moddev-logsummary Player-prev.log
```

```
| count | event                  | handler class | exception                      |
|-------|------------------------|---------------|--------------------------------|
| 7830  | VesselSituation.OnLand | ROC_Class     | System.NullReferenceException  |
```

7,830 exceptions in a few minutes, almost all during one flight to the Island
Airfield, with only three flight scene loads in the whole session.

## The cause

`ROC_Class` is a `[KSPAddon]` for the flight scene. KSP creates a new one on every
flight scene load and destroys the old one. The class subscribes to
`GameEvents.VesselSituation.onLand` in `Start()` and never unsubscribes, so every
destroyed copy stays subscribed. `onLand` fires on every change to LANDED (a plane
bouncing on a runway fires it a lot), and each dead copy then throws from
`StartCoroutine`.

The fix is six lines: an `OnDestroy()` that removes the handler, the same thing the
mod's main class already does for its own events.

## Building it

The mod's project file expects Visual Studio on Windows, a `KSPDIR` variable and a
couple of `.bat` files after the build. Instead of editing it:

```sh
moddev-csproj --build /work/RoverScience/RoverScience/RoverScience-LGG.csproj
```

```
  old-style project, 26 source files, 4 game/mod references
  note: dropped PostBuildEvent (it ran scripts from the original build; copy the output by hand)
  note: AssemblyVersion.tt is a T4 template; the .cs it generates is used as is,
        so a version number in it may be stale
  RoverScienceCont -> build-RoverScience-LGG/out/RoverScienceCont.dll
```

That last note mattered: the DLL reported version 2.3.5.8 while the release was
2.3.5.10. Not a problem with the fix, but the kind of thing that confuses a tester.

## Proving it

The bug needs a flight scene, a scene change and a landing, so the main menu is not
enough. The assistant wrote a small test addon from `templates/probe/` that loads a
save, enters flight three times, fires `onLand` ten times, logs `[ROCPROBE] done`
and quits. Then one command ran the game twice, without a screen, with only the DLL
different:

```sh
moddev-ab --save default --until '\[ROCPROBE\] done' --timeout 1800 \
  --put /mods/RoverScience=GameData/RoverScience \
  --put probe/out/RocLeakProbe.dll=GameData/zzRocLeakProbe/RocLeakProbe.dll \
  --b-put build/out/RoverScienceCont.dll=GameData/RoverScience/Plugins/RoverScienceCont.dll
```

About nine minutes per run. The top of `compare.md`:

```
| A  | B | kind    | what                                                              |
|----|---|---------|-------------------------------------------------------------------|
| 20 | 0 | handler | NullReferenceException in ROC_Class handling VesselSituation.OnLand, |
|    |   |         | thrown at UnityEngine.MonoBehaviour.IsObjectMonoBehaviour         |
```

Two dead copies times ten events: 20 before, 0 after. Both runs also show 10
exceptions from the live copy, because headless no vessel loads and the event fired
with a null vessel. That's the test's doing, and the PR text says so.

## What the assistant got wrong

All of these were caught by a test or by my review before anything left my machine:

- The first test addon used `FlightGlobals.ActiveVessel`, which stays null in a
  headless game. The run "passed" without testing anything. It was fixed to fall
  back to any loaded vessel and to log which one it used.
- The first version of the log summary counted each of these exceptions twice
  (KSP logs them twice) and reported 15,660. A count done by hand the day before
  didn't match, which is how it was found.
- The first headless runner linked to the game files instead of copying them. KSP
  opens its own files for writing, even the ones it only reads, so the game
  couldn't start from a read-only install.
- The first before/after comparison put the real result in tenth place, under
  per-frame errors whose counts only differed because one run lasted a few
  seconds longer.

## Where the person comes in

The final decision is always mine, and so is the responsibility for everything that
goes out, the lines the assistant wrote included. In practice:

- **Deciding.** Which bug is worth a PR, whether the maintainer is active, what goes
  public. The assistant drafts; I edit and send.
- **Approving every commit.** The assistant shows the diff and the commit message
  and waits for my explicit OK, each time. Approving one commit doesn't approve the next.
- **Testing by hand before committing.** Every feature or fix gets a manual test
  first, whenever it can be tested by hand. The headless run proves the mechanism;
  the manual test proves it works in a real game. The assistant writes the steps
  and I follow them, marking each one ok, odd, broken or skipped. See
  [examples/test-roverscience-fix.md](examples/test-roverscience-fix.md) and
  `moddev-checklist`. The same goes for the Unity editor, which an AI shouldn't drive
  ([examples/unity-kspedia-bundle.md](examples/unity-kspedia-bundle.md)).
- **Reading the diff.** Six lines here. That's the point: a small fix that's easy to
  review.

If you want your own assistant to work this way, point it at [AGENTS.md](../AGENTS.md).
Claude Code users can also copy `.claude/skills/ksp-mod-fix/` into their mod's repo
or `~/.claude/skills/`.
