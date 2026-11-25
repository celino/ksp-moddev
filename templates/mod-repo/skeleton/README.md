<h1 align="center">{{NAME}}</h1>

<p align="center"><i>One line that says what it is, in your project's voice.</i></p>

<p align="center">
  <img alt="KSP 1.12" src="https://img.shields.io/badge/KSP-1.12-1e6fb8">
  <img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-555">
  <img alt="Made with AI assistance, reviewed and tested by a human" src="https://img.shields.io/badge/made%20with-AI%20assistance-8a8a8a">
  <img alt="Status: test build, not played yet" src="https://img.shields.io/badge/status-test%20build-c0392b">
</p>

> **Status: test build 0.1.0.** It loads and logs one line; nothing else is built, and nothing has been played in a
> career save. Do not use it on a career you care about.

TODO: two or three sentences: what it is, for whom, which problem it solves. Link every dependency by name.

## What it does

TODO: the idea in plain language, with the numbers a player wants (taken from the cfgs, never from memory).

## Requirements

- KSP 1.12.x (tested on 1.12.5)
- TODO: each dependency with its minimum version and a link; say which are optional.

## Install

Download the zip from the [releases](https://github.com/{{GITHUB_USER}}/{{NAME}}/releases) and copy its `GameData`
folder into your KSP folder.

## Known issues

- Nothing has been played yet.

## Localization

All player-facing texts are in `GameData/{{NAME}}/Localization/` (`en-us`, `pt-br`). To add a language, copy `en-us.cfg`,
rename the language node and translate the values; `tools/validate.py` checks that every key exists in every language.

## Building from source

The DLL is not in git (it needs KSP's own assemblies). With KSP installed:

```sh
tools/build.sh /path/to/KSP        # Source/ -> GameData/{{NAME}}/Plugins/{{NAME}}.dll (mcs, mono)
tools/check-reproducible.sh /path/to/KSP   # builds twice and compares the bytes
tools/validate.py                  # the checks the CI runs
tools/package.sh                   # dist/{{NAME}}-<version>.zip
```

## Testing

The CI checks the version file, the changelog, the language keys and the README on every commit. What the game does
with it is tested on the maintainer's own machine; the [CHANGELOG](CHANGELOG.md) says what was actually played.

## How this was made

TODO: say who plans, who writes, who tests, and that an AI assistant helped, in your own words. The person who
publishes is the one responsible for every line, mistakes included.

## Credits

- TODO: the authors of what this builds on, with names checked in their repos or forum threads, never guessed.

## License

[MIT](LICENSE).
