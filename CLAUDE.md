# Tel-Mod-Library

## After changing a mod

1. Build it in Release, which deploys it to the game: `dotnet build "mods/<Mod>/<Mod>.csproj" -c Release`.
2. Run its unit tests in `tests/<Mod>.Tests`.
3. Run its in-game tests: `python tests/InGameTests.TelMods/run.py --scope "<Mod>"`. That runs every
   in-game test of the mod, from every suite, in one game boot; no list of suites is needed.
   `tests/InGameTests.TelMods/README.md` explains the in-game tests.
