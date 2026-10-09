# Tel Mod Library

Library of mods for Idol Manager

<details>
<summary><h2 style="display: inline-block;">Mods Included (click to expand)</h2></summary>

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

1. **Configure Directories:** Set directories of `ModOutputDirDebug`, `ModOutputDirRelease` and `dllDir` in `Directory.Build.props`

2. **Get Pre-requisites (if Visual Studio did not do it automatically):** 
   - .NET Framework 4.6: [https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies.net46](https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies.net46)
   - UnityEngine 2019.4.23 libraries: [https://nuget.bepinex.dev/packages/unityengine.modules/2019.4.23](https://nuget.bepinex.dev/packages/unityengine.modules/2019.4.23)

3. **Build**


Check out [IM-FastForward](https://github.com/ui3TD/IM-FastForward) for a tutorial for beginners.

## Publishing to Steam Workshop

`tools/publish_workshop.py` builds a mod and updates its Workshop item in one step: content, title, full description (`assets/steam description.txt`) and change note (the changelog line for the csproj `<Version>`). The item is the csproj's `<WorkshopID>`. It uploads a fresh Release build from a clean staging folder, not the game's Mods folder, and refuses to publish until the mod's changes are committed and pushed.

1. One-time: install [SteamCMD](https://developer.valvesoftware.com/wiki/SteamCMD) to `C:\steamcmd` (or set `STEAMCMD`) and run `steamcmd +login <user> +quit` to approve Steam Guard.
2. Bump `<Version>` and add a `- <version>: ...` line under `[h3]Changelog[/h3]` in the steam description.
3. Run `python tools/publish_workshop.py "<Mod Name>" --user <user>` (or set `STEAM_USER`). Add `--dry-run` to preview the upload.
