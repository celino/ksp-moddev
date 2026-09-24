# Changelog

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
