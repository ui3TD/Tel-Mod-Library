# Tel-Mod-Library

## After changing a mod

1. Build it in Release, which deploys it to the game: `dotnet build "mods/<Mod>/<Mod>.csproj" -c Release`.
2. Run its unit tests in `tests/<Mod>.Tests`.
3. Run its in-game tests: `python tests/InGameTests.TelMods/run.py --scope "<Mod>"`. That runs every
   in-game test of the mod, from every suite, in one game boot; no list of suites is needed.
   `tests/InGameTests.TelMods/README.md` explains the in-game tests.
   Skip this step when the change can't reach what the mod's in-game tests check. For AI Fan Chatter,
   changes to the prompt's wording or facts (the files in `Moments/` and `Prompt/`, the texts in
   `constants.json`) are covered by the unit tests and `python tools/ai_fan_chatter_live_report.py
   --dry-run`; run the in-game tests when its patches (`Patches/`), settings, key store, transport, feed
   (`Feed/`) or save code change.
4. Run `python tools/check_repo.py` (changelog line, description, info.json values).

## Steam changelog

Each version bump adds `- <version>: <statement>` (at most 100 characters after `- `) as the first line
under `[h3]Changelog[/h3]` in `assets/steam description.txt`. The changelog is newest first and keeps
only the 5 newest versions plus every major version (1.0.0, 2.0.0, ...; 0.x versions are not major);
drop the line that falls out. `tools/check_repo.py` enforces this.

## Shared code

`shared/<Name>/` is compiled into every mod that imports its `.projitems`; each mod ships its own copy.
`shared/AICore/` is the AI service code (providers, key store, request limits, transport) of Tel's AI
mods, currently AI Fan Chatter. Each AI mod fills in `AIMod` (name, settings prefix, schema name) in a
file of its own. After changing `shared/AICore/`, run `tests/AICore.Tests`, then the steps above for
every AI mod; the key store and transport are covered by AI Fan Chatter's in-game tests.
