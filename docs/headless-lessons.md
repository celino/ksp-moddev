# Headless KSP: lessons from hundreds of test rounds

Things that cost hours the first time. Each one was met while testing real mods with `moddev-headless` or on a
desktop GPU, and each has a cheap way around it. They are written down so you (or your assistant) can skip them.

## Where to run what

- **The container is for the menu, the Space Center, the editors and plain logic.** It renders in software
  (llvmpipe). Flight scenes over terrain, especially with a rover on the ground, may never get ready there, and
  the log can stay silent for an hour. For anything that needs flight, run on the host GPU in a window.
- **Lower graphics settings make it worse, not better.** Changing them invalidates the texture cache that
  KSPCommunityFixes builds, and the next start is slower.
- **A first run from a new profile can wait for a click.** KSPCommunityFixes asks to opt in to its texture cache and
  waits. Pre-fill `GameData/KSPCommunityFixes/PluginData/PNGTextureCache.cfg` with
  `userOptInChoiceDone = True` and `textureCacheEnabled = True`.
- **KSPCommunityFixes' fast loader can stall at "configs 0" in the container** when the install has no texture cache
  (only a GPU run creates it). Remove `GameData/KSPCommunityFixes` for container rounds.
- A paused container (for example `docker pause` while you play something else) looks like a slow game. Look for
  gaps in the log timestamps before you blame the mod.

## Nobody clicks, so nothing saves

Bugs in `ScenarioModule.OnSave`, scene changes and "open the save straight into flight" only appear if something
does those things. A test-only addon has to save, change scene and resume the save itself, and log what happened.
(`templates/test-driver/` does this from a small config, with its `open` and `cycle` steps.)

- **QuickStart always goes through the Space Center** before flight, so a mod that starts at the KSC never sees a
  "save opened in flight" bug with it. Remove QuickStart for those tests and resume the save from the main menu.
- Use `KSPAddon.Startup.EveryScene`. `AllGameScenes` skips the main menu.
- `GameEvents.*` do not exist yet in `Startup.Instantly`; subscribe from the main menu.
- `EventVoid.Add` needs an **instance** method. A static method has a null `Target` and throws inside the game.
- `onFlightReady` does not fire when a save is opened straight into flight. Treat "flight scene, `ActiveVessel`
  not null and not `packed`" as ready, whichever comes first.
- `Debug.Log` inside a `logMessageReceivedThreaded` handler never reaches `KSP.log`; write to your own file.
- An exception caught by your own `try/catch` is logged as a plain message. If you are hunting an exception,
  catch it yourself and log the stack explicitly.

## "The log stopped" is not "the game hung"

- A coroutine that dies silently (for example a `MissingMethodException`) leaves the game running at full frame
  rate with a quiet log. `KSP.log` also reaches the disk late. Judge a hang by **frames**, not by log lines: a test
  addon with a background thread that writes `heartbeat.txt` (frame count, coroutine ticks, memory, last Unity
  line) settles it in seconds.
- Do not wait for the very last line. Stop a fixed time after an earlier marker.
- An iterator method (`yield`) that calls something missing fails **as a whole** at JIT time. Keep the heavy logic
  outside coroutines.

## Building test addons

- **Compile against KSP's own `mscorlib.dll`**, not the system one
  (`-nostdlib -r:KSP_Data/Managed/mscorlib.dll ...`). With the system one, `Split(',')` became
  `Split(char, StringSplitOptions)` and failed at run time.
- **Reproducible DLLs:** `mcs` produces the same bytes from the same sources if the **file name** is the same, so a
  CI that rebuilds the DLL and compares it with `cmp` with the committed one is valid.

## Hand-written `.craft` files

- Part names use `.` where the config uses `_` (`hc.navcam`), or the game says "Ship file error!" with no detail.
- `srfN` and `attN` need the complete format (`srfAttach,<part_uid>_<pos>_<dir>_<pos>_<dir>`). An
  `IndexOutOfRange` in `KSPUtil.GetAttachNodeInfo` means the node format is wrong.
- A vessel placed with `FlightGlobals.SetVesselPosition` needs the height **above the terrain** (`asl = false`).
  With `asl = true` the terrain was added twice and a kerbal fell from 2.6 km.
- An experiment that "doesn't collect" and logs nothing: check `usageReqMaskInternal` (4 = crew inside that part).
  `DeployExperiment` only shows an on-screen message.
- `TimeWarp.SetRate` goes back to x1 after about 42 game hours; ask again on every step.

## Housekeeping

- `pgrep -f` and `pkill -f` match **your own shell** when the pattern is on its command line. A loop like
  `while pgrep -f "x.sh"` started from that shell waits forever. Chain the rounds in one script, or wait on a PID.
- `ps -C KSP.x86_64` also lists the KSP running inside your containers, not only the one you are playing.
- Do not poll with `sleep` and `tail`. `moddev-wait --scene FLIGHT` (or a regex) answers with one line, and says so
  if the game quit.
- On a desktop with a screensaver, a long window run is blanked after a while unless you keep the screen awake.
