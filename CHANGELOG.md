# Changelog

## Unreleased

- `templates/test-driver/`: flight steps `launch` (only Available, not inactive crew), `focus`, `fly`, `land`, `dock`,
  `recover`, `eva`; `force` (static member or `ScenarioModule` field, read back); `commnet-diag`; `expect state = ...`
  (antenna, roster, crew, parachute, docking port, vessels, CommNet); steps of your own in a second file
  (`partial void AddSteps()`), and `build.sh` builds every `.cs` in the folder
- `templates/test-driver/run.sh`: `DRIVER_DLL` (a copy's own build) and `EXTRA` (a folder copied next to the driver),
  so a copy calls this `run.sh` instead of keeping its own
- `templates/test-driver/`: a round with no failure and no passed `expect` (or `Prove` in a step of your own) ends
  `RESULT INCONCLUSIVE`, and `run.sh` exits 3 for it; a `RESULT PASS` without any `ASSERT PASS` line is also 3
- `templates/test-driver/run.sh`: any `[TestDriver] ... FAIL` line (e.g. an expected offer that never came) makes the
  round FAIL (exit 1), even when the driver said `RESULT PASS`
- `templates/test-driver/run.sh`: `MENU_S` (no main menu in N seconds: exit 2) and `MAX_EXC` (more than N exceptions in
  `KSP.log`: exit 1) stop a round early; both off by default
- `templates/test-driver/run.sh`: `KEEP_LOG=1` keeps `KSP.log`, `Player.log`, `heartbeat.txt` and `result.txt` of the
  round in `OUT_DIR`
- `templates/test-driver/run.sh`: on a timeout, stops the game's whole process group and waits for it (`STOP_S`) before
  removing anything; before, with a wrapper in `KSP_LAUNCH`, the game stayed open after "removed"
- `templates/test-driver/run.sh`: a save path with spaces is removed whole (unquoted, its pieces were removed instead),
  and the copied save is made writable
- `moddev-wait --idle N`: exits 3 with `idle: no new line for Ns; last line: ...` when the log stops; off by default
- `moddev-wait --from-start`: also finds a line already in the log (by default only new lines count, and `--help` now
  says so)
- `tools/test-templates.sh`: makes ToyMod in a temporary folder and runs `validate.py`, `build.sh`, `dotnet build` and
  the test-driver's `build.sh`, then plays `run.sh` against a fake KSP (PASS, FAIL, INCONCLUSIVE, timeout with and
  without a wrapper, the guards, `KEEP_LOG`, paths with spaces); skips what needs the KSP libraries, mcs or dotnet
- `templates/mod-repo/`: `Source/<Mod>.csproj` compiles with `dotnet build` (it failed with a duplicate
  `AssemblyVersion`); the README says to add a git remote before `release.py status`
- `templates/test-driver/` and `templates/mod-repo/` READMEs say that `build.sh` needs Mono's `mcs`, which the image does
  not have
- [docs/releasing.md](docs/releasing.md): a release commit that only changes Markdown starts no image build; start it
  with `gh workflow run docker-build.yml --ref main`
- `tools/release-notes.py` (in `templates/mod-repo/`) prints its usage instead of a traceback when called without a
  version; `.dockerignore` keeps the test-driver's `out/` out of the image
- `moddev-logsummary` has a full reference ([docs/moddev-logsummary.md](docs/moddev-logsummary.md), also in the image)
  and a complete `--help`; it now refuses a malformed `--since`, `--since` on a log without timestamps (both printed an
  empty, clean-looking summary), `--top 0` and an invalid `--mod` regex
- `moddev-logsummary --mod REGEX` narrows the exception tables to one subject (message or any stack frame) and lists
  the log lines about it (repeatable); `--known FILE` hides and counts the `[ERR]`/exception entries you already know
  about, so only what is new is left (`--show-known` turns that off); works with `--compare`
- [docs/releasing.md](docs/releasing.md): a version gets its number only when it is closed. `templates/mod-repo/` now
  brings `tools/release.py` (`status`, `prepare`, `publish`; what has and has not been tried is in that page), starts at
  `0.0.0`, its `validate.py` refuses a changelog section above the `.version` or marked "unreleased", and
  `package.sh` names test builds `-dev.zip`
- `moddev-wait`: waits for a scene change or a log line and answers with one line, and says so if
  KSP quit, instead of `pgrep`/`sleep`/`tail` loops
- `docs/headless-lessons.md`: what cost hours in hundreds of headless and GPU rounds
- `templates/test-driver/`: a test-only addon that plays a scenario written in a config file (open scenes, read values,
  `expect`), logs `ASSERT PASS/FAIL` and ends with `RESULT PASS` or `RESULT FAIL`; heartbeat file, watchdog, a runner that
  survives scene changes, `build.sh` (mcs against the game's own libraries) and `run.sh` (installs, runs in a window,
  removes everything, exit code 0/1/2)
- `templates/mod-repo/`: `new-mod.sh` makes a mod repo with an honest-status README, a changelog with a "Flown in a career
  save so far" line, `tools/validate.py`, a reproducible `tools/build.sh`, `package.sh` and CI
- `skills/`: `in-game-test` (an in-game test with a person at the keyboard) and `mod-design` (the design of one mod
  system, section by section, with a decision log), for Claude Code

## 1.1.0 "Headless Kerbal" (2026-10-10)

- `moddev-headless`: runs KSP to the main menu (or to any log line) on a virtual
  screen, from a hardlinked scratch copy of a cached mirror of the install, and
  summarizes the log (the image now includes rsync for the mirror)
- `moddev-logsummary`: exceptions grouped by type and by the mod that threw them,
  GameEvents handler exceptions, repeated `[ERR]` lines, assemblies that failed to load;
  `--compare A B` lists what changed between two logs
- `moddev-ab`: two `moddev-headless` runs that differ only in the files you name,
  and the comparison of their logs
- `moddev-csproj`: writes a `net48` SDK-style project from a mod's old Visual Studio
  project, with references found in your game, so it builds with `dotnet build`
- `moddev-checklist`: turns step-by-step instructions written in Markdown into a
  page to tick off (ok, odd, broken, skipped, plus a note) and copy back
- `AGENTS.md`, a Claude Code skill and `docs/ai-workflow.md`: how an AI assistant
  should use these tools, with one real fix as the example; `templates/probe/`
  for test-only addons. The image carries them in `/opt/ksp-moddev/`
- `docs/rebuild-old-bundles.md` tests the bundle with `moddev-headless`
- CI: GitHub Actions checks that the image still builds on every push and pull
  request (nothing is pushed to a registry)
- README: how this was made, credits and a Sponsor button

## 1.0.0 (2026-09-24)

First public release. Tested by rebuilding Surface Experiment Pack's 2016
asset bundles and loading them in KSP 1.12.5 on Linux.

- Unity 2019.4.18f1 (GameCI base) with Linux and Windows build targets
- Unity Hub + Firefox for the Personal license, and a guide in `docs/license.md`
- noVNC desktop on `127.0.0.1:6080`, with an optional `VNC_PASSWORD`
- `PUID`/`PGID` remapping at startup, so the image works for any file owner
- `fetch-parttools`: Squad's PartTools for 1.12 from web.archive.org, SHA-256 checked
- .NET SDK 8 for `net48` plugin DLLs
- `moddev-selftest`, `moddev-license`
- Guide: rebuilding an old mod's asset bundles (`docs/rebuild-old-bundles.md`)
