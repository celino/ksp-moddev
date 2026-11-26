#!/usr/bin/env python3
"""Release procedure, the same for every mod; what differs per mod lives in tools/mod.json. See docs/releasing.md of
ksp-moddev for the rule: a version number exists only in the release commit and the tag; until then the CHANGELOG top
section is "## Unreleased".

Usage (from the repository root):
  python3 tools/release.py status
      Reads .version, the CHANGELOG and the remote tags; fails if the repo announces a version that is not tagged.
  python3 tools/release.py prepare <patch|minor|major|X.Y.Z>
      Edits the working tree for the release: .version, the CHANGELOG heading and every "replace" entry of mod.json,
      then runs tools/validate.py. Commits nothing. Needs a clean tree on the release branch, in sync with the remote.
      The number must be higher than the current one. If anything fails, including validate.py, the edits are undone.
  python3 tools/release.py publish [--yes]
      After the release commit ("Release X.Y.Z") exists and dist/<Name>-X.Y.Z.zip was built from it (the .sha256 is written if missing): pushes the
      commit, waits for CI (GitHub), tags, pushes the tag and creates the release with the zip and the CHANGELOG
      section as notes. Order matters: commit, push, CI, tag, release. Without --yes it only prints the plan.
      If the tag is already on the remote (on this commit) but the release was never created, it creates only the
      release; if the release exists without the zip or the .sha256, it attaches only the missing file.

tools/mod.json:
  name        the mod's name (zip name, tag message, release title)
  version_file path of the KSP-AVC .version file (default: the only one under GameData/)
  branch      release branch (default "master")
  remote      preferred git remote name (default: "github", else the first remote)
  host        "github" (gh CLI) or "gitea" (API; needs GITEA_TOKEN and the "gitea" block below)
  gitea       {"api": "https://host/api/v1", "repo": "owner/name"}  (only for host "gitea")
  ci          false to skip waiting for CI before the tag (default: wait; on Gitea only if .gitea/workflows exists)
  replace     list of {"file", "regex"}: regex has two groups around the version, e.g. "(Mod-)\\d+\\.\\d+\\.\\d+(\\.zip)"
              the first match in the file is rewritten; no match is an error
"""
import datetime
import glob
import json
import re
import subprocess
import sys
import time

CFG = json.load(open("tools/mod.json", encoding="utf-8"))
NAME = CFG["name"]
BRANCH = CFG.get("branch", "master")


def run(*cmd, check=True, stdin=None):
    r = subprocess.run(cmd, capture_output=True, text=True, input=stdin)
    if check and r.returncode:
        sys.exit(f"{' '.join(cmd)}\n{r.stderr.strip() or r.stdout.strip()}")
    return r.stdout.strip()


def remote():
    names = run("git", "remote").split()
    want = CFG.get("remote", "github")
    if want in names:
        return want
    return names[0] if names else sys.exit("no git remote")


def version_file():
    if CFG.get("version_file"):
        return CFG["version_file"]
    found = glob.glob("GameData/**/*.version", recursive=True)
    if len(found) != 1:
        sys.exit(f"expected one .version file under GameData/, found {len(found)}: set \"version_file\" in tools/mod.json")
    return found[0]


def read_version():
    v = json.load(open(version_file(), encoding="utf-8"))["VERSION"]
    return "%d.%d.%d" % (v["MAJOR"], v["MINOR"], v["PATCH"])


def remote_tags():
    out = run("git", "ls-remote", "--tags", remote())
    return {m.group(1) for m in re.finditer(r"refs/tags/(v[\w.\-]+)$", out, re.M)}


def split_changelog(text):
    m = re.search(r"^## (.+)$", text, re.M)
    u = re.search(r"^## Unreleased[^\n]*\n(.*?)(?=^## |\Z)", text, re.M | re.S)
    return (m.group(1) if m else ""), (u.group(1).strip() if u else None)


def status():
    version = read_version()
    tags = remote_tags()
    top, pending = split_changelog(open("CHANGELOG.md", encoding="utf-8").read())
    print(f".version        {version}")
    print(f"remote tags     {', '.join(sorted(tags)) or '(none)'}")
    print(f"CHANGELOG top   ## {top}")
    print(f"pending entries {'yes' if pending else 'none'}")
    if version != "0.0.0" and f"v{version}" not in tags:
        print(f"PROBLEM: .version says {version} but tag v{version} is not on the remote: finish the release "
              f"(publish) or undo the number.")
        return 1
    print("ok: the announced version is tagged" if version != "0.0.0" else "ok: never released (0.0.0)")
    return 0


def prepare(arg):
    if run("git", "status", "--porcelain"):
        sys.exit("working tree is not clean")
    if run("git", "rev-parse", "--abbrev-ref", "HEAD") != BRANCH:
        sys.exit(f"not on {BRANCH}")
    rem = remote()
    run("git", "fetch", rem, BRANCH, "--tags")
    if run("git", "rev-parse", "HEAD") != run("git", "rev-parse", f"{rem}/{BRANCH}"):
        sys.exit(f"HEAD is not {rem}/{BRANCH}: pull or push first")

    old = read_version()
    major, minor, patch = (int(x) for x in old.split("."))
    if arg == "patch":
        new = f"{major}.{minor}.{patch + 1}"
    elif arg == "minor":
        new = f"{major}.{minor + 1}.0"
    elif arg == "major":
        new = f"{major + 1}.0.0"
    elif re.fullmatch(r"\d+\.\d+\.\d+", arg):
        new = arg
    else:
        sys.exit("give patch, minor, major or X.Y.Z")
    if tuple(int(x) for x in new.split(".")) <= (major, minor, patch):
        sys.exit(f"{new} is not higher than the current version {old}")
    if f"v{new}" in remote_tags():
        sys.exit(f"tag v{new} already exists on the remote")

    text = open("CHANGELOG.md", encoding="utf-8").read()
    if not split_changelog(text)[1]:
        sys.exit("CHANGELOG.md has no '## Unreleased' section with entries: nothing to release")
    today = datetime.date.today().isoformat()

    touched = ["CHANGELOG.md", version_file()] + [r["file"] for r in CFG.get("replace", [])]
    backup = {f: open(f, encoding="utf-8").read() for f in dict.fromkeys(touched)}
    try:
        edit_files(new, old, text, today)
        print(f"{old} -> {new} ({today})")
        print(run("git", "diff", "--stat"))
        out = subprocess.run([sys.executable, "tools/validate.py"], capture_output=True, text=True)
        print(out.stdout.strip() or out.stderr.strip())
        if out.returncode:
            raise SystemExit("validate.py failed")
    except BaseException as e:
        for f, content in backup.items():
            open(f, "w", encoding="utf-8").write(content)
        sys.exit(f"prepare failed, edits undone: {e}" if str(e) else "prepare failed, edits undone")
    print(f"\nNext: build and package from this tree, play it, review `git diff`, get the owner's OK, then commit as "
          f"'Release {new}' and run: python3 tools/release.py publish --yes")


def edit_files(new, old, text, today):
    open("CHANGELOG.md", "w", encoding="utf-8").write(
        re.sub(r"^## Unreleased[^\n]*$", f"## {new} ({today})", text, count=1, flags=re.M))

    vf = version_file()
    src = open(vf, encoding="utf-8").read()
    maj, mnr, pat = new.split(".")
    src2 = re.sub(r'("VERSION"\s*:\s*\{\s*"MAJOR":\s*)\d+(,\s*"MINOR":\s*)\d+(,\s*"PATCH":\s*)\d+',
                  rf"\g<1>{maj}\g<2>{mnr}\g<3>{pat}", src, count=1)
    if src2 == src:
        sys.exit(f"could not rewrite VERSION in {vf}")
    open(vf, "w", encoding="utf-8").write(src2)

    for rule in CFG.get("replace", []):
        body = open(rule["file"], encoding="utf-8").read()
        body2, n = re.subn(rule["regex"], rf"\g<1>{new}\g<2>", body, count=1)
        if not n:
            sys.exit(f"mod.json: no match for {rule['regex']!r} in {rule['file']}")
        open(rule["file"], "w", encoding="utf-8").write(body2)


def wait_ci(sha):
    for _ in range(12):   # the run can take a few seconds to appear
        rid = run("gh", "run", "list", "--commit", sha, "--limit", "1", "--json", "databaseId", "--jq",
                  ".[0].databaseId // empty", check=False)
        if rid:
            run("gh", "run", "watch", rid, "--exit-status")
            return
        time.sleep(5)
    sys.exit("no CI run found for the release commit; check by hand, then tag")


def wait_ci_gitea(sha, timeout=1200):
    """Waits for the combined commit status of Gitea Actions to be success; fails on failure or error."""
    import os
    import urllib.request
    g = CFG["gitea"]
    token = os.environ.get("GITEA_TOKEN") or sys.exit("set GITEA_TOKEN")
    url = f"{g['api']}/repos/{g['repo']}/commits/{sha}/status"
    start, seen = time.time(), False
    while time.time() - start < timeout:
        req = urllib.request.Request(url, headers={"Authorization": f"token {token}"})
        d = json.load(urllib.request.urlopen(req))
        if d.get("total_count", 0):
            seen = True
            if d["state"] == "success":
                print(f"CI ok ({d['total_count']} checks)")
                return
            if d["state"] in ("failure", "error"):
                bad = [x["context"] for x in d["statuses"] if x["status"] in ("failure", "error")]
                sys.exit(f"CI failed on {sha[:8]}: {', '.join(bad)}. Fix it, commit again as a new Release commit.")
        elif time.time() - start > 90 and not seen:
            sys.exit(f"no CI status for {sha[:8]} after 90 s (is the runner up?). Set \"ci\": false in mod.json to skip.")
        time.sleep(10)
    sys.exit(f"CI still running after {timeout // 60} min: check by hand, then tag")


def gitea_release(tag, notes):
    """Creates the release (no files) and returns its id."""
    import os
    import urllib.request
    g = CFG["gitea"]
    token = os.environ.get("GITEA_TOKEN") or sys.exit("set GITEA_TOKEN")
    req = urllib.request.Request(
        f"{g['api']}/repos/{g['repo']}/releases",
        data=json.dumps({"tag_name": tag, "name": f"{NAME} {tag[1:]}", "body": notes}).encode(),
        headers={"Authorization": f"token {token}", "Content-Type": "application/json"})
    return json.load(urllib.request.urlopen(req))["id"]


def release_state(tag):
    """(exists, names of the files attached, id). The id is only known on Gitea."""
    if CFG.get("host", "github") == "github":
        r = subprocess.run(["gh", "release", "view", tag, "--json", "assets", "--jq", ".assets[].name"],
                           capture_output=True, text=True)
        return (r.returncode == 0, set(r.stdout.split()) if r.returncode == 0 else set(), None)
    import os
    import urllib.error
    import urllib.request
    g = CFG["gitea"]
    token = os.environ.get("GITEA_TOKEN") or sys.exit("set GITEA_TOKEN")
    req = urllib.request.Request(f"{g['api']}/repos/{g['repo']}/releases/tags/{tag}", headers={"Authorization": f"token {token}"})
    try:
        d = json.load(urllib.request.urlopen(req))
    except urllib.error.HTTPError as e:
        if e.code == 404:
            return (False, set(), None)
        raise
    return (True, {a["name"] for a in d.get("assets", [])}, d["id"])


def upload_files(tag, files, rid):
    """Attaches files to an existing release."""
    if CFG.get("host", "github") == "github":
        run("gh", "release", "upload", tag, *files)
        return
    import os
    g = CFG["gitea"]
    token = os.environ["GITEA_TOKEN"]
    for f in files:
        run("curl", "-sS", "--fail", "-K", "-", "-F", f"attachment=@{f}",
            f"{g['api']}/repos/{g['repo']}/releases/{rid}/assets?name={f.split('/')[-1]}",
            stdin=f'header = "Authorization: token {token}"\n')


def create_release(tag, zipf, notes):
    if CFG.get("host", "github") == "github":
        print(run("gh", "release", "create", tag, zipf, zipf + ".sha256", "--title", f"{NAME} {tag[1:]}", "--notes", notes))
    else:
        rid = gitea_release(tag, notes)
        upload_files(tag, [zipf, zipf + ".sha256"], rid)
        print(f"release {tag} created")


def publish(yes):
    version = read_version()
    tag = f"v{version}"
    rem = remote()
    if run("git", "status", "--porcelain"):
        sys.exit("working tree is not clean: commit the release first")
    if run("git", "log", "-1", "--format=%s") != f"Release {version}":
        sys.exit(f"HEAD is not the 'Release {version}' commit")
    tag_pushed = tag in remote_tags()
    missing, rid = None, None   # files still to attach when the release exists but is incomplete
    zipf = f"dist/{NAME}-{version}.zip"
    wanted = {zipf.split("/")[-1], zipf.split("/")[-1] + ".sha256"}
    if tag_pushed:   # a half-done release: the tag is out, the release was never created or lacks files
        peeled = run("git", "ls-remote", rem, f"refs/tags/{tag}^{{}}") or run("git", "ls-remote", rem, f"refs/tags/{tag}")
        if peeled.split()[0] != run("git", "rev-parse", "HEAD"):
            sys.exit(f"{tag} is on the remote but not on this commit: do not move a published tag; fix and release the next patch")
        exists, have, rid = release_state(tag)
        if exists and wanted <= have:
            sys.exit(f"{tag} is already released")
        if exists:
            missing = sorted(wanted - have)
            print(f"{tag} is released but lacks {', '.join(missing)}: attaching only those")
        else:
            print(f"{tag} is on the remote without a release: creating only the release")
    elif run("git", "tag", "-l", tag) and run("git", "rev-parse", f"{tag}^{{commit}}") != run("git", "rev-parse", "HEAD"):
        sys.exit(f"local tag {tag} points to another commit (an earlier try): delete it with `git tag -d {tag}` and run again")
    if not glob.glob(zipf):
        sys.exit(f"missing {zipf}: build and package first")
    if not glob.glob(zipf + ".sha256"):   # not every package.sh writes it; the format is that of `sha256sum`
        import hashlib
        digest = hashlib.sha256(open(zipf, "rb").read()).hexdigest()
        open(zipf + ".sha256", "w").write(f"{digest}  {zipf.split('/')[-1]}\n")
    if missing:
        print(f"attach {', '.join(missing)} to release {tag} ({CFG.get('host', 'github')})")
    elif tag_pushed:
        print(f"create release {tag} ({CFG.get('host', 'github')}) with {zipf} and its .sha256, notes from CHANGELOG")
    else:
        print(f"git push {rem} {BRANCH}\nwait for CI{' (skipped: ci=false)' if CFG.get('ci', True) is False else ''}\ngit tag -a {tag}\ngit push {rem} {tag}\n"
              f"create release {tag} ({CFG.get('host', 'github')}) with {zipf} and its .sha256, notes from CHANGELOG")
    if not yes:
        print("\n(plan only: add --yes to run it)")
        return
    if missing:
        return upload_files(tag, [f"dist/{n}" for n in missing], rid)
    notes = run(sys.executable, "tools/release-notes.py", version)
    if not tag_pushed:
        run("git", "push", rem, BRANCH)
        sha = run("git", "rev-parse", "HEAD")
        if CFG.get("ci", True) is not False:
            if CFG.get("host", "github") == "github":
                wait_ci(sha)
            elif glob.glob(".gitea/workflows/*.y*ml"):
                wait_ci_gitea(sha)
            else:
                print("no .gitea/workflows: not waiting for CI")
        if not run("git", "tag", "-l", tag):   # a rerun after a failed tag push keeps the local tag
            run("git", "tag", "-a", tag, "-m", f"{NAME} {version}")
        run("git", "push", rem, tag)
    create_release(tag, zipf, notes)


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    if cmd == "status":
        sys.exit(status())
    elif cmd == "prepare" and len(sys.argv) == 3:
        prepare(sys.argv[2])
    elif cmd == "publish":
        publish("--yes" in sys.argv)
    else:
        sys.exit(__doc__)
