#!/bin/bash
# Tests the templates end to end, the way a modder uses them, in a temporary folder (with a space in its path):
#   - templates/mod-repo: new-mod.sh makes ToyMod; validate.py refuses the TODOs, then passes; tools/build.sh (mcs) and
#     dotnet build of Source/ToyMod.csproj compile against the KSP libraries;
#   - templates/test-driver: build.sh compiles; run.sh plays rounds with a FAKE KSP (a script named KSP.x86_64 that writes
#     a KSP.log) and must give the right exit code for PASS, FAIL, INCONCLUSIVE, timeout and the MENU_S / MAX_EXC guards,
#     keep the logs with KEEP_LOG=1, leave no process behind and remove only its own files (paths with spaces).
#
#   tools/test-templates.sh [/path/to/KSP]     (or KSP_DIR=/path/to/KSP; inside the ksp-moddev image it is /ksp)
#   TEMPLATES=/other/templates tools/test-templates.sh ...   test another copy of the templates
#   RUN_SH=/my/copy/run.sh tools/test-templates.sh ...      play the fake rounds through a copy's run.sh, which must call
#                                                           "$TESTDRIVER_TEMPLATE/run.sh" (set to the template under test)
#
# Without the KSP libraries (or without mcs / dotnet) the steps that need them are skipped and listed; the fake rounds
# still run. Prints one line per check and ends with "templates: N/N ok" (exit 0) or "templates: N/M ok" (exit 1).
# It refuses to start while a real KSP is running: the fake one has the same process name.
set -uo pipefail
case "${1:-}" in -h|--help) sed -n '2,17p' "$0" | sed 's/^# \{0,1\}//'; exit 0;; esac
here="$(cd "$(dirname "$0")" && pwd)"
tpl="${TEMPLATES:-$here/../templates}"
ksp="${1:-${KSP_DIR:-}}"
[ -d "$tpl/mod-repo" ] && [ -d "$tpl/test-driver" ] || { echo "no templates in $tpl" >&2; exit 1; }
if pgrep -x KSP.x86_64 >/dev/null; then echo "a KSP is running; the fake KSP would be mistaken for it. Try again later." >&2; exit 1; fi
have_ksp=0; [ -n "$ksp" ] && [ -f "$ksp/KSP_Data/Managed/Assembly-CSharp.dll" ] && have_ksp=1

tmp="$(mktemp -d)"; work="$tmp/with space"; mkdir -p "$work" "$tmp/bin"
# every process of a fake round carries this variable: that is how leftovers are found (and only ours are killed)
# (set only on the run.sh command line, so this script's own processes do not carry it)
fake_pids() { grep -lsz "^FAKE_KSP_TAG=$tmp\$" /proc/[0-9]*/environ 2>/dev/null | cut -d/ -f3; }
kill_fakes() { local p; p=$(fake_pids); [ -n "$p" ] && kill -KILL $p 2>/dev/null; return 0; }
trap 'kill_fakes; chmod -R u+w "$tmp"; rm -rf "$tmp"' EXIT
ok=0; total=0; skipped=()
pass() { total=$((total + 1)); ok=$((ok + 1)); echo "ok    $1"; }
fail() { total=$((total + 1)); echo "FAIL  $1"; [ -s "$tmp/out" ] && tail -6 "$tmp/out" | sed 's/^/        /'; }
skip() { skipped+=("$1"); echo "skip  $1: $2"; }
# check NAME CMD...: the command must exit 0
check() { local n="$1"; shift; if "$@" >"$tmp/out" 2>&1; then pass "$n"; else fail "$n"; fi; }

# ---- mod-repo -------------------------------------------------------------------------------------------------------
toy="$work/ToyMod"
check "mod-repo: new-mod.sh ToyMod" "$tpl/mod-repo/new-mod.sh" ToyMod "$toy" --author "Test Author" --github-user tester
if [ -d "$toy" ]; then
  if (cd "$toy" && python3 tools/validate.py) >"$tmp/out" 2>&1; then fail "mod-repo: validate.py refuses the README TODOs"
  else pass "mod-repo: validate.py refuses the README TODOs"; fi
  sed -i 's/\bTODO\b/Done/g' "$toy/README.md"
  check "mod-repo: validate.py passes once filled in" bash -c 'cd "$1" && python3 tools/validate.py' _ "$toy"
  if [ $have_ksp = 0 ]; then skip "mod-repo: tools/build.sh" "no KSP libraries (give /path/to/KSP or KSP_DIR)"
  elif ! command -v mcs >/dev/null; then skip "mod-repo: tools/build.sh" "no mcs (Mono) here"
  else check "mod-repo: tools/build.sh (mcs)" bash -c 'cd "$1" && tools/build.sh "$2" && test -s GameData/ToyMod/Plugins/ToyMod.dll' _ "$toy" "$ksp"; fi
  if [ $have_ksp = 0 ]; then skip "mod-repo: dotnet build" "no KSP libraries"
  elif ! command -v dotnet >/dev/null; then skip "mod-repo: dotnet build" "no dotnet SDK here"
  else check "mod-repo: dotnet build Source/ToyMod.csproj" bash -c 'cd "$1/Source" && dotnet build -c Release -nologo -v q -p:KspDir="$2" -o "$1/dotnet-out" && test -s "$1/dotnet-out/ToyMod.dll"' _ "$toy" "$ksp"; fi
else skip "mod-repo: the rest" "new-mod.sh made nothing"; fi

# ---- test-driver: build -----------------------------------------------------------------------------------------------
td="$work/test-driver"; cp -rp "$tpl/test-driver" "$td"; rm -rf "$td/out"
[ -n "${RUN_SH:-}" ] && echo "rounds through the copy $RUN_SH"
if [ $have_ksp = 0 ]; then skip "test-driver: build.sh" "no KSP libraries"
elif ! command -v mcs >/dev/null; then skip "test-driver: build.sh" "no mcs (Mono) here"
else check "test-driver: build.sh (mcs)" bash -c '"$1/build.sh" "$2" && test -s "$1/out/TestDriver.dll"' _ "$td" "$ksp"; fi
mkdir -p "$td/out"; [ -s "$td/out/TestDriver.dll" ] || echo "fake dll" > "$td/out/TestDriver.dll"   # run.sh only copies it

# ---- test-driver: run.sh with a fake KSP ------------------------------------------------------------------------------
# Spaces on purpose: unquoted, "$tmp/with space/K S P/saves/x" splits into "$tmp/with", "space/K", "S" and "P/saves/x",
# the last three relative to the current folder. The rounds run from "$work", where sentinels wait at those names.
fk="$work/K S P"
mkdir -p "$fk/GameData" "$fk/saves" "$work/space/K" "$work/S"; touch "$tmp/with" "$work/space/K/sentinel" "$work/S/sentinel"
sentinels=("$tmp/with" "$work/space/K/sentinel" "$work/S/sentinel")
cat > "$fk/KSP.x86_64" <<'FAKE'
#!/bin/bash
# fake KSP: behaves as the "fake = <mode>" line of the scenario says, writing KSP.log like the real game
mode=$(sed -n 's/^[[:space:]]*fake[[:space:]]*=[[:space:]]*//p' GameData/zzTestDriver/scenario.cfg | head -1)
L() { echo "[LOG 00:00:00.000] $*" >> KSP.log; }
D() { L "[TestDriver] $*"; }
if [ "$mode" = nomenu ]; then trap '' TERM; L "loading"; sleep 300; exit 0; fi
L "[HighLogic]: =========================== Scene Change : From LOADING to MAINMENU ====================="
[ -n "${PLAYER_LOG:-}" ] && echo "fake Player.log" > "$PLAYER_LOG"
mkdir -p GameData/zzTestDriver/PluginData; echo "frames=1" > GameData/zzTestDriver/PluginData/heartbeat.txt
case "$mode" in
  pass)         D "ASSERT PASS: x eq 1 (got 1)"; D "RESULT PASS (1 of 1 checks passed, 1 expect passed)";;
  fail)         D "ASSERT FAIL: x eq 1 (got 0)"; D "RESULT FAIL (0 of 1 checks passed, 0 expect passed)";;
  inconclusive) D "ASSERT PASS: scene SPH loaded (scene EDITOR)"; D "RESULT INCONCLUSIVE (1 of 1 checks passed, 0 expect passed)";;
  noproof)      D "RESULT PASS (0 of 0 checks passed)";;                       # an older driver: PASS with nothing checked
  waitfail)     D "ASSERT PASS: x eq 1 (got 1)"; D "FAIL: no offer after 240 s"; D "RESULT PASS (1 of 1 checks passed)";;
  extra)        if [ -f GameData/zzTestDriver/extra-marker.txt ] && [ -f GameData/zzTestDriver/TestDriver.dll ]; then D "ASSERT PASS: EXTRA copied"; D "RESULT PASS (1 of 1)"
                else D "ASSERT FAIL: EXTRA copied"; D "RESULT FAIL (0 of 1)"; fi;;
  save)         if [ -w saves/testdriver-run/persistent.sfs ]; then D "ASSERT PASS: save writable"; D "RESULT PASS (1 of 1)"
                else D "ASSERT FAIL: save writable"; D "RESULT FAIL (0 of 1)"; fi;;
  hang)         trap '' TERM; L "the game hangs and ignores TERM"; sleep 300;;
  exc)          for i in $(seq 20); do echo "[EXC 00:00:00.000] NullReferenceException: fake" >> KSP.log; done; sleep 300;;
esac
echo "PASS" > GameData/zzTestDriver/PluginData/result.txt
FAKE
chmod +x "$fk/KSP.x86_64"
# a wrapper like tela-acordada: passes TERM on to the game and quits without waiting for it
cat > "$tmp/bin/fakewrap" <<'WRAP'
#!/bin/bash
"$@" & child=$!
trap 'kill -TERM $child; exit 0' TERM
wait $child
WRAP
chmod +x "$tmp/bin/fakewrap"
src_save="$work/source save"; mkdir -p "$src_save"; echo "GAME {}" > "$src_save/persistent.sfs"; chmod -R a-w "$src_save"

# round MODE WANT_RC [ENV=VALUE...]: one run.sh round, then the checks that hold for every round
round() {
  local mode="$1" want="$2"; shift 2
  local scen="$work/scen-$mode.cfg"
  printf 'TESTDRIVER\n{\n    name = fake\n    save = testdriver-run\n    fake = %s\n}\n' "$mode" > "$scen"
  cd "$work" && env FAKE_KSP_TAG="$tmp" TESTDRIVER_TEMPLATE="$td" POLL_S=1 MAX_S=20 STOP_S=3 PLAYER_LOG="$tmp/Player.log" "$@" "${RUN_SH:-$td/run.sh}" "$fk" "$scen" >"$tmp/out" 2>&1
  local rc=$? why="" f
  [ "$rc" = "$want" ] || why="exit $rc, wanted $want"
  case "$mode" in   # the guard must be what ended the round, not the MAX_S timeout
    nomenu) grep -q "no main menu" "$tmp/out" || why="$why; not stopped by MENU_S";;
    exc)    grep -q "exceptions in KSP.log" "$tmp/out" || why="$why; not stopped by MAX_EXC";;
  esac
  local left; left=$(fake_pids); [ -n "$left" ] && why="$why; processes left: $(ps -o comm= -p $left | tr '\n' ' ')"
  [ -e "$fk/GameData/zzTestDriver" ] && why="$why; zzTestDriver left in GameData"
  [ -e "$fk/saves/testdriver-run" ] && why="$why; test save left"
  for f in "${sentinels[@]}"; do [ -e "$f" ] || why="$why; rm -rf removed $f, not its own"; done
  if [ -z "$why" ]; then pass "run.sh $mode -> $want${*:+ ($*)}"; else echo "$why" >> "$tmp/out"; fail "run.sh $mode -> $want${*:+ ($*)}"; fi
  kill_fakes; return 0
}
round pass 0
round fail 1
round inconclusive 3
round noproof 3
round waitfail 1
round save 0 SAVE_FROM="$src_save"
round hang 2 MAX_S=3
round hang 2 MAX_S=3 KSP_LAUNCH="$tmp/bin/fakewrap"
round nomenu 2 MENU_S=2
round exc 1 MAX_EXC=10
xdir="$work/extra pack"; mkdir -p "$xdir"; echo x > "$xdir/extra-marker.txt"
round extra 0 EXTRA="$xdir"
keep="$work/kept logs"
round pass 0 KEEP_LOG=1 OUT_DIR="$keep"
if [ -f "$keep/KSP.log" ] && [ -f "$keep/Player.log" ] && [ -f "$keep/heartbeat.txt" ] && [ -f "$keep/result.txt" ]; then
  pass "run.sh KEEP_LOG=1 keeps KSP.log, Player.log, heartbeat.txt, result.txt"
else ls -la "$keep" > "$tmp/out" 2>&1; fail "run.sh KEEP_LOG=1 keeps KSP.log, Player.log, heartbeat.txt, result.txt"; fi

[ ${#skipped[@]} -gt 0 ] && echo "skipped ${#skipped[@]}: ${skipped[*]}"
echo "templates: $ok/$total ok"
[ "$ok" = "$total" ]
