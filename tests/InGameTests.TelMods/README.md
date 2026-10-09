# InGameTests.TelMods

Checks of the Tel mods that run inside the real game, on top of the
[IM-InGameTests](https://github.com/ui3TD/IM-InGameTests) runner. The unit tests in the other
`tests\` projects can't reach the things these cover: the Unity lifecycle, the game's real data,
and every mod loaded together.

The runner is kept free of references to these mods, so the Tel checks live here as a separate
assembly. The runner loads `InGameTests.*.dll` from its plugins folder in test mode only.

Only checks that need to know which mods are Tel's, or where this checkout is, belong here. Checks and
helpers that work for any mod go in the runner: its smoke suite already checks every enabled Harmony
mod's patches, transpilers and texts, and its `Game` and `TestTools` classes hold the game helpers these
checks use.

## Running

IM-InGameTests must be checked out next to this repo, or set `IM_INGAMETESTS` to its folder.
Steam must be running.

```
python tests/InGameTests.TelMods/run.py                # smoke suite, with the Tel checks
python tests/InGameTests.TelMods/run.py --suite mods   # per-mod checks, about 20 s
python tests/InGameTests.TelMods/run.py --build-mods   # build and deploy the installed Tel mods first
python tests/InGameTests.TelMods/run.py --weeks 12 -v  # any run_ingame_tests.py option works
```

`run.py` builds this project, which deploys the checks to `<game>\BepInEx\plugins\InGameTests\`, then
runs `run_ingame_tests.py` with every other argument. The runner tests whatever is deployed there.

The mods are only built with `--build-mods`, which builds every mod that has a local copy in the
game's Mods folder, in Release, and so deploys it. Without it, build a changed mod in Release
yourself before running.

## Tiers

| Tier | Suite | What | Cost |
|---|---|---|---|
| 0 | `smoke` | The loaded Tel mods are this checkout's builds, right after the save loads | about 1 s |
| 1, 2 | `mods` | Per-mod checks of values and UI on the fixture save | about 2 s |

### Tier 0: the loaded builds (`AllMods/TelModSmokeTests.cs`)

| Test | Fails when |
|---|---|
| `LoadedBuildsMatchCheckout` | A loaded mod's version differs from its csproj, its DLL isn't byte-identical to a build in this checkout's `bin`, or a deployed asset is missing or different. The other results would then be about other code. |

Mods that aren't installed or enabled are listed in a note and skipped. The rest of tier 0 runs in the
runner's own smoke suite: `EveryPatchMethodIsApplied`, `EveryTranspilerChangesIL` and
`EveryModTextIsLoaded`. `run.py` passes `scope.txt` with `--scope-file`, which limits those checks to
the Tel mods (`^com\.tel\.`). Other installed mods stay loaded and are listed in a note instead of checked.

`LoadedBuildsMatchCheckout` can't see edits made since a mod's last build. File times don't work for
that: git rewrites files whose content hasn't changed, and a rebuild after a line-ending change isn't
byte-identical. Use `--build-mods`, or build the mod yourself.

### Tiers 1 and 2: per-mod checks (`mods` suite)

A check belongs here only if the unit tests can't make it. That means it needs at least one of these:
- the game's real scene and prefabs, which the unit tests fake;
- the real loaded data: every installed mod's JSON, the fixture save, the real `GenerateGirl`;
- several mods patching the same method together.

Behaviour the unit tests already run on the real patched game methods stays out, for example Stale
Theater's attendance or Concert Rebalance's forecast.

| File | Fails when |
|---|---|
| `ModMenusTests` | Opening Settings doesn't give exactly one Mod Settings button right after Settings, or a second opening adds another. Clicking it doesn't open a menu with a row for every setting in the enabled mods' `modmenu.json`. Apply doesn't save the value. |
| `PoliciesThatMatterTests` | After every mod's policy file loads, a policy type lacks one header and one selected choice, or lists a choice twice. A real training tick under Quality doesn't cost 0.5/7 mental stamina a day. Quality doesn't halve the trainee's training time. |
| `EffortlessTrainingTests` | A real training tick doesn't cost 1 physical stamina a day, times the game's Quality and Moonlighter factors. |
| `FanAttritionTests` | The game's fan tooltip doesn't render exactly the 15 lines the mod fills by position, ending with the churn line. |
| `MbtiPersonalitiesTests` | An idol's Extras tab doesn't name her type. An idol in the save carries two types. A newly generated idol from an idol pack with a type doesn't end up with exactly that type. |
| `StarSignsTests` | An idol's Extras tab doesn't end with her star sign, after the other mods' lines. |
| `NationalTourTests` | A replacement picture didn't load, or the World Tour tab, the new tour map or the tour results don't show it. A country marker isn't on its prefecture. |
| `TourStaminaLimitTests` | A tour stop's new fans aren't the game's figure times 3.5, and times 1.2 per Polyglot idol with Traits Expansion on. |
| `WorkerRightsTests` | A newly generated idol's salary isn't 20,000. |

Each check skips itself with a note when its mod isn't enabled. Where another mod changes the same value,
the expected value includes that mod's factor only when it's enabled, so `--only <Mod>` runs one mod's
checks on their own. Checks put back what they change, apart from using up idol IDs. The session is never saved.

Two runner helpers do most of the work:
- `TestTools.Spy` patches a method ahead of every other patch, so it sees the caller's arguments or the
  game's own result.
- `Game.TrainingTickAddParams` records the stamina charges of one real training tick.

## Writing checks

Tests are static coroutines marked `[InGameTest(Suite = ..., Order = ...)]`; see the runner's README.
Checks of every Tel mod against this checkout go in `AllMods/`. Checks of one mod go in
`Mods/<Mod>Tests.cs`, one file per mod, and skip themselves when that mod isn't enabled (`ModTest.Require`).
Keep mod-specific code out of `ModTest.cs`, so changing or retiring a mod touches only its own file. Reach
mod types with `Assembly.GetType` and HarmonyLib's `AccessTools` and `Traverse`.
`TelMod.All()` reads each `mods\*\*.csproj` at run time. The path of this checkout is built into the
DLL, so rebuild after moving the repo. `TelMod.Active()` returns the mods whose patches are live.
