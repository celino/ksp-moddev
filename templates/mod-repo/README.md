# mod-repo

A skeleton for a new KSP 1.12 mod repo that follows what has worked: an honest status in the README, a changelog that says
what was actually played, a validator that fails early, a zip with `GameData/` at the root, CI that runs the checks, and a
build that gives the same bytes every time.

```sh
./new-mod.sh MyMod ~/dev/MyMod --author "Your Name" --github-user yourlogin
cd ~/dev/MyMod && git init -b master   # the release branch named in tools/mod.json
python3 tools/validate.py        # fails on purpose until you replace the TODO lines in README.md
tools/build.sh /path/to/KSP      # GameData/MyMod/Plugins/MyMod.dll
tools/check-reproducible.sh /path/to/KSP
tools/package.sh                 # dist/MyMod-dev.zip while the changelog has "## Unreleased"
```

**Versions.** The skeleton starts at `0.0.0` with an "Unreleased" changelog section, and `validate.py` refuses a changelog
section with a number above the one in the `.version` file. When you decide to close a version, `tools/release.py`
writes the number and publishes it: follow [docs/releasing.md](../../docs/releasing.md).

What you get:

| file | what it is for |
|---|---|
| `README.md` | status first, then what it does, requirements, install, known issues, localization, building, credits. `TODO` marks what is yours to write |
| `CHANGELOG.md` | "Unreleased" plus one section per released version, with a **"Flown in a career save so far"** line that says what was played and what only passed the checks |
| `tools/release.py`, `tools/mod.json` | the release: `status`, `prepare` (writes the number) and `publish`; `mod.json` holds what differs from mod to mod |
| `LICENSE` | MIT text with your name; a placeholder note says to choose before you publish |
| `GameData/<Mod>/<Mod>.version` | for the version checker and CKAN; `URL` and `DOWNLOAD` stay empty until the repo is public |
| `GameData/<Mod>/Localization/` | `en-us` and `pt-br` with one key, as an example |
| `Source/` | a one-line addon and a `.csproj` for an IDE |
| `tools/validate.py` | version file, changelog section and "Flown" line, no placeholders left in the README, same language keys and `<<n>>` arguments in every language, keys used in code are defined, `AssemblyVersion` equals the version, relative links, and with `--tag vX.Y.Z` the tag equals the version |
| `tools/build.sh`, `check-reproducible.sh` | build with `mcs -nostdlib` against KSP's own libraries; same sources and same file name give the same bytes |
| `tools/package.sh`, `release-notes.py` | the zip (with a sha256) and the release notes taken from the changelog |
| `.github/`, `.gitea/` | CI that validates on every push and tag, and a bug report form |

Choices you may want to change: the CI does not build the DLL (it needs KSP's assemblies), so the zip comes from a local build
and is attached to the release by hand; CKAN's `.netkan`, SpaceDock and the funding file are left out on purpose, since they
need decisions that are yours.
