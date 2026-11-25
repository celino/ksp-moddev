#!/bin/bash
# Builds twice into scratch folders and compares the bytes: a release DLL anyone can rebuild and check with cmp.
set -euo pipefail
cd "$(dirname "$0")/.."
a=$(mktemp -d); b=$(mktemp -d); trap 'rm -rf "$a" "$b"' EXIT
OUT="$a" tools/build.sh "$@" >/dev/null
OUT="$b" tools/build.sh "$@" >/dev/null
cmp "$a/{{NAME}}.dll" "$b/{{NAME}}.dll" && echo "reproducible: $(sha256sum "$a/{{NAME}}.dll" | cut -c1-16)"
