# Releasing a version

For mods made with [`templates/mod-repo/`](../templates/mod-repo/), which bring `tools/release.py`. The same rule applies
to ksp-moddev itself, [by hand](#ksp-moddev-itself).

These steps can be followed by you or by an AI assistant working for you. "The person" is whoever decides what gets
published.

## The rule

**A version gets its number at the very end, when you decide to close it.** Until then, nothing public names it: not the
CHANGELOG, the README, the `.version` file, `AssemblyVersion`, docs, code comments, commit messages, branch names or
issues.

A number written early is a promise: readers look for a release that doesn't exist, and the plan may change (what looked
like a small fix turns out to break saves).

## While you work

- New changes go under `## Unreleased` in `CHANGELOG.md`, with no number and no date. Keep that spelling: the tools look
  for it.
- The version files keep the **last released** version. A mod that was never released keeps `0.0.0`.
- `tools/package.sh` names the zip `<Mod>-dev.zip` while that section exists: it is a test build.
- Commit messages say what changed, not which version it is for.
- A text that needs to point at unreleased work says "not released yet".

## Set up once

`tools/mod.json` says what differs from mod to mod. The template starts with a GitHub release from the `master` branch
and one extra place that holds the version, `AssemblyVersion`.

| key | meaning |
|---|---|
| `name` | the mod's name: zip name, tag message and release title |
| `version_file` | path of the `.version` file; without it, the only one under `GameData/` is used |
| `branch` | the release branch (`master` if absent) |
| `remote` | the preferred git remote (`github` if absent); if there is no remote with that name, the first one is used |
| `host` | `github` (uses the `gh` command, logged in) or `gitea` (needs the `gitea` block and `GITEA_TOKEN`) |
| `gitea` | `{"api": "https://host/api/v1", "repo": "owner/name"}` |
| `ci` | `false` to tag without waiting for CI |
| `replace` | other places that hold the version: `{"file", "regex"}`, where the regex has two groups around the number. Only the first match in each file is rewritten; no match is an error |

## Closing a version

Start only when the person who publishes says this version is being closed. Each step needs the one before it. All
commands run from the mod's repo.

1. **Where you are.** `python3 tools/release.py status` shows the `.version`, the tags on the remote and the changelog.
   It fails if the repo announces a version that has no tag: solve that first.
2. **Scope.** List what is under `## Unreleased` and compare it with `git log <last-tag>..` (the whole log before the
   first release) so nothing is missing or claimed twice. Anything unfinished is finished or taken out now. Update the
   README's status.
3. **Tests, still without a number.** Build, `tools/validate.py`, package and play it. Write what was really played (or
   that nothing was) in the "Flown in a career save so far" line. Commit and push all of this.
4. **Choose the size of the step**, from what changed since the last release: something that breaks saves, configs or
   command lines people rely on is `major`; new features are `minor`; only fixes are `patch`. Before the first major
   version, a breaking change is `minor`. The person decides.
5. **Prepare.** The tree must be clean, on the release branch and equal to the remote.

   ```sh
   python3 tools/release.py prepare patch    # or minor, major, or the number written out
   ```

   It turns `## Unreleased` into the number with today's date, writes it in the `.version` file and in every `replace`
   entry, and runs `tools/validate.py`. It commits nothing. It refuses an empty "Unreleased" section, a number that is not
   higher than the current one and a number that already has a tag. If anything fails, `validate.py` included, it undoes
   its edits.

   If the README or the `DOWNLOAD` field of the `.version` file name the version, update them now (or add them to
   `replace`).
6. **Check the release itself.** Build and package again from this tree; the zip now has the number in its name.
   `tools/validate.py --tag vX.Y.Z` passes, and that zip is installed and loaded once, with a clean log. From here until
   the tag exists, `release.py status` reports a problem: that is expected.
7. **Approval.** Show the diff, the commit message, the tag, the release title and notes
   (`tools/release-notes.py X.Y.Z`) and the files to attach. Nothing is committed or published before a clear yes.
8. **Commit** with exactly `Release X.Y.Z` as the first line.
9. **Publish.** Without `--yes` it only prints the plan:

   ```sh
   python3 tools/release.py publish          # the plan
   python3 tools/release.py publish --yes    # does it
   ```

   It pushes the commit, waits for CI, creates and pushes the tag `vX.Y.Z`, and creates the release with the zip, its
   `.sha256` and the changelog section as notes. It does not build or package: it uses the zip already in `dist/`.

   If the tag is already on the remote, on this same commit, `publish` finishes what is missing: it creates the release,
   or attaches only the zip or `.sha256` that an existing release lacks.

   On GitHub it watches one workflow run of that commit: if the repo has more than one workflow, check the others. On
   Gitea it waits only if the repo has `.gitea/workflows/`; that wait also needs `GITEA_TOKEN`, and it stops with an error
   after 90 seconds without any status (is the runner up?) or after 20 minutes still running.

   The download links of a Gitea release use the address that server is configured with: check that the people who need
   the files can reach it.
10. **Verify.** Download the published files and compare their checksums with the tested ones; open the release page and
    the links it has. Then whatever else carries the version (CKAN, SpaceDock, announcements), each with its own
    approval: the tool does none of that.

Don't open the next version's section in advance. `## Unreleased` comes back with the first change that goes into it.

## When something fails

| when | what to do |
|---|---|
| in `prepare` | it undoes its edits. Fix the cause, commit and push the fix, and run it again |
| before the tag (push or CI) | fix it, build and package again, and commit the fix with `Release X.Y.Z` as its first line too: `publish` only accepts a `HEAD` with that message |
| after the tag was pushed, with no release | run `publish` again, with the release commit still at `HEAD`: it creates only the release |
| the release exists but a file is missing | run `publish` again: it attaches only what is missing, from `dist/`. Don't rebuild first: a new zip may not match the file or checksum already attached |
| a local tag points to another commit (an earlier try) | `publish` refuses before pushing anything: delete that tag with `git tag -d vX.Y.Z` and run it again |
| the tag on the remote is on another commit | `publish` refuses. Don't move a published tag: fix it and release the next patch |
| after the release is public | don't move the tag: fix it and release the next patch |

Pre-releases (`-rc1` and the like) are not supported by `release.py`.

## ksp-moddev itself

It has no `release.py`. Follow the same order by hand: `## Unreleased` until the version is closed; the tests
(`docker compose build` and `docker compose run --rm moddev moddev-selftest`); the person chooses the number; one commit
`Release X.Y.Z` that turns the section into `## X.Y.Z (YYYY-MM-DD)` and updates the places that state the current
version; approval; then push, CI, tag `vX.Y.Z`, release, and the same verification.
The image workflow ignores pushes that only change Markdown, so a release commit like this may start no CI run: start it
with `gh workflow run docker-build.yml --ref main` and wait for it before the tag.

## What has been tried

- **From this template**, with a throwaway mod built against KSP 1.12.5, a local remote and a stand-in for the `gh`
  command: `status`, `prepare` (with its refusals and the undo), the package, `validate.py`, and `publish` with and
  without `--yes`, including a tag with no release, a release without its `.sha256`, a release that already exists, a tag
  on another commit and an old local tag.
- **A real Gitea:** `publish --yes` ran once, on a copy of another mod against a throwaway repo, with an earlier version
  of `release.py` that did not wait for CI. The release came out with the zip, a matching `.sha256` and the notes. The CI
  wait, added later, was tried on its own: against a real successful run, and with simulated answers for failure, pending
  and no status.
- **Not tried:** `publish` against the real GitHub; on a real Gitea, the check for an existing release and the upload of
  a missing file (only simulated answers); a complete `publish --yes` with a CI wait on either real host; and the by-hand
  procedure for ksp-moddev, in this written form.
