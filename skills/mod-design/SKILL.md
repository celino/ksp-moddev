---
name: mod-design
description: Writes the design document of ONE system of a KSP mod (for example an attribute system, or one contract), section by section, with the person approving each section and a way to resume if the session drops. Use when the person asks for a design, to "design system X", to plan a mod or contract, or before building a new system. Not for bugs, translations or tests.
---

# Design of one mod system

Adapted from two open projects, with credit: the section structure and the rhythm "one section at a time, written after
approval" come from the `design-system` skill of
[Claude-Code-Game-Studios](https://github.com/Donchitos/Claude-Code-Game-Studios) (MIT); the mechanic formula and the
pending-item markers come from [game-design-document](https://github.com/ityes22/game-design-document) (Apache-2.0).
A lean version: no agents, no hooks, no Word or PDF. Output is Markdown.

**Scope:** one system at a time ("character sheet", "contract A3"), not the whole mod. This is not a publisher's GDD: no
monetization, competition or launch schedule.

## Before you start (at most 5 reads)
1. The topic's folder and its project sheet, if any (any page that says what the mod is and what was decided). Decisions
   already taken are **not** reopened.
2. The project's notes, only the part about this topic.
3. Neighbouring design documents (`design-*.md` in the same folder), to cross dependencies.
4. If the system touches another mod, read that mod's real cfg or code. A premise you read = **V** (verified); one you
   deduced = **H** (hypothesis); one you do not know = **?**.
5. If `design-<system>.md` exists this is a resume: find the sections still marked `[TO DESIGN]` and go on from the next
   one. Do not rediscuss the finished ones.

## The file
`design-<system>.md` in the topic's folder, in the person's language (translate the headings and markers below, and keep
them the same from then on), headings and simple tables, relative links. Ask **once** "may I create the
skeleton?", create it with every section as `[TO DESIGN]` **plus the "Decision log" at the end**, and write each decision
**at the moment it is made** (not only when the section closes). Header: state (draft or approved), absolute date, link to
the project sheet, if there is one.

## Decision log (what must not be lost)
The file ends with `## Decision log`. It **only grows**: never summarize or delete (a revised decision is a new line that
cites the old one). One table, one line per decision, numbered `D1, D2...`:

| # | Section | Decision | Why (the person's words if any) | Alternatives dropped, and why | Source | Date |
| --- | --- | --- | --- | --- | --- | --- |

- **Decision**: complete, with the exact numbers and names (never "see above").
- **Why**: the reason the person gave; if it was your recommendation that they accepted, write "recommendation accepted" and the argument.
- **Dropped**: every option shown and not chosen, with the reason; also one they suggested and you advised against.
- **Source**: `person` / `project-sheet §x` / `read in code` / `test`. Text you deduced and they never confirmed is **not** a decision: it goes to `[OPEN QUESTION]`.
- Body sections show the clean result and cite `(D7)`; the full reason lives **only** in the log (one source per fact).
- An idea said in passing and not decided (a candidate, "maybe later") also gets a line, type `candidate`, so it does not vanish.

## Sections (in this order; skip one only with a one-line reason)
1. **Summary**: 2 sentences. What the system does and for whom (the player).
2. **What the player notices**: do they see it, hear it, or pay for it? Without that the system does not go in.
3. **Rules**: what happens, as a short list. Each mechanic answers: *input* → *logic* (conditions, range of randomness) →
   *output* (what shows up) → *parameters* (a table with numbers) → *why*.
4. **States and transitions**: only if the system has states (a contract offered / accepted / complete). Otherwise skip.
5. **Formulas**: concrete numbers and ranges, never "varies with the level". Small effects, switchable in the options.
6. **Edge cases**: dead kerbal, a save without the neighbouring mod, a mod removed mid-career, a value out of range, a scene
   where the code does not run (editor, tracking station), an old save without the new data.
7. **Interactions with other mods and systems**: table *mod/system · what we read or write · how (hook, cfg, API) · premise
   state (V/H/?)*. A conflict with a neighbouring design goes to section 12; it is not hidden.
8. **Data and save**: what is stored, where, in which format, how an old save migrates. Who owns the data.
9. **Tuning knobs**: what can change in a cfg or in the difficulty options, with the default.
10. **How to test**: one item per rule in section 3, as "do X, Y should appear". It becomes the script of an in-game test
    (`in-game-test`). Say what can be proven headless and what needs hands.
11. **Acceptance and stop criteria**: when the system is done; when we give up ("if nobody notices it, it does not go on").
12. **Open questions**: `[OPEN QUESTION: ...]` (the person's decision) and `[PLAYTEST: ...]` (a value to tune by playing),
    each with what changes depending on the answer.

## Rhythm of each section
1. Show what is already known (with the source) and **2 or 3 options** where there is a choice, with a recommendation. One
   question at a time.
2. The person decides. Write the draft of the section.
3. **Each decision goes into the log as soon as it is made**, before the next question. When the section is approved, write
   its body citing the `D#`, then the next section. **Never** generate the whole document and present it finished.
4. **Check when a section closes:** reread the conversation of this section and list, in one line each, every decision,
   number and reason that came up; confirm each is in the log. Something missing: write it now and say what was added.
5. An assumption that contradicts something already decided: stop and ask; do not work around it.

## Context and resuming
- **Dump before any `/clear`, compaction or end of session:** write down everything said and not yet in the file
  (decisions, reasons, dropped options, candidates, doubts, numbers) and only then suggest clearing. Say how many new log
  lines went in. No dump, no `/clear` suggestion.
- The person can ask for a dump at any time: check the whole conversation against the file.
- Before asking for the final approval: a general check (every logged decision appears in the body, no `[TO DESIGN]` left,
  no contradiction between `D#`s); show the counts of decisions, candidates and open questions.
- Mark "APPROVED on <date>" at the top only when **the person** says so. Build only after that.

## Never
- Implement, install or commit during the design. A commit needs the person's explicit OK.
- Summarize, condense or delete a log line; write "as discussed" without saying what and why.
- Invent a number without a `[PLAYTEST]` or a source. Treat a hypothesis as a fact.
- Write public text (a post, a PR) here: that goes in its own draft.
