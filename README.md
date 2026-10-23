# Tel Mod Library

Library of mods for Idol Manager

<details>
<summary><h2 style="display: inline-block;">Mods Included (click to expand)</h2></summary>

* AI Fan Chatter
* Concert Rebalance
* Effortless Training
* Extended SSK
* Fan Attrition
* FastForward
* Going Viral (work in progress - buggy)
* Growing Distant
* IM-HarmonyIntegration Plugin
* JSON Load Order
* MBTI Personalities
* Menu Hotkeys
* ModMenus
* More Sister Groups
* National Tour
* Never Graduate
* Policies That Matter
* Stale Theater Shows
* Star Signs
* Targeted Auditions
* Tour Stamina Limit
* Traits Expansion
* Traits Fix
* Unofficial Patch
* Worker Rights
</details>


## Build Instructions

To compile this project, follow these steps:

1. **Configure Directories:** Put the game's DLLs in a `dll` folder next to this repo (`dllDir` in `Directory.Build.props`). Builds deploy to the game's Mods folder (`ModOutputDirDebug`, `ModOutputDirRelease`); pass `-p:ModOutputDir=<folder>` to deploy elsewhere, or `-p:DeployToGame=false` to build without deploying.

2. **Get Pre-requisites (if Visual Studio did not do it automatically):** 
   - .NET Framework 4.6: [https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies.net46](https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies.net46)
   - UnityEngine 2019.4.23 libraries: [https://nuget.bepinex.dev/packages/unityengine.modules/2019.4.23](https://nuget.bepinex.dev/packages/unityengine.modules/2019.4.23)

3. **Build**

4. **Check:** `python tools/check_repo.py` checks changelogs, descriptions and `info.json` values (CI runs it on every push).


Check out [IM-FastForward](https://github.com/ui3TD/IM-FastForward) for a tutorial for beginners.

## Publishing to Steam Workshop

`tools/publish_workshop.py` builds a mod and updates its Workshop item in one step: content, title, full description (`assets/steam description.txt`) and change note (the changelog line for the csproj `<Version>`). The item is the csproj's `<WorkshopID>`. It uploads a fresh Release build from a clean staging folder, not the game's Mods folder, and refuses to publish until the mod's changes are committed and pushed.

1. One-time: install [SteamCMD](https://developer.valvesoftware.com/wiki/SteamCMD) to `C:\steamcmd` (or set `STEAMCMD`) and run `steamcmd +login <user> +quit` to approve Steam Guard.
2. Bump `<Version>` and add a `- <version>: ...` line under `[h3]Changelog[/h3]` in the steam description. The changelog lists the newest version first and keeps only the 5 newest versions plus every major version (1.0.0, 2.0.0 and so on), so remove the oldest line that is no longer one of those. `tools/check_repo.py` checks this.
3. Run `python tools/publish_workshop.py "<Mod Name>" --user <user>` (or set `STEAM_USER`). Add `--dry-run` to preview the upload.

To change only the Workshop page text, commit and push the edited `assets/steam description.txt` and run with `--description-only`: it updates the title and description without a build, a new version or a change note.

### Workshop tags

A mod's tags are set in its csproj, as `<Tags>["gameplay"]</Tags>`. SteamCMD can't send tags, so `publish_workshop.py` leaves the tags on Steam as they are and tells you when they differ from the csproj.

- `python tools/workshop_tags.py` compares every mod's csproj tags with the tags on Steam. It changes nothing.
- `python tools/workshop_tags.py --apply "<Mod Name>"` sets that mod's tags on Steam. `--apply-all` does this for every mod whose tags differ. Add `--dry-run` to see what would be sent.

`--apply` starts the game on the main menu with no mods and no save (through [IM-InGameTests](https://github.com/ui3TD/IM-InGameTests) probe sessions), and sets the tags from inside the game, which is logged in to Steam. Only the tags change: the title, description, files, preview, visibility and version on Steam stay as they are. It refuses a mod whose csproj isn't committed and pushed. Unlisted and private items can't be read from the public Steam API, so `--apply-all` skips them; name them with `--apply` and check the item's page afterwards.
