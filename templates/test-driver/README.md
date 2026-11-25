# test-driver

A test-only addon that plays a **scenario written in a config file** and ends the log with one line you can grep:
`[TestDriver] RESULT PASS` or `[TestDriver] RESULT FAIL (n of m checks passed)`. For tests that must run without a person
(overnight, in a loop, before a release). It is the next step after [`templates/probe/`](../probe/), which is one
hard-coded run: here the steps live in `scenario.cfg`, so a new test is a new text file, not a new build.
Never ship it with a mod.

```sh
./build.sh /path/to/KSP                                  # out/TestDriver.dll (mcs against KSP's own libraries)
SAVE_FROM=/path/to/KSP/saves/mysave ./run.sh /path/to/KSP scenario.cfg
```

`run.sh` copies the driver (and the save) into the install, opens the game in a window, waits for it to close, prints the
`RESULT` line and the failed `ASSERT` lines, and takes everything out again. Exit code 0 = PASS, 1 = FAIL, 2 = no result.
To wait from another shell: `moddev-wait --log /path/to/KSP/KSP.log 'RESULT (PASS|FAIL)'`.

## Steps

One `STEP { do = ... }` node per step, one key per line (KSP's config reader does not take several on one line):

| do | keys | what it does |
|---|---|---|
| `wait` | `seconds` | pause |
| `log` | `text` | a line in the log |
| `open` | `scene` | `SPACECENTER`, `SPH`, `VAB`, `TRACKSTATION`, `MAINMENU`; waits until the scene is ready |
| `cycle` | `scenes` | `open` each scene of the comma list, in turn |
| `var` | `key`, `set` or `from` | `from = funds, reputation, science, scene, vessels, kerbals`; `from = type`, `of = Namespace.Type` gives 1/0; `from = static`, `of = Namespace.Type.Member` reads a static field, property or method |
| `expect` | `key`, `value`, `op` | `ASSERT PASS/FAIL`; `op` is `eq` (default), `ne`, `contains`, `ge`, `le` |
| `save` | | save the game now |
| `shot` | `name` | `Screenshots/testdriver-<scenario>-<name>.png` |

Your mod's own steps go in `Exec()` (read its state, call it, change it) with `Assert(...)`. Settings at the top of the
scenario: `save` (leave it out to run from the main menu), `settle` (seconds to wait after each scene), `timeout`.

## What is built in, and why

- **The runner lives on a `DontDestroyOnLoad` object.** A coroutine on a scene's addon dies with the scene, silently, and the
  round "hangs" with the game running.
- **"Scene ready" is `GameEvents.onLevelWasLoadedGUIReady`.** `HighLogic.LoadedScene` already holds the target while the
  scene is still loading. Waiting on it passed at once, minutes too early.
- **A step that throws** is logged with the stack, counted as a failure, and the scenario goes on.
- **Heartbeat:** a background thread writes `GameData/zzTestDriver/PluginData/heartbeat.txt` every 5 s: frames, coroutine
  ticks, memory, scene, step. A quiet log with frames still rising is a dead coroutine, not a hung game.
- **Watchdog:** after `timeout` seconds the round ends with `RESULT FAIL`; the result is also in `PluginData/result.txt`.
- **Build:** `-nostdlib` against KSP's own `mscorlib` (see [docs/headless-lessons.md](../../docs/headless-lessons.md)).

## Not covered

Flight (needs a vessel: load a save straight into flight, or use a mod such as SceneCycle for that), screens that need a
click, anything that needs the pixels to be judged by eye.
