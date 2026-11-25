#!/bin/bash
# Builds GameData/{{NAME}}/Plugins/{{NAME}}.dll from Source/*.cs against the KSP install's own libraries (mcs, mono).
#   tools/build.sh /path/to/KSP      (or KSP_DIR=/path/to/KSP)
# -nostdlib with KSP's own mscorlib is on purpose: the system one binds calls such as Split(',') to overloads that
# KSP's runtime does not have, and the failure shows up only in the game. The output file name is the assembly name;
# the same sources and the same file name give the same bytes (tools/check-reproducible.sh proves it).
set -euo pipefail
cd "$(dirname "$0")/.."
ksp="${1:-${KSP_DIR:-}}"
[ -d "$ksp/KSP_Data/Managed" ] || { echo "usage: $0 /path/to/KSP" >&2; exit 1; }
m="$ksp/KSP_Data/Managed"
out="${OUT:-GameData/{{NAME}}/Plugins}"
mkdir -p "$out"
args=(); for r in mscorlib System System.Core Assembly-CSharp UnityEngine UnityEngine.CoreModule; do args+=("-r:$m/$r.dll"); done
mcs -nostdlib -target:library -debug- -deterministic -out:"$out/{{NAME}}.dll" "${args[@]}" Source/*.cs
echo "built $out/{{NAME}}.dll"
