#!/bin/bash
# Runs one TestDriver scenario on the desktop, in a window, and prints the RESULT line.
#   ./run.sh /path/to/KSP scenario.cfg            (build first: ./build.sh /path/to/KSP)
#   SAVE_FROM=/path/to/saves/mysave ./run.sh ...   copies that save to saves/<save = in the scenario> for the run
#   KSP_LAUNCH=tela-acordada ./run.sh ...          a command to put in front of ./KSP.x86_64 (e.g. keep the screen awake)
#   MAX_S=900 ./run.sh ...                         give up after this many seconds (default 900)
#   MENU_S=600 ./run.sh ...                        fail when the main menu is not reached in this many seconds (default off)
#   MAX_EXC=50 ./run.sh ...                        stop and fail when KSP.log has more than this many [EXC] lines (default off)
#   POLL_S=5 ./run.sh ...                          seconds between the checks above (default 5)
#   DRIVER_DLL=/path/TestDriver.dll ./run.sh ...  the driver to install (default out/TestDriver.dll next to this script);
#                                                  a copy of the template calls this run.sh with its own build
#   EXTRA=/path/folder ./run.sh ...                its contents go into GameData/zzTestDriver too (packs, crafts, data)
#   KEEP_LOG=1 ./run.sh ...                        keep KSP.log, Player.log, heartbeat.txt and result.txt of the round in
#                                                  OUT_DIR (default ./testdriver-<scenario file>-<date-time>)
# Adds GameData/zzTestDriver (and the copied save) to the install and removes them at the end, also on Ctrl+C.
# Exit code: 0 = PASS (RESULT PASS, at least one ASSERT PASS, no FAIL line of the driver)
#            1 = FAIL (RESULT FAIL, any "[TestDriver] ... FAIL" line, or more than MAX_EXC exceptions)
#            2 = no result (crash, hang, timeout, no main menu in MENU_S)
#            3 = INCONCLUSIVE (the round finished without failures but proved nothing: no expect passed)
# Watch it from outside with:  moddev-wait --log /path/to/KSP/KSP.log 'RESULT (PASS|FAIL)'
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
ksp="${1:-}"; scen="${2:-}"; dll="${DRIVER_DLL:-$here/out/TestDriver.dll}"
[ -x "$ksp/KSP.x86_64" ] && [ -f "$scen" ] && [ -f "$dll" ] || { echo "usage: $0 /path/to/KSP scenario.cfg   (after ./build.sh; driver: $dll)" >&2; exit 1; }
[ -z "${EXTRA:-}" ] || [ -d "$EXTRA" ] || { echo "EXTRA=$EXTRA is not a folder" >&2; exit 1; }
if pgrep -x KSP.x86_64 >/dev/null; then echo "KSP is running; wait for it" >&2; exit 1; fi
dest="$ksp/GameData/zzTestDriver"
[ -e "$dest" ] && { echo "$dest already exists; not touching it" >&2; exit 1; }
save=$(sed -n 's/^[[:space:]]*save[[:space:]]*=[[:space:]]*\([^[:space:]/]*\).*/\1/p' "$scen" | head -1)
copied=""
if [ -n "${SAVE_FROM:-}" ]; then
  [ -n "$save" ] || { echo "SAVE_FROM is set but the scenario has no save = line" >&2; exit 1; }
  [ -e "$ksp/saves/$save" ] && { echo "$ksp/saves/$save already exists; not touching it" >&2; exit 1; }
  cp -rp "$SAVE_FROM" "$ksp/saves/$save"; copied="$ksp/saves/$save"
  chmod -R u+w "$copied"   # the game has to write to it, also when the source save is read-only
fi
pid=""
# KSP_LAUNCH may be a wrapper: $pid is then the wrapper, not the game. The launch gets its own process
# group (setsid), and the whole group is stopped and waited for before anything is removed.
stop_game() {
  [ -n "$pid" ] && pgrep -g "$pid" >/dev/null || return 0
  kill -TERM -- "-$pid" 2>/dev/null
  for _ in $(seq "${STOP_S:-60}"); do pgrep -g "$pid" >/dev/null || return 0; sleep 1; done
  echo "== KSP did not stop: killing it"
  kill -KILL -- "-$pid" 2>/dev/null; sleep 1
}
player_log="${PLAYER_LOG:-$HOME/.config/unity3d/Squad/Kerbal Space Program/Player.log}"
keep_logs() {
  [ "${KEEP_LOG:-0}" = 1 ] || return 0
  local out="${OUT_DIR:-$PWD/testdriver-$(basename "$scen" .cfg)-$(date +%Y%m%d-%H%M%S)}"
  mkdir -p "$out" || return 0
  for f in "$ksp/KSP.log" "$player_log" "$dest/PluginData/heartbeat.txt" "$dest/PluginData/result.txt"; do
    [ -f "$f" ] && cp -p "$f" "$out/"
  done
  echo "== logs kept in $out"
}
cleanup() { stop_game; keep_logs; rm -rf "$dest" ${copied:+"$copied"}; echo "== removed the driver${copied:+ and the test save}"; }
trap cleanup EXIT
mkdir -p "$dest"; [ -n "${EXTRA:-}" ] && cp -rp "$EXTRA"/. "$dest/"
cp -p "$dll" "$dest/TestDriver.dll"; cp -p "$scen" "$dest/scenario.cfg"
rm -f "$ksp/KSP.log"
echo "== running $(basename "$scen"): KSP opens in a window and closes by itself; don't click it"
(cd "$ksp" && exec setsid ${KSP_LAUNCH:-} ./KSP.x86_64 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 >/dev/null 2>&1) &
pid=$!; start=$(date +%s); guard=""
while kill -0 "$pid" 2>/dev/null; do
  sleep "${POLL_S:-5}"
  el=$(( $(date +%s) - start ))
  if (( el > ${MAX_S:-900} )); then guard=timeout; echo "== timeout after ${el}s: stopping KSP (pid $pid)"; break; fi
  if (( ${MENU_S:-0} > 0 && el > MENU_S )) && ! grep -aqE 'Scene Change : From [A-Z]+ to MAINMENU' "$ksp/KSP.log" 2>/dev/null; then
    guard=menu; echo "== no main menu after ${el}s (MENU_S=$MENU_S): stopping KSP"; break
  fi
  if (( ${MAX_EXC:-0} > 0 )); then
    exc=$(grep -ac '^\[EXC ' "$ksp/KSP.log" 2>/dev/null); exc=${exc:-0}
    if (( exc > MAX_EXC )); then guard=exc; echo "== $exc exceptions in KSP.log (MAX_EXC=$MAX_EXC): stopping KSP"; break; fi
  fi
done
stop_game   # the wrapper may be gone while the game is still writing the log
line=$(grep -a '\[TestDriver\] RESULT' "$ksp/KSP.log" 2>/dev/null | tail -1)
echo "${line:-no RESULT line in KSP.log (crash or hang; see heartbeat.txt and Player.log)}"
fails=$(grep -aE '\[TestDriver\] (ASSERT FAIL|.*FAIL:)' "$ksp/KSP.log" 2>/dev/null | head -10)
[ -n "$fails" ] && echo "$fails"
passes=$(grep -ac '\[TestDriver\] ASSERT PASS' "$ksp/KSP.log" 2>/dev/null); passes=${passes:-0}
case "$guard" in exc) exit 1;; timeout|menu) exit 2;; esac
case "$line" in
  *"RESULT FAIL"*) exit 1;;
  *"RESULT PASS"*|*"RESULT INCONCLUSIVE"*)
    [ -n "$fails" ] && { echo "== FAIL: the driver logged a failure above, so this is not a PASS"; exit 1; }
    case "$line" in *"RESULT INCONCLUSIVE"*) echo "== INCONCLUSIVE: nothing was proved (no expect passed)"; exit 3;; esac
    (( passes > 0 )) || { echo "== INCONCLUSIVE: RESULT PASS without any ASSERT PASS line"; exit 3; }
    exit 0;;
  *) exit 2;;
esac
