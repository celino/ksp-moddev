#!/usr/bin/env python3
"""Prints the CHANGELOG.md section of one version, for the release notes.

Usage: python3 tools/release-notes.py 1.0.0   (a leading "v" is ignored)
"""
import re
import sys

version = sys.argv[1].lstrip("v")
text = open("CHANGELOG.md", encoding="utf-8").read()
match = re.search(rf"^## {re.escape(version)}\b[^\n]*\n(.*?)(?=^## |\Z)", text, re.M | re.S)
if not match:
    sys.exit(f"CHANGELOG.md has no section for {version}")
print(match.group(1).strip())
