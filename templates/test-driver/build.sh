#!/bin/bash
# Builds out/TestDriver.dll against the KSP install's own libraries (mcs, mono).
#   ./build.sh /path/to/KSP        (or KSP_DIR=/path/to/KSP ./build.sh)
# -nostdlib with KSP's mscorlib is on purpose: with the system one, calls such as Split(',') bind to
# overloads KSP's runtime does not have and fail at run time, inside a coroutine, with no message.
# The DLL name is the assembly name: build to out/TestDriver.dll, do not rename the output.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
ksp="${1:-${KSP_DIR:-}}"
[ -d "$ksp/KSP_Data/Managed" ] || { echo "usage: $0 /path/to/KSP" >&2; exit 1; }
m="$ksp/KSP_Data/Managed"
mkdir -p "$here/out"
refs=(mscorlib System System.Core Assembly-CSharp UnityEngine UnityEngine.CoreModule UnityEngine.IMGUIModule
      UnityEngine.ScreenCaptureModule UnityEngine.ImageConversionModule UnityEngine.PhysicsModule)
args=(); for r in "${refs[@]}"; do args+=("-r:$m/$r.dll"); done
mcs -nostdlib -target:library -debug- -out:"$here/out/TestDriver.dll" "${args[@]}" "$here/TestDriver.cs"
echo "built $here/out/TestDriver.dll"
