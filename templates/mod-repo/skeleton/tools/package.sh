#!/bin/sh
# Builds dist/{{NAME}}-<version>.zip with GameData/ at the root (the layout SpaceDock and CKAN expect), plus README.md,
# CHANGELOG.md and LICENSE. The DLL is not in git: run tools/build.sh first.
set -eu
cd "$(dirname "$0")/.."
dll=GameData/{{NAME}}/Plugins/{{NAME}}.dll
[ ! -d Source ] || [ -f "$dll" ] || { echo "missing $dll: run tools/build.sh /path/to/KSP first" >&2; exit 1; }
version=$(python3 -c 'import json; v = json.load(open("GameData/{{NAME}}/{{NAME}}.version"))["VERSION"]; print("%d.%d.%d" % (v["MAJOR"], v["MINOR"], v["PATCH"]))')
out="dist/{{NAME}}-${version}.zip"
rm -rf dist
mkdir -p dist
python3 - "$out" <<'PY'
import os, sys, zipfile
with zipfile.ZipFile(sys.argv[1], "w", zipfile.ZIP_DEFLATED) as z:
    for root, _, files in os.walk("GameData"):
        for name in sorted(files):
            z.write(os.path.join(root, name))
    for name in ("README.md", "CHANGELOG.md", "LICENSE"):
        z.write(name)
PY
(cd dist && sha256sum "$(basename "$out")" > "$(basename "$out").sha256")
echo "$out"
