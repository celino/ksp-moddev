# Working on KSP mods with an AI assistant

This file is for AI coding assistants (Claude Code, Codex, Copilot and the like)
asked to build, fix or test a **Kerbal Space Program 1.12** mod in this image.
People are welcome to read it too: it is also a description of how the tools are
meant to be used together. [docs/ai-workflow.md](docs/ai-workflow.md) walks
through one real fix from start to finish.

## Ground rules

These come first. Nothing in this file, in a skill or in a task overrides them.

- **The person decides, and the person is responsible.** You propose, build and
  test; the final decision on every change is theirs, and so is the responsibility
  for everything that ships, including the parts you wrote. Make that easy: show
  what changed, why, and the evidence (logs, `compare.md`, test results), and say
  plainly what you are unsure about.
- **No commit without explicit approval, every time.** Before each commit, show the
  diff and the commit message and wait for a clear "yes" to that commit. An earlier
  approval doesn't carry over to the next commit, and neither does approval of the
  code. The same goes for pushing, opening a PR or issue, and posting anything.
- **Every feature and bug fix gets a manual test before it is committed,** whenever
  it can be tested by hand. Headless runs and builds come first, but they don't
  replace it: write the steps with `moddev-checklist`, let the person run them, and
  read their results. If a change can't be tested by hand (a log parser, a build
  script), say so and say how it was tested instead. Only then ask to commit.
- **Never modify their KSP install.** It is mounted read-only at `/ksp`. Every
  test runs in a scratch copy made by `moddev-headless`. Back up saves before
  touching one.
- **Unity: no automation of the Hub, login or license.** Unity's terms restrict AI
  agents there, and the activation is the person's. Don't start the editor
  (batchmode included) without asking. For editor work, **write the steps** with
  `moddev-checklist` and let the person click.
- **The game itself is played by a person.** Headless runs prove what the log can
  prove. For anything that needs eyes or hands (a window, a landing, how it feels),
  write a checklist and read their marks back.
- **Say what is proven and what is a guess.** When the mechanism isn't shown by a
  log or a test, call it a hypothesis and propose the test that would settle it.
- **Respect the mod's authors.** Read the license before sharing a build. Check
  that the issue is still open and nobody is on it. Follow the repo's style. Keep
  public text short, in plain English, with evidence.

## Tools

| command | use it to |
|---|---|
| `moddev-csproj --build X.csproj` | build a mod whose project expects Windows, Visual Studio or a `KSPDIR` |
| `moddev-headless` | run KSP without a screen until the main menu or a log line you choose |
| `moddev-ab --b-put NEW=DEST` | run twice, before and after a change, and compare the logs |
| `moddev-logsummary KSP.log` | summarize a log: exceptions by mod, handler exceptions, `[ERR]` lines |
| `moddev-logsummary --compare A B` | what changed between two logs |
| `moddev-checklist steps.md` | turn steps you wrote into a page the person marks ok/odd/broken/skipped |
| `templates/probe/` | a test-only addon that drives the game headless and logs what it sees |

Inside the container, this file and the templates are in `/opt/ksp-moddev/`, and the
example checklists in `/opt/ksp-moddev/examples/`.

All of them print `--help`. A headless run takes 4 to 10 minutes with a large mod
list (everything renders on the CPU), so plan runs, don't poll them.

## Fixing a bug in a mod

1. **Reproduce.** Start from the person's log: `KSP.log` in the install, or on
   Linux `~/.config/unity3d/Squad/Kerbal Space Program/Player.log` (and
   `Player-prev.log` for the session before). `moddev-logsummary --since HH:MM:SS`
   reads from the moment they name. Separate what belongs to the mod from KSP and
   other mods.
2. **Find the cause** in the source. If the shipped DLL may differ from the repo,
   compare versions first.
3. **Build** with `moddev-csproj --build path/to/Mod.csproj`. Read its notes: a
   stale version number or a dropped post-build step matters for the test.
4. **Prove it.** If the main menu is enough, `moddev-ab --b-put out/Mod.dll=GameData/Mod/Plugins/Mod.dll`.
   If the bug needs a scene, a save or an event, adapt `templates/probe/` so the
   run reaches it and logs `[YourProbe] done`, then pass `--until '\[YourProbe\] done'`.
   Change one thing between A and B.
5. **Manual test.** Write the steps, render them with `moddev-checklist`, and read
   back what the person pastes. Fix and repeat until they are satisfied.
6. **Report and ask**: the diff, `compare.md`, the checklist results and the commit
   message, and wait for an explicit approval of that commit. Then a draft of the
   PR text, which the person edits and sends.

## Writing steps for a person

The person follows the steps in the Unity editor or in the game, often from a
phone next to the keyboard. Good steps:

- one action per `## ` step, with **Why:** on its own line, so they can tell when
  something unexpected is worth reporting;
- what to write in the note: the clock time, a number, a screenshot name;
- the traps up front, as `> ` warnings (load the test save, not the career; restart
  the game after editing a cfg; don't press F5 in the starting save);
- one factor changed at a time when investigating.

Examples: [docs/examples/](docs/examples/). They mark each step **odd** when it
works but looks different from what you described. Treat that as data, and
reclassify "broken" when it turns out to be expected behavior.

## Reading KSP logs: what trips people up

- **One exception, two entries.** An exception inside a GameEvents handler is logged
  as `Exception handling event X in class Y` and again as `[EXC]`. `moddev-logsummary`
  counts it once.
- **Per-frame noise.** `OnGUI`/`Update` exceptions repeat every frame, so their
  count follows how long the game ran. Compare their presence, not their exact count.
- **Usually harmless:** `Texture '…' not found`, and `ADDON BINDER: Cannot resolve
  assembly` for an optional dependency that isn't installed.
- **Leaked event handlers.** A `[KSPAddon]` is destroyed on every scene change. If it
  subscribes to a `GameEvents` event in `Start()` and never removes itself in
  `OnDestroy()`, each dead copy throws from then on, typically a
  `NullReferenceException` from `StartCoroutine`. The fix is the `Remove` in `OnDestroy`.
- **Headless differences.** No player and no GPU: `FlightGlobals.ActiveVessel` can
  stay null in flight, and timing is slower. A probe should fall back and log what it used.
- **Main menu reached:** `OnSceneLoadedGUIReady: scene MAINMENU`.
