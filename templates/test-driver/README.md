# test-driver

A test-only addon that plays a **scenario written in a config file** and ends the log with one line you can grep:
`[TestDriver] RESULT PASS`, `RESULT FAIL (n of m checks passed, ...)` or `RESULT INCONCLUSIVE`. For tests that must run without a person
(overnight, in a loop, before a release). It is the next step after [`templates/probe/`](../probe/), which is one
hard-coded run: here the steps live in `scenario.cfg`, so a new test is a new text file, not a new build.
Never ship it with a mod.

Run both on your own computer: `build.sh` needs Mono's `mcs`, which the ksp-moddev image does not have.

```sh
./build.sh /path/to/KSP                                  # out/TestDriver.dll (mcs against KSP's own libraries)
SAVE_FROM=/path/to/KSP/saves/mysave ./run.sh /path/to/KSP scenario.cfg
```

`run.sh` copies the driver (and the save) into the install, opens the game in a window, waits for it to close, prints the
`RESULT` line and the failed `ASSERT` lines, and takes everything out again (the screenshots of `shot` steps stay in
`Screenshots/`). The save given in `SAVE_FROM` is copied and made writable; the source is not touched.

| Exit | Meaning |
|---|---|
| 0 | PASS: `RESULT PASS`, at least one `ASSERT PASS`, no `[TestDriver] ... FAIL` line |
| 1 | FAIL: `RESULT FAIL`, **any** `[TestDriver] ... FAIL` line (also `FAIL: no offer after 240 s` from a step of yours), or more than `MAX_EXC` exceptions |
| 2 | no result: crash, hang, `MAX_S` timeout, no main menu in `MENU_S` seconds |
| 3 | INCONCLUSIVE: no failure, but nothing was proved (no `expect` passed). A test that tested nothing is not green. |

Settings (environment): `MAX_S` (default 900), `MENU_S` and `MAX_EXC` (default off), `KEEP_LOG=1` (keeps `KSP.log`,
`Player.log`, `heartbeat.txt` and `result.txt` in `OUT_DIR`, default `./testdriver-<scenario>-<date-time>`), `KSP_LAUNCH`
(a wrapper in front of the game), `STOP_S` (seconds to wait for the game to quit before KILL, default 60). With the 140
mods of a full install, one round took about 5 minutes. On a timeout the whole process group of the game is stopped
before anything is removed, also when `KSP_LAUNCH` is a wrapper that quits first.

To wait from another shell: `moddev-wait --log /path/to/KSP/KSP.log --idle 300 'RESULT (PASS|FAIL|INCONCLUSIVE)'`
(add `--from-start` if the round may be over already). `tools/test-templates.sh` tests `run.sh` with a fake KSP.

## Steps

One `STEP { do = ... }` node per step, one key per line (KSP's config reader does not take several on one line):

| do | keys | what it does |
|---|---|---|
| `wait` | `seconds` | pause |
| `log` | `text` | a line in the log |
| `open` | `scene` | `SPACECENTER`, `SPH`, `VAB`, `TRACKSTATION`, `MAINMENU`; waits until the scene is ready |
| `cycle` | `scenes` | `open` each scene of the comma list, in turn |
| `var` | `key`, `set` or `from` | `from = funds, reputation, science, scene, vessels, kerbals`; `from = type`, `of = Namespace.Type` gives 1/0; `from = static`, `of = Namespace.Type.Member` reads a static field, property or method |
| `expect` | `key`, `value`, `op` | `ASSERT PASS/FAIL`; `op` is `eq` (default), `ne`, `contains`, `ge`, `le`. Only a passed `expect` counts as proof for PASS |
| `save` | | save the game now |
| `shot` | `name` | `Screenshots/testdriver-<scenario>-<name>.png` |

| `bounce` | `scenes`, `times`, `settle` | back and forth between two scenes with a short settle |
| `force` | `of`, `value`, `hold` | set a static field or property (`Namespace.Type.Member`) and read it back (`ASSERT`); no `value` = `True`, held every frame |
| `force` | `scenario`, `field`, `value` | set a field of a loaded `ScenarioModule` and read it back |
| `commnet-diag` | | logs every `CommNetHome` (name, lat/lon, distance to the active vessel, `isKSC`, origin) and the active vessel's `IsConnected`, `SignalStrength`, `IsConnectedHome`, `ControlPath` |
| `expect` | `state` | a proof about the game state: `antenna-extended`, `kerbal-in-roster` (`name`, `status`), `crew-active`, `parachute-armed`, `docking-port-free`, `vessels` (`value`: vessels with a docking port in physics range), `commnet-connected` (`value`) |
| `expect` | `var = name=value` | short form of `key` + `value` |

### Flight steps

| do | keys | what it does; proof |
|---|---|---|
| `launch` | `craft` (in the save's `Ships/` or an absolute path), `site`, `crew` | onto the pad with Available crew that is **not inactive** (a CrewRandR vacation Kerbal leaves the vessel without control); `var crew` = the names |
| `focus` | `vessel` | opens the flight scene on a vessel of the save |
| `fly` | `seconds`, `throttle`, `stage` | full throttle, next stage, a log line every 10 s |
| `land` | `seconds`, `chutes`, `warp` | deploys the parachutes and waits; **proof:** `LANDED`/`SPLASHED` after `FLYING`/`SUB_ORBITAL` |
| `dock` | `target`, `gap`, `seconds`, `direct` | the active vessel's free port is turned and put `gap` m from the target's free port; **proof:** `onPartCouple`/`onDockingComplete` fired and both ports are in one vessel. Start from a save with the two vessels already close (a few tens of metres) in orbit |
| `recover` | `vessel`, `seconds` | recovers the active (or named) vessel; **proof:** `onVesselRecovered` for it |
| `eva` | `name` | that Kerbal (or the first) goes on EVA; **proof:** the active vessel is that Kerbal |

Assert what the scenario prepared before using it (`expect state = docking-port-free`, `parachute-armed`, `crew-active`):
a round that starts from a wrong state proves nothing. The flight steps, `commnet-diag` and the state checks ran in
KSP 1.12.5 (landing, docking, recovery and EVA passed on their first round); `force` was only compiled.

Known limits of the state checks (they check less than their names say):
- `parachute-armed` passes for any parachute that is not cut, including one still stowed;
- `kerbal-in-roster` checks the name and `status`, not the type: an `Unowned` Kerbal with `status = Available` passes;
- `land` takes the first `LANDED`, even in a bounce (`FLYING -> LANDED -> FLYING` in the same second).

Your mod's own steps go in a second `.cs` file (`partial void AddSteps() { steps["mystep"] = MyStep; }`; `build.sh` builds
every `.cs` here, `EXTRA_CS` adds more). A copy elsewhere can keep its own `build.sh` (template `.cs` + its file) and call
this `run.sh` with `DRIVER_DLL=its/out/TestDriver.dll` and `EXTRA=folder` (packs, crafts).
Inside your steps (read your mod's state, call it, change it), use `Prove(...)` for the check that is the point of the
test (it counts towards PASS, like `expect`) and `Assert(...)` for plumbing (it can fail the round, never pass it).

Settings at the top of the scenario: `save` (leave it out to run from the main menu), `settle` (seconds to wait after
each scene), `timeout`.

## What is built in, and why

- **The runner lives on a `DontDestroyOnLoad` object.** A coroutine on a scene's addon dies with the scene, silently, and the
  round "hangs" with the game running.
- **"Scene ready" is `GameEvents.onLevelWasLoadedGUIReady`.** `HighLogic.LoadedScene` already holds the target while the
  scene is still loading. Waiting on it passed at once, minutes too early.
- **A step that throws** is logged with the stack, counted as a failure, and the scenario goes on.
- **Heartbeat:** a background thread writes `GameData/zzTestDriver/PluginData/heartbeat.txt` every 5 s: frames, coroutine
  ticks, memory, scene, step. A quiet log with frames still rising is a dead coroutine, not a hung game.
- **Watchdog:** after `timeout` seconds the round ends with `RESULT FAIL`; the result is also in `PluginData/result.txt`.
- **No PASS without proof:** a round with no failure and no passed `expect`/`Prove` ends `RESULT INCONCLUSIVE`. Scenes
  that load and saves that open are not a test of your mod (a regression once reported 7 of 7 green with one round that
  had checked nothing).
- **Build:** `-nostdlib` against KSP's own `mscorlib` (see [docs/headless-lessons.md](../../docs/headless-lessons.md)).

## Not covered

Flying a real trajectory (no autopilot: `fly` is full throttle and staging), rendezvous (start `dock` from a save with
the vessels already close), screens that need a click, anything that needs the pixels to be judged by eye.
