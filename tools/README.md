# tools

Scripts for working on ksp-moddev itself (not copied into the image).

| Script | What it does |
|---|---|
| `test-templates.sh [/path/to/KSP]` | tests `templates/mod-repo` and `templates/test-driver` end to end in a temporary folder: makes ToyMod, runs `validate.py`, `build.sh`, `dotnet build`, and plays the test-driver's `run.sh` against a fake KSP. Ends with `templates: N/N ok`. |

Run it before a release, on your computer and in the image:

```sh
tools/test-templates.sh /path/to/KSP
docker compose run --rm -T -v "$PWD:/src:ro" moddev /src/tools/test-templates.sh /ksp
```

Your computer has `mcs` and no `dotnet`, or the other way round in the image: each run skips what it cannot do and says
so. Run both to cover everything. Without the KSP libraries only the steps that need them are skipped. It refuses to
start while a real KSP is running (the fake one has the same process name). It needs the KSP libraries, so it does not
run in the public CI.
