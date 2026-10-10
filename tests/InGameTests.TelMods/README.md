# InGameTests.TelMods

Checks of the Tel mods that run inside the real game, on top of the
[IM-InGameTests](https://github.com/ui3TD/IM-InGameTests) runner. The unit tests in the other
`tests\` projects can't reach the things these cover: the Unity lifecycle, the game's real data,
and every mod loaded together.

The runner is kept free of references to these mods, so the Tel checks live here as a separate
assembly. The runner loads `InGameTests.*.dll` from its plugins folder in test mode only.

Only checks that need to know which mods are Tel's, or where this checkout is, belong here. Checks and
helpers that work for any mod go in the runner: its smoke suite already checks every enabled Harmony
mod's patches and transpilers, and its `Game` and `TestTools` classes hold the game helpers these
checks use.

## Running

IM-InGameTests must be checked out next to this repo, or set `IM_INGAMETESTS` to its folder.
Steam must be running.

```
python tests/InGameTests.TelMods/run.py                # smoke suite, with the Tel checks
python tests/InGameTests.TelMods/run.py --suite mods   # per-mod checks, about 20 s
python tests/InGameTests.TelMods/run.py --scope "<Mod>"  # after changing a mod: everything that tests it
python tests/InGameTests.TelMods/run.py --build-mods   # build and deploy the installed Tel mods first
python tests/InGameTests.TelMods/run.py --weeks 12 -v  # any run_ingame_tests.py option works
```

`run.py` builds this project, which deploys the checks to `<game>\BepInEx\plugins\InGameTests\`, then
runs `run_ingame_tests.py` with every other argument. The runner tests whatever is deployed there.

**After changing a mod, run `run.py --scope "<Mod>"`.** The runner then picks every test of that
mod from every suite (the `affected` suite), plus the checks that judge every mod, limited to that
one, in one boot. Nothing else needs to know which suites a mod's tests are in.

Without `--scope` or `--scope-list`, `run.py` passes `scope.txt` (the Tel mods, `^com\.tel\.`) and
the suite defaults to `smoke`. The runner's README, "Choosing what runs", explains `--load`,
`--scope` and `--suite`.

The mods are only built with `--build-mods`, which builds every mod that has a local copy in the
game's Mods folder, in Release, and so deploys it. Without it, build a changed mod in Release
yourself before running.

## Tiers

| Tier | Suite | What | Cost |
|---|---|---|---|
| 0 | `smoke` | The loaded Tel mods are this checkout's builds, right after the save loads | about 1 s |
| 1, 2 | `mods` | Per-mod checks of values and UI on the fixture save | about 2 s |
| 3 | `auditions`, `elections`, `clock` | High-effort scenarios, run when a mod they test changes (`run.py --scope "<Mod>"`) | 5 to 20 s each |

### Tier 0: the loaded builds (`AllMods/TelModSmokeTests.cs`)

| Test | Fails when |
|---|---|
| `LoadedBuildsMatchCheckout` | A loaded mod's version differs from its csproj, its DLL isn't byte-identical to a build in this checkout's `bin`, or a deployed asset is missing or different. The other results would then be about other code. |
| `EveryTestClassNamesATelMod` | A class here with tests has no `[ModUnderTest]`, or names a HarmonyID no mod project in this checkout has. |

Mods that aren't installed, enabled or in scope are listed in a note and skipped. The rest of tier 0 runs in the
runner's own smoke suite: `EveryPatchMethodIsApplied` and `EveryTranspilerChangesIL`. All of these judge
every mod in scope, which by default is the Tel mods. Other installed mods stay loaded and are listed in a
note instead of checked.

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
| `ModMenusTests` | Opening Settings doesn't give exactly one Mod Settings button right after Settings, or a second opening adds another. Clicking it doesn't open a menu with a row for every setting in the enabled mods' `modmenu.json`. Apply doesn't save the value. A text field, copied from the rival name popup, keeps a tie to that popup, isn't what a click on it hits, draws outside its row, or doesn't take typing up to its limit, save it on Apply and drop it on Cancel. |
| `PoliciesThatMatterTests` | After every mod's policy file loads, a policy type lacks one header and one selected choice, or lists a choice twice. A real training tick under Quality doesn't cost 0.5/7 mental stamina a day. Quality doesn't halve the trainee's training time. |
| `EffortlessTrainingTests` | A real training tick doesn't cost 1 physical stamina a day, times the game's Quality and Moonlighter factors. |
| `FanAttritionTests` | The game's fan tooltip doesn't render exactly the 15 lines the mod fills by position, ending with the churn line. |
| `MbtiPersonalitiesTests` | An idol's Extras tab doesn't name her type. An idol in the save carries two types. A newly generated idol from an idol pack with a type doesn't end up with exactly that type. |
| `StarSignsTests` | An idol's Extras tab doesn't end with her star sign, after the other mods' lines. |
| `NationalTourTests` | A replacement picture didn't load, or the World Tour tab, the new tour map or the tour results don't show it. A country marker isn't on its prefecture. |
| `TourStaminaLimitTests` | A tour stop's new fans aren't the game's figure times 3.5, and times 1.2 per Polyglot idol with Traits Expansion on. |
| `WorkerRightsTests` | A newly generated idol's salary isn't 20,000. |

Each check skips itself with a note when its mod isn't enabled. Where another mod changes the same value,
the expected value includes that mod's factor only when it's enabled, so `--load <Mod>` runs one mod's
checks on their own. Checks put back what they change, apart from using up idol IDs. The session is never saved.

Two helpers do most of the work:
- `TestTools.Spy`, from the runner, patches a method ahead of every other patch, so it sees the caller's
  arguments or the game's own result.
- `Training.TickAddParams`, in `Shared/`, records the stamina charges of one real training tick.

### Tier 3: scenarios (`auditions`, `elections`, `clock` suites)

Each runs a whole game scenario: an audition, an election's results, or four weeks of the clock. The
same rule applies as for tiers 1 and 2, and each asserts only what the unit tests can't. `run.py --scope
"<Mod>"` runs them along with the mod's other checks; `--suite <name>` runs one scenario for every mod in
scope.

| File | Suite | Fails when |
|---|---|---|
| `TargetedAuditionsTests` | `auditions` | A 16-candidate nationwide audition doesn't give 16 cards that are all revealed and clickable, inside exactly one scroll area. A body repeats before the pool of bodies the game can pick from runs out, or a unique idol's body appears twice. With vocal prioritised, vocal isn't the best rolled skill of at least 10 of 16 candidates, read as the stat roll leaves them (so Unofficial Patch's reroll in the same method is included). |
| `UnofficialPatchTests` | `auditions` | With every portrait job started 3 s late, no card unlocks showing the placeholder; a late portrait doesn't reach the opened card and the stats panel; or closing the audition while portraits are still to come logs an error or leaves the queue or a render running. |
| `ExtendedSskTests` | `elections` | A new election, started as the concert's end starts it, doesn't rank every idol who can take part up to 64 (15 on the fixture, past vanilla's 10), or its results popup doesn't award each place's fame once and finish. |
| `FastForwardTests` | `clock` | Two clicks on the real fast button don't give 28x (5600 minutes a second) with a gold label, or four weeks at that speed miss or repeat a day's or a Monday's event. |

Left to the unit tests: Targeted Auditions' settings, body pool rules, ages, orientation and priority
maths alone; Unofficial Patch's unlock timing, which jobs a close drops, the late-render cap and the 60 s
stop; Extended SSK's fame values per rank and broadcast, its limit parsing and the wish cap; FastForward's
clamping, parsing, third click and hotkey.

These suites leave the game changed for the rest of the boot (an election can't be undone), so each test
starts by closing every popup and doesn't rely on what ran before it.

## Writing checks

Tests are static coroutines marked `[InGameTest(Suite = ..., Order = ...)]`; see the runner's README.
Checks of every Tel mod against this checkout go in `AllMods/`, in a class marked
`[ModUnderTest(ModUnderTestAttribute.EveryMod)]`. Checks of one mod go in `Mods/<Mod>Tests.cs`, one file
and one class per mod, marked `[ModUnderTest(HarmonyId)]` with the same `HarmonyId` const the checks pass
to `ModTest.Require`, which skips them when that mod isn't enabled. A check never sets or relies on
another mod; other mods are only loaded around it.

Each test picks its own suite, so one mod's file can hold a cheap `mods` check and a high-effort
scenario. Name suites after the scenario (the constants in `ModTest.cs`), never after a mod.
Keep mod-specific code out of `ModTest.cs` and `Shared/`, so changing or retiring a mod touches only its
own file. Reach mod types with `Assembly.GetType` and HarmonyLib's `AccessTools` and `Traverse`.
Game helpers that several checks share go in `Shared/` unless they pass the runner's rules for helpers
(its README, "Adding a helper to the runner"); then they belong in the runner.
`TelMod.All()` reads each `mods\*\*.csproj` at run time. The path of this checkout is built into the
DLL, so rebuild after moving the repo. `TelMod.Active()` returns the mods whose patches are live.
