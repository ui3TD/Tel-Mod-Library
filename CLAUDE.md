# Tel-Mod-Library

## After changing a mod

1. Build it in Release, which deploys it to the game: `dotnet build "mods/<Mod>/<Mod>.csproj" -c Release`.
2. Run its unit tests in `tests/<Mod>.Tests`.
3. Run its in-game tests: `python tests/InGameTests.TelMods/run.py --scope "<Mod>"`. That runs every
   in-game test of the mod, from every suite, in one game boot; no list of suites is needed.
   `tests/InGameTests.TelMods/README.md` explains the in-game tests.
   Skip this step when the change can't reach what the mod's in-game tests check. For AI Fan Chatter,
   changes to the prompt's wording or facts (`Moment.cs`, `Members.cs`, `History.cs`, `AgencyRules.cs`,
   `Prompt.cs`, `PromptTemplate.cs`, `PostParser.cs`, the texts in `constants.json`) are covered by the
   unit tests and `python tools/ai_fan_chatter_live_report.py --dry-run`; run the in-game tests when its
   patches, settings, key store, transport, feed queue or save code change.
4. Run `python tools/check_repo.py` (changelog line, description, info.json values).
