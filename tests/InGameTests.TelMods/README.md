# InGameTests.TelMods

Checks of the Tel mods that run inside the real game, on top of the
[IM-InGameTests](https://github.com/ui3TD/IM-InGameTests) runner. The unit tests in the other
`tests\` projects can't reach the things these cover: the Unity lifecycle, the game's real data,
and every mod loaded together.

The runner is kept free of references to these mods, so the Tel checks live here as a separate
assembly. The runner loads `InGameTests.*.dll` from its plugins folder in test mode only.

## Running

IM-InGameTests must be checked out next to this repo, or set `IM_INGAMETESTS` to its folder.
Steam must be running.

```
python tests/InGameTests.TelMods/run.py                # smoke suite, with the Tel checks
python tests/InGameTests.TelMods/run.py --build-mods   # build and deploy the installed Tel mods first
python tests/InGameTests.TelMods/run.py --weeks 12 -v  # any run_ingame_tests.py option works
```

`run.py` runs `run_ingame_tests.py --build tests\InGameTests.TelMods\InGameTests.TelMods.csproj`, passing
every other argument through. The build deploys the checks to `<game>\BepInEx\plugins\InGameTests\`.

The mods are only built with `--build-mods`, which builds every mod that has a local copy in the
game's Mods folder, in Release, and so deploys it. Without it, build a changed mod in Release
yourself before running.

## Tiers

| Tier | Suite | What | Cost |
|---|---|---|---|
| 0 | `smoke` | Generic checks of every enabled Tel mod, right after the save loads | about 1 s |
| 1, 2 | `mods` (planned) | Per-mod checks of values and UI on the fixture save | |

### Tier 0: checks of every mod (`AllMods/TelModSmokeTests.cs`)

| Test | Fails when |
|---|---|
| `LoadedBuildsMatchCheckout` | A loaded mod's version differs from its csproj, its DLL isn't byte-identical to a build in this checkout's `bin`, or a deployed asset is missing or different. The other results would then be about other code. |
| `EveryPatchMethodIsApplied` | A Prefix, Postfix, Transpiler or Finalizer declared in a mod isn't applied under its HarmonyID. This also catches mod types that fail to load: Harmony skips their patch classes, and the loader only logs it. |
| `EveryTranspilerChangesIL` | A Tel transpiler leaves its method's IL unchanged. A transpiler whose IL search finds nothing usually returns the code untouched, and the mod then does nothing, with no error. |

Mods that aren't installed or enabled are listed in a note and skipped.

`LoadedBuildsMatchCheckout` can't see edits made since a mod's last build. File times don't work for
that: git rewrites files whose content hasn't changed, and a rebuild after a line-ending change isn't
byte-identical. Use `--build-mods`, or build the mod yourself.

## Writing checks

Tests are static coroutines marked `[InGameTest(Suite = ..., Order = ...)]`; see the runner's README.
Checks that apply the same rule to every mod go in `AllMods/`. Checks of one mod go in
`Mods/<Mod>Tests.cs`, one file per mod, and skip themselves when that mod isn't enabled.
`TelMod.All()` reads each `mods\*\*.csproj` at run time. The path of this checkout is built into the
DLL, so rebuild after moving the repo. `TelMod.Active()` returns the mods whose patches are live.
