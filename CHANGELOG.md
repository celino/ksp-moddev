# Changelog

## Unreleased: 1.1.0 "Headless Kerbal"

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
