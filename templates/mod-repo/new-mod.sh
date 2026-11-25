#!/bin/bash
# Makes a new mod repo from skeleton/.
#   new-mod.sh MyMod ~/dev/MyMod --author "Your Name" --github-user yourlogin
# MyMod: a C# identifier (letters and digits, starts with a capital); it names the folder in GameData, the DLL and the
# namespace. The target folder must not exist. Nothing is committed or published.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
name="${1:-}"; dest="${2:-}"; shift 2 || true
author="Your Name"; user="your-github-login"
while [ $# -gt 0 ]; do case "$1" in --author) author="$2"; shift 2;; --github-user) user="$2"; shift 2;; *) echo "unknown option $1" >&2; exit 1;; esac; done
[[ "$name" =~ ^[A-Z][A-Za-z0-9]*$ ]] && [ -n "$dest" ] || { echo "usage: $0 MyMod target-folder [--author NAME] [--github-user LOGIN]" >&2; exit 1; }
[ -e "$dest" ] && { echo "$dest already exists; not touching it" >&2; exit 1; }
cp -rp "$here/skeleton" "$dest"
# file and folder names first (deepest first), then the text inside
find "$dest" -depth -name '*__NAME__*' | while read -r p; do mv "$p" "$(dirname "$p")/$(basename "$p" | sed "s/__NAME__/$name/g")"; done
find "$dest" -type f | while read -r f; do
  python3 - "$f" "$name" "$author" "$user" "$(date +%Y)" <<'PY'
import sys
p, name, author, user, year = sys.argv[1:]
s = open(p, encoding="utf-8").read()
for a, b in (("{{NAME}}", name), ("{{AUTHOR}}", author), ("{{GITHUB_USER}}", user), ("{{YEAR}}", year)):
    s = s.replace(a, b)
open(p, "w", encoding="utf-8").write(s)
PY
done
echo "created $dest. Next: git init, then read README.md (the TODO lines are yours) and run tools/validate.py"
