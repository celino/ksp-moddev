#!/bin/bash
# Runs one TestDriver scenario on the desktop, in a window, and prints the RESULT line.
#   ./run.sh /path/to/KSP scenario.cfg            (build first: ./build.sh /path/to/KSP)
#   SAVE_FROM=/path/to/saves/mysave ./run.sh ...   copies that save to saves/<save = in the scenario> for the run
#   KSP_LAUNCH=tela-acordada ./run.sh ...          a command to put in front of ./KSP.x86_64 (e.g. keep the screen awake)
#   MAX_S=900 ./run.sh ...                         give up after this many seconds (default 900)
# Adds GameData/zzTestDriver (and the copied save) to the install and removes them at the end, also on Ctrl+C.
# Exit code: 0 = RESULT PASS, 1 = RESULT FAIL, 2 = no result (crash, hang, timeout).
# Watch it from outside with:  moddev-wait --log /path/to/KSP/KSP.log 'RESULT (PASS|FAIL)'
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
ksp="${1:-}"; scen="${2:-}"
[ -x "$ksp/KSP.x86_64" ] && [ -f "$scen" ] && [ -f "$here/out/TestDriver.dll" ] || { echo "usage: $0 /path/to/KSP scenario.cfg   (after ./build.sh)" >&2; exit 1; }
if pgrep -x KSP.x86_64 >/dev/null; then echo "KSP is running; wait for it" >&2; exit 1; fi
dest="$ksp/GameData/zzTestDriver"
[ -e "$dest" ] && { echo "$dest already exists; not touching it" >&2; exit 1; }
save=$(sed -n 's/^[[:space:]]*save[[:space:]]*=[[:space:]]*\([^[:space:]/]*\).*/\1/p' "$scen" | head -1)
copied=""
if [ -n "${SAVE_FROM:-}" ]; then
  [ -n "$save" ] || { echo "SAVE_FROM is set but the scenario has no save = line" >&2; exit 1; }
  [ -e "$ksp/saves/$save" ] && { echo "$ksp/saves/$save already exists; not touching it" >&2; exit 1; }
  cp -rp "$SAVE_FROM" "$ksp/saves/$save"; copied="$ksp/saves/$save"
fi
cleanup() { rm -rf "$dest" $copied; echo "== removed the driver${copied:+ and the test save}"; }
trap cleanup EXIT
mkdir -p "$dest"; cp -p "$here/out/TestDriver.dll" "$dest/"; cp -p "$scen" "$dest/scenario.cfg"
rm -f "$ksp/KSP.log"
echo "== running $(basename "$scen"): KSP opens in a window and closes by itself; don't click it"
(cd "$ksp" && exec ${KSP_LAUNCH:-} ./KSP.x86_64 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 >/dev/null 2>&1) &
pid=$!; start=$(date +%s)
while kill -0 "$pid" 2>/dev/null; do
  sleep 5
  if (( $(date +%s) - start > ${MAX_S:-900} )); then echo "== timeout: killing KSP (pid $pid)"; kill "$pid"; sleep 5; kill -9 "$pid" 2>/dev/null; break; fi
done
line=$(grep -a '\[TestDriver\] RESULT' "$ksp/KSP.log" 2>/dev/null | tail -1)
echo "${line:-no RESULT line in KSP.log (crash or hang; see heartbeat.txt and Player.log)}"
grep -a '\[TestDriver\] ASSERT FAIL' "$ksp/KSP.log" 2>/dev/null | head -10
case "$line" in *"RESULT PASS"*) exit 0;; *"RESULT FAIL"*) exit 1;; *) exit 2;; esac
