#!/usr/bin/env python3
"""Fails early. Checks that:
  - the .version file is valid and complete;
  - CHANGELOG.md has a "Flown in a career save so far" line and a section for that version, which is the last released
    one (0.0.0 before the first release: then the section is "## Unreleased");
  - no CHANGELOG.md section announces a number above it: a version gets its number when it is released;
  - README.md has no leftover {{placeholders}} or TODO markers;
  - every language file under Localization/ has exactly the keys (and <<n>> arguments) of en-us;
  - every #LOC_ key used in Source/ is defined in en-us;
  - the AssemblyVersion in Source/ agrees with the .version file;
  - relative links in the Markdown files resolve;
  - with --tag vX.Y.Z, the tag is the .version's version.

Usage: python3 tools/validate.py [--tag vX.Y.Z]
"""
import glob
import json
import os
import re
import sys

problems = []
root = "GameData"
ver_files = glob.glob(f"{root}/*/*.version")
if len(ver_files) != 1:
    sys.exit(f"expected one .version file under {root}/*/, found {len(ver_files)}")
ver_path = ver_files[0]
mod_dir = os.path.dirname(ver_path)
try:
    data = json.load(open(ver_path, encoding="utf-8"))
except Exception as e:
    sys.exit(f"{ver_path}: {e}")
for key in ("NAME", "VERSION", "KSP_VERSION", "KSP_VERSION_MIN", "KSP_VERSION_MAX"):
    if key not in data:
        problems.append(f"{ver_path}: missing {key}")
v = data.get("VERSION", {})
version = "%d.%d.%d" % (v.get("MAJOR", 0), v.get("MINOR", 0), v.get("PATCH", 0))

changelog = open("CHANGELOG.md", encoding="utf-8").read()
headings = re.findall(r"^## +(.+?)\s*$", changelog, re.M)
unreleased = any(h.startswith("Unreleased") for h in headings)   # the spelling tools/release.py looks for
numbered = {tuple(map(int, m.groups())): h for h in headings for m in [re.match(r"v?(\d+)\.(\d+)\.(\d+)\b", h)] if m}
current = tuple(map(int, version.split(".")))
if version == "0.0.0":
    if not unreleased:
        problems.append('CHANGELOG.md has no "## Unreleased" section')
elif current not in numbered:
    problems.append(f"CHANGELOG.md has no section for {version}")
for number, heading in numbered.items():
    if number > current or "unreleased" in heading.lower():
        problems.append(f'CHANGELOG.md: "## {heading}" names a version that is not released; use "## Unreleased"')
if "Flown in a career save so far" not in changelog:
    problems.append("CHANGELOG.md has no 'Flown in a career save so far' line")

readme = open("README.md", encoding="utf-8").read()
for pat in (r"\{\{", r"\bTODO\b"):
    if re.search(pat, readme):
        problems.append(f"README.md still has {pat}")

# localization: same keys and same <<n>> arguments in every language
def load(path):
    keys = {}
    for line in open(path, encoding="utf-8"):
        m = re.match(r"\s*(#[A-Za-z0-9_]+)\s*=\s*(.*)$", line)
        if m:
            keys[m.group(1)] = sorted(re.findall(r"<<\d+>>", m.group(2)))
    return keys

loc = sorted(glob.glob(f"{mod_dir}/Localization/*.cfg"))
base_path = f"{mod_dir}/Localization/en-us.cfg"
base = {}
if loc and not os.path.exists(base_path):
    problems.append(f"{base_path}: missing (en-us is the reference language)")
elif loc:
    base = load(base_path)
    for path in loc:
        if path == base_path:
            continue
        other = load(path)
        for k in sorted(set(base) - set(other)):
            problems.append(f"{path}: missing key {k}")
        for k in sorted(set(other) - set(base)):
            problems.append(f"{path}: key {k} is not in en-us")
        for k in sorted(set(base) & set(other)):
            if base[k] != other[k]:
                problems.append(f"{path}: {k} has arguments {other[k]} but en-us has {base[k]}")
src = "".join(open(f, encoding="utf-8").read() for f in glob.glob("Source/**/*.cs", recursive=True))
for k in sorted(set(re.findall(r'"(#LOC_[A-Za-z0-9_]+)"', src))):
    if k not in base:
        problems.append(f"Source uses {k}, which en-us does not define")

# the code agrees with the version
m = re.search(r'AssemblyVersion\("(\d+)\.(\d+)\.(\d+)', src)
if src and not m:
    problems.append("Source/ has no AssemblyVersion")
elif m and ".".join(m.groups()) != version:
    problems.append(f"AssemblyVersion {'.'.join(m.groups())} differs from .version {version}")

# relative links in the Markdown files
for path in glob.glob("*.md") + glob.glob("docs/**/*.md", recursive=True):
    for target in re.findall(r"\]\(([^)#\s]+)(?:#[^)]*)?\)", open(path, encoding="utf-8").read()):
        if re.match(r"[a-z]+:", target):
            continue
        if not os.path.exists(os.path.normpath(os.path.join(os.path.dirname(path), target))):
            problems.append(f"{path}: broken link {target}")

if "--tag" in sys.argv:
    tag = sys.argv[sys.argv.index("--tag") + 1].lstrip("v")
    if tag != version:
        problems.append(f"tag {tag} differs from .version {version}")
    if version == "0.0.0":
        problems.append("0.0.0 means never released: choose the number before tagging")
    if unreleased:
        problems.append('CHANGELOG.md still has "## Unreleased": the release section must hold everything in the tag')

if problems:
    sys.exit("\n".join(problems))
print(f"ok: {data.get('NAME')} {version}")
