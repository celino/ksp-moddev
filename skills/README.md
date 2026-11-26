# Skills for Claude Code

Two skills that go with these tools. To use them, copy a folder into `.claude/skills/` of your project (or `~/.claude/skills/`).
Claude Code picks a skill up from its description, or you call it with `/in-game-test` or `/mod-design`. They are plain Markdown, so
other assistants can be pointed at the file too. The paths, language and tools of your own project don't belong inside
them: put those in a short skill or instructions file of your project that sends the assistant to read these.
The ready-made fix skill for this image is in [`.claude/skills/ksp-mod-fix/`](../.claude/skills/ksp-mod-fix/).

| skill | for |
|---|---|
| `in-game-test` | an in-game test with a person at the keyboard: prepare, rounds with build variants, read, clean up |
| `mod-design` | the design document of one system of a mod, a section at a time, with a decision log that is never summarized |

These public versions have not been used on a real test or design in this form yet.
