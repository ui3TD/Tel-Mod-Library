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

## Code comments

Comments say what is true, never why the code is designed as it is:

- **Allowed:** what the code does, or its inputs and outputs. How the game, the engine or other mods behave,
  stated as a fact: "The game calls this twice per frame."
- **Not allowed:** a reason, a purpose, a rejected alternative or a defended risk. Examples from this
  repo: "on real time so a paused game still pulses", "in case another mod calls this inside another
  election". Delete such a sentence or rewrite it as a fact; a reason that matters goes in the commit
  message.

Single words don't decide it: "otherwise the game's value" describes what the code returns.

Old comments are cleaned up one mod at a time. When working on a mod, also rewrite that mod's existing
reason comments, in a separate comment-only commit. A comment-only change gets no version bump and no
changelog line; it ships with the mod's next release.

## Steam changelog

Each version bump adds `- <version>: <statement>` (at most 100 characters after `- `) as the first line
under `[h3]Changelog[/h3]` in `assets/steam description.txt`. The changelog is newest first and keeps
only the 5 newest versions plus every major version (1.0.0, 2.0.0, ...; 0.x versions are not major);
drop the line that falls out. `tools/check_repo.py` enforces this.

## FastForward template

The IM-FastForward repo (the beginner template, next to this repo) holds the same FastForward mod. After
changing `mods/FastForward`, `tests/FastForward.Tests`, `tests/Common/PatchTargetAssert.cs`,
`Mod.BeforeSdk.targets`, `.editorconfig`, `.gitattributes`, `NuGet.Config` or the build settings in either
`Directory.Build.props`:

1. Run `python tools/sync_fastforward_template.py --apply`. It copies those files to the template
   (`source/` there holds the mod) and lists build-setting differences in the props files to fix by hand.
2. Commit in both repos. Push IM-FastForward before this repo: CI compares this repo with the pushed
   template.
3. Update by hand what the script doesn't compare: the template's in-game test
   (`tests/InGameTests.FastForward`, same steps as `tests/InGameTests.TelMods/Mods/FastForwardTests.cs`), its
   single-mod `tools/` and its README.

## Shared code

`shared/<Name>/` is compiled into every mod that imports its `.projitems`; each mod ships its own copy.
`shared/AICore/` is the AI service code (providers, key store, request limits, transport) of Tel's AI
mods, currently AI Fan Chatter. Each AI mod fills in `AIMod` (name, settings prefix, schema name) in a
file of its own. After changing `shared/AICore/`, run `tests/AICore.Tests`, then the steps above for
every AI mod; the key store and transport are covered by AI Fan Chatter's in-game tests.
`shared/UiCopies/` copies a game UI object without the parts other mods added to it, keeping only the
parts in a `VanillaShape` read from the game with no mods loaded; ModMenus and National Tour use it. Its
unit tests are in `tests/NationalTour.Tests` (`UiCopyTests`); after changing it, run the steps above
for both mods.
