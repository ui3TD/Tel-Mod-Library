# Changelog

This changelog covers changes made after the `main` branch point at commit `1c6d2aa` (2026-01-19). Entries are ordered chronologically by commit date. Multiple commits to the same mod on the same date are consolidated into the final behavior for that date rather than repeating intermediate fixes.

## 2026-01-19

### Unofficial Patch

- **`mods/Unofficial Patch/Unofficial Patch.cs`**
  - Hardened the `SEvent_Concerts._concert._projectedValues.GetRevenue` transpiler so the `GetHype` call is found whether the compiler emitted `call` or `callvirt`, then replaced with the static revenue infix without depending on one exact opcode form.
  - Added a `singles._single.GetSenbatsuParamValue` patch that invokes the private `SenbatsuCalcParam` through Harmony `AccessTools` using the requested `data_girls._paramType`; senbatsu lookups no longer fall back to effectively evaluating every requested stat as Cute.
  - Added handling for negated VN girl-variable requirements in `vn_requirements.CheckGirl`, so formulas beginning with `!` correctly invert `IsVariable(...)` instead of being interpreted as a literal variable name.
  - Added shared IL matching/logging helpers and converted brittle literal/opcode checks to named constants and guarded Harmony lookups. Failed pattern matches now fail safely and can emit once-per-patch diagnostics instead of silently producing malformed IL.

## 2026-01-20

### Unofficial Patch

- **`mods/Unofficial Patch/Unofficial Patch.cs`**
  - Reworked `Profile_Fans_Pies.Render_Pies` correction to reconstruct the Teen/Young Adult/Adult age ratios before applying cumulative fills. The resulting pie cannot overflow 100% because rounding overshoot is normalized back into the `0..1` range.
  - Changed the relationship breakup patch to capture the pre-breakup `Dating` state in a Harmony Prefix. Partner-status knowledge is now cleared only when the relationship was actually dating before vanilla `BreakUp` changed its state.
  - Updated projected concert revenue handling so club venues keep their vanilla hype treatment, while non-club venues use the intended post-100% hype curve.
  - Added the `FUJI_3_TICKETS` 5% ticket bonus to projected concert revenue calculations.
  - Corrected the projected hype/display ceiling from 100% to the vanilla 200% maximum.
  - Updated `CodeMatcher` edits to use `Set(...)` for opcode/operand replacement in the concert and partner-string patches.

### Shared build configuration

- **`Directory.Build.props`**
  - Added a fallback `dllDir` based on `MSBuildThisFileDirectory` when `SolutionDir` is unavailable, allowing project-level builds to resolve the shared DLL directory correctly.
- **`NuGet.Config`**
  - Changed the nuget.org source from HTTP to HTTPS.

## 2026-02-23

### FastForward

- **`mods/FastForward/FastForward.cs`**
  - Replaced direct `double.Parse` use with invariant-culture validation for the configured multiplier, including NaN/infinity rejection and a safe `1x..50x` range.
  - Centralized super-fast application and button-color updates so both the fast button and the `4` hotkey use the same validated speed path and tolerate missing camera/UI components.
  - Added audition-popup protection: custom speeds above vanilla fast are temporarily reduced to vanilla fast while the audition popup initializes, then restored from `mainScript.Time_Resume` only if the player has not changed time state in the meantime.
  - Added cleanup for pending speed restoration when the player leaves fast mode, preventing stale audition state from restoring an old speed later.
- **`mods/FastForward/FastForward.csproj`**
  - Version increased from **1.2.0** to **1.2.1**.

### Harmony Checker

- **`mods/Harmony Checker/Harmony Checker.cs`**
  - Added null/hierarchy guards around the main-menu Mods button lookup. Missing or renamed UI elements no longer throw during menu initialization and interfere with other mod hooks.

### Targeted Auditions

- **`mods/Targeted Auditions/Targeted Auditions.cs`**
  - Hardened audition-card scrolling setup with null checks, an explicit `ScrollRect.viewport`, reuse of an existing `ContentSizeFitter`, and safe hierarchy reparenting.
  - Added an audition portrait-load watchdog. `Popup_Audition.Set`, `Reset`, and `Close` maintain load state, and `PortraitsLoaded` can fail safe after six seconds instead of leaving the recruitment popup permanently blocked by one unresolved portrait.
  - Added fallback portrait handling and visibility restoration when the watchdog has to release a stuck audition popup.
- **`mods/Targeted Auditions/Targeted Auditions.csproj`**
  - Version increased from **2.0.1** to **2.0.2**.

### Unofficial Patch

- **`mods/Unofficial Patch/Unofficial Patch.cs`**
  - Reworked `Theaters.CompleteDay` correction so it no longer overwrites `Doing_Now` before vanilla settles the previous day. The patch now reads the stat row vanilla actually recorded, zeros day-off revenue, and distributes valid ticket/subscription revenue to participating idols from that recorded result.
  - Added null/empty guards around theaters, stat rows, groups, girl lists, and breakup relationship members.
  - Replaced the underage `data_girls.girls.GetPartnerString` IL branch rewrite with a Postfix that rebuilds the normal partner/sexuality/interested text for underage idols while leaving vanilla behavior untouched for AOC idols. This removes a fragile dependency on the compiler's branch layout.

## 2026-03-05

### ModMenus

- **`mods/ModMenus/ModMenus.cs`**
  - Reworked installation of the **Mod Settings** button so it is attempted from startup and whenever the Settings tab opens, instead of assuming the settings hierarchy already exists at one exact moment.
  - Added `ModMenusBootstrap`, which retries installation while the UI is still being constructed and stops after a bounded number of attempts.
  - Added hierarchy fallbacks for finding the settings container and a suitable button template, plus detection/repair of an already-created Mod Settings button to avoid duplicates.
  - Rebinds localized button text and the click listener when repairing an existing button, and forces layout rebuilding after insertion.

### Unofficial Patch

- **`mods/Unofficial Patch/Unofficial Patch.cs`**
  - Added null guards around both `SaveManager.LoadData(bool)` and `SaveManager.LoadData(string)` overloads.
  - The previous in-memory `SaveManager.Data` is captured before loading. If a missing/corrupt load path leaves `Data` null, the previous data (or a fresh `SavedData` fallback) is restored so subsequent `SaveEvent`/autosave calls cannot crash on a null save object.

## 2026-08-08

### Targeted Auditions

- **`mods/Targeted Auditions/Popup UI.cs`**
  - Removed the old `Staff_Nickname`-based per-audition age-entry popup and its Harmony patches.
- **`mods/Targeted Auditions/Targeted Auditions.cs`**
  - Centralized age-range loading in `LoadConfiguredAgeRange()`. Reversed minimum/maximum settings are swapped and written back in corrected order.
  - Centralized birthday creation in `ApplyRandomBirthdayInConfiguredRange(...)` instead of duplicating the generation logic at the patch site.
  - Tightened the `Popup_Audition.Set` patch to the `(Auditions.data, bool)` overload, starts the portrait watchdog before the original method runs, and clears/logs watchdog state from a Harmony Finalizer when popup setup throws.
  - Added exception logging/finalizer cleanup around `Auditions.GenerateGirls` so audition-generation failures do not leave supporting state hanging.
- **`mods/Targeted Auditions/assets/JSON/Mod Menu/modmenu.json`**
  - Removed obsolete hidden `AuditionAgeLimit_TogglePopup` controls because age limits are now taken directly from Mod Menu settings.
- **`mods/Targeted Auditions/Targeted Auditions.csproj`**
  - Removed the deleted popup source from the project and increased the version from **2.0.2** to **2.0.3**.

## 2026-08-15

### Extended SSK

- **`mods/Extended SSK/Extended SSK.cs`**
  - Replaced the cached `GetFameBaseVal` delegate, which could remain permanently bound to the first election instance, with a cached `MethodInfo` invoked against the current `SEvent_SSK._SSK` instance. Fame bonuses beyond rank 10 now use the correct election's base fame value.

### Fan Attrition

- **`mods/Fan Attrition/Fan Attrition.cs`**
  - Corrected MC-fame scaling from integer division (`/ 10`) to floating-point division (`/ 10f`), restoring the intended quadratic bonus for lower fame levels instead of truncating the coefficient.
  - Excluded Internet shows from the mod's fatigue-based audience multiplier, matching the mod description that only TV and Radio are affected by the additional fatigue penalty.
- **`mods/Fan Attrition/FanTooltip.cs`**
  - Added `cafeFans` to the weekly `TOTAL` calculation so the displayed total agrees with the individual café-fan line and the game's actual fan gain.

### Going Viral

- **`mods/Going Viral/TrendingManager.cs`**
  - Added explicit trend-duration bounds (`-90..90`) and uses the clamped stored duration in notifications. Negative trends are discarded when Fan Attrition is not installed.
  - Corrected crisis math so negative trending uses the same positive-magnitude fan/churn multiplier as positive trending rather than producing a negative coefficient that could invert fan loss into fan gain.
  - Hardened weekly fan-source collection with null guards and overflow-safe addition for contracts, shows, and the latest seven café stat rows.
  - Corrected probability arithmetic to use floating-point division where appropriate.
  - Rebalanced TV trending to a true **0% to 15%** chance: cast fame, genre level, and optional MC fame are normalized from vanilla's `0..10` scales into `0..1`, averaged into one quality score, then multiplied by the `0..1` same-genre-TV freshness coefficient. The previous `show.fame[0]` contribution was removed so MC/cast fame are not counted twice.
  - Fan-acquisition weighting now uses non-negative appeal with the documented 3x casual weighting and does not mix opinion into acquisition weights.
  - Fan-churn weighting now uses non-negative appeal, clamped opinion, stable 3x casual weighting, and the crisis multiplier on total churn rather than allowing the trend sign to distort demographic weights.
  - Added overflow-safe integer/long scaling and addition helpers used throughout the trending calculations.
- **`mods/Going Viral/ApplyTrending.cs`**
  - Reworked single trending so the multiplier applies to the **total** new fans and the resulting bonus is distributed across casual fan buckets without dividing by zero when the original casual gain is zero.
  - Replaced the show-sales compiler-local transpiler with scoped Harmony patches around `Shows._show.SetSales`, `data_girls.AddFans_Equally`, and `Shows._show.SetNewFans`. Vanilla distribution/rounding remains in place while the viral bonus is recorded and applied to casual buckets.
  - Added context cleanup through Harmony Finalizers so nested calls or exceptions cannot leak show-trending state into later calculations.
  - Hardened business and tour trending fan multipliers with null/range-safe scaling.
- **`mods/Going Viral/FanMechanics.cs`**
  - Replaced fan-distribution local-variable transpilers with scoped contexts around the game's `AddFans` methods and `GetTotalAppeal`/`GetFamePoints` calls. Only the weighting functions are substituted; vanilla allocation and rounding remain intact.
  - Added stack-based context/finalizer cleanup so nested Harmony calls and exceptions do not leave stale acquisition/churn state active.
  - Hardened theater/fan-opinion processing with null checks and bounded arithmetic.
- **`mods/Going Viral/Resource.cs`**
  - Reworked trend save persistence so tagged trend data is consumed as a private save marker instead of being allowed to overwrite vanilla Buzz.
  - Added a high reserved marker/offset that cannot collide with normal Buzz values, migration of legacy `10000 + trend` saves (including negative trends), and reset of static trend state when loading a save with no marker.
  - Added load-depth/finalizer handling so failed or nested loads cannot leave the marker parser in a stale loading state.
- **`mods/Going Viral/FanTooltip.cs`**
  - Replaced hard-coded tooltip child indices with named, created-on-demand lines for demographic ratios, fan sources, churn, and trending status. UI hierarchy changes no longer redirect text into the wrong line or cause index errors.
  - Weekly totals include café fans and use overflow-safe arithmetic.
  - Added guards for missing idols/fan-appeal data and zero total fans when rendering demographic ratios.
- **`mods/Going Viral/BonusTooltip.cs`**
  - TV genre buttons now calculate the most recent TV show separately for the genre represented by each button instead of reusing the currently selected genre for every tooltip.
  - Tooltips are rebuilt from the parameter tooltip each render so the Going Viral suffix cannot accumulate repeatedly.
  - Single marketing trend/crisis percentages use floating-point probability math and are clamped to valid display ranges.
- **`mods/Going Viral/TriggerTrending.cs`**
  - Hardened single-marketing, TV-episode, scandal-point, and resource-trigger paths with explicit pre/post trend-state tracking so a trigger is reported only when it actually creates a new trend/crisis.
  - Prevented duplicate crisis handling when Fake Scandal's resource change has already started the crisis.
- **`mods/Going Viral/assets/JSON/Constants/constants.json`**
  - Converted the constants asset to strict quoted JSON and removed the duplicate `TIP__RADIO` entry while retaining the intended localization text, including the TV description's “Up to 15%” wording.
- **`mods/Going Viral/Going Viral.csproj`**
  - Version increased from **1.0.0** to **1.0.1**.

### Growing Distant

- **`mods/Growing Distant/Growing Distant.cs`**
  - Capped salary-based positive influence at the vanilla 512 relationship-point ceiling. High salary satisfaction can no longer accumulate hidden influence above the game's normal maximum and delay later relationship decay.

### MBTI Personalities

- **`mods/MBTI Personalities/MBTI Personalities.cs`**
  - Corrected the ISTP concert-accident calculation to operate in the game's `0..100` percentage-point scale. The trait now reduces the **remaining failure chance** rather than mixing fractional and percentage units.

### ModMenus

- **`mods/ModMenus/ModMenus.cs`**
  - Corrected the fallback slider default from `max + min / 2` to the true arithmetic midpoint, `(max + min) / 2f`.

### Never Graduate

- **`mods/Never Graduate/Never Graduate.cs`**
  - Kept the existing hard stop on `data_girls.UpdateGraduationDates` and added direct blocks for `data_girls.girls.Graduation_Date_Update`, `Graduation_Announce`, and `Graduation_Announce_Confirm` so alternate callers and other mods cannot bypass the no-graduation rule.
  - If a save already contains an idol in `announced_graduation`, the direct date-update guard restores the idol's previous status before suppressing the graduation check.
  - The stronger blocking makes the mod authoritative over date-driven graduation while still leaving direct firing available.
- **`mods/Never Graduate/Never Graduate.csproj`**
  - Updated the description to reflect the stronger graduation blocking and increased the version from **1.0.0** to **1.2.0**.

### Stale Theater Shows

- **`mods/Stale Theater Shows/Stale Theater Shows.cs`**
  - Fixed the `Theaters._theater.GetNumberOfVisitors` transpiler's branch target handling. Labels from the original normal-path instruction are transferred to the injected `Ldarg_0`, ensuring branches pass through the custom attendance multiplier instead of skipping it.

### Targeted Auditions

- **`mods/Targeted Auditions/Targeted Auditions.cs`**
  - Scoped audition-specific sexuality, stat-priority, birthday, and body-ID behavior to `Auditions.GenerateGirls` using an exception-safe generation-depth context. Rival, story, unique-idol, and other non-audition girl generation are no longer modified by Targeted Auditions settings.
  - Reworked birthday generation to choose an age in the configured inclusive range and then choose a valid birthday for that exact age. Candidates can no longer land one year older than the configured maximum because of month/day offsets.
  - Body IDs now remain unique until the currently eligible audition body pool is exhausted. Only then is `Auditions.UsedBodyIDs` cleared so larger candidate counts can recycle bodies as needed.
  - Replaced direct access to the game's private `data_girls_textures.textureAssets` field with `HarmonyLib.AccessTools.Field(...)`, keeping private game-state access within Harmony/reflection.
- **`mods/Targeted Auditions/assets/steam description.txt`**
  - Clarified that age limits are inclusive and only affect generated audition candidates, and documented the body-ID reuse behavior for large auditions.

### Traits Expansion

- **`mods/Traits Expansion/Traits Expansion.cs`**
  - Corrected **Sadistic** detection to count members who are active bullies (`IsBully`) rather than members who are being bullied (`IsBullied`).
  - Changed **Wooden Acting** drama rewards from a flat subtraction to the intended **50% multiplicative penalty**.
  - Changed **Quick Wit** variety rewards from a flat addition to the intended **50% multiplicative bonus**.

### Traits Fix

- **`mods/Traits Fix/Traits Fix.cs`**
  - Replaced the compiler-local-dependent **Live Fast** birthday transpiler with scoped `Birthday_Popup.DoParam`, `data_girls.girls.param.setVal`, and `Birthday_Stat.Set` patches. Actual stat loss and displayed birthday loss now stay synchronized, double only decreases, and remain within valid stat bounds.
  - Added null-safe handling to random post-peak Live Fast deterioration.
  - Reworked **Indiscreet** relationship leaks so outside relationships and idol-idol relationships are handled by the appropriate game paths, already-known relationships are not leaked every week, both sides of an idol relationship are synchronized when revealed, and dating-forbidden scandal penalties are applied consistently.
  - Hardened **Maternal**, **Precocious**, **Arrogant**, and **Forgiving** relationship adjustments with safe relationship-member extraction and corrected dynamic relationship math; Maternal/Precocious positive growth now accounts for the game's positive-value halving behavior.
  - Hardened **Meme Queen** Internet-show stat bonuses so they modify the actual parameter just added and only force show-popup recalculation when the medium changes.
  - Corrected **Meme Queen** viral-marketing odds by applying the advertised success and critical-success bonuses without separately subtracting from regular failure chance, which vanilla already derives from the other probability buckets.
  - Hardened **Annoying** show stamina penalties and **Misandry** handshake appeal penalties against null/inactive/sick casts and avoids unnecessary per-idol checks when the relevant show/single condition is absent.
  - Fixed **Perfectionist** tour handling by capturing the tour attendance result before vanilla `FinishTour` clears the active tour object; concert handling was likewise made null-safe.
  - Replaced global trait-calculation booleans with stack-based contexts and Harmony Finalizers around business/show/single/concert stat calculations, preventing nested calls or exceptions from leaving trait modifiers active for unrelated `param.GetVal` calls.
  - Hardened recent-single/chart lookup used by result-dependent traits so current-month player singles can be resolved from chart data without null/index failures.
- **`mods/Traits Fix/Traits Fix.csproj`**
  - Version increased from **1.1.0** to **1.1.1**.

### Worker Rights

- **`mods/Worker Rights/Worker Rights.cs`**
  - Fixed the low-salary graduation penalty by assigning the immutable `DateTime.AddDays(...)` return value back to `Graduation_Date`.
  - Applies the full advertised 10x penalty on the game's existing weekly `Graduation_Date_Update` cadence: **-10 days** below 50% salary satisfaction and **-30 days** below 20%.
  - Added Harmony ordering compatibility with Never Graduate so Never Graduate can remain authoritative when both mods are installed.
  - Low-fame expected salary is now a floor (`max(vanilla, ¥40,000)`) instead of overwriting a larger vanilla/third-party expectation.
  - In Hard mode, Fame 10 idols' 10%-of-average-earnings rule is likewise enforced as a floor and ignores NaN/infinite/non-positive earnings.
  - Added null/policy guards around staff firing, salary rules, generated idols, and graduation-date processing.
- **`mods/Worker Rights/Worker Rights.csproj`**
  - Version increased from **1.0.0** to **1.0.1**.

### ModMenus
- **`mods/ModMenus/ModMenus.cs`**
  - Fixed ModMenus button injection to detect when the setting tab is selected, reducing lag.

### Policies That Matter
- **`mods/Policies That Matter/Policies That Matter.cs`**
  - Added function that removes duplicate policy definitions after the game has loaded all policy JSON files

## Harmony Checker
- **`mods/Harmony Checker/Harmony Checker.cs`**
  - Keeps the existing `MainMenu_Buttons_Controller.Start` check.
  - Adds a post-mod-load refresh via `Mods.StopSpinner`, which runs after `Mods.LoadMods` in the vanilla mod-loading coroutine.
  - Calls `Lang_Button.ResetText()` after changing the constant so an already-started button visibly updates to `Mods [IM-HI installed`.

### Shared StatLimits library

- **`shared/StatLimits/StatLimits.cs`**
  - Standardized numeric caps through `Mathf.Clamp` while preserving the existing limits: business proposal coefficients remain `0..20`, and show/senbatsu/concert/team-chemistry stats remain `0..100`.
  - Added null/empty guards for senbatsu results and cast-parameter lists so the limit patches do not throw when a game/mod call returns no parameter object.
  - Removed `LINQ Last()` dependencies in cast-parameter patches and accesses the final element only after confirming the list is non-empty.

## 2026-08-16

### ModMenus

- **`mods/ModMenus/ModMenus.cs`**
  - Changed **Mod Settings** placement to use the vanilla `Settings` GameObject as its explicit ordering anchor, keeping the lookup independent of the button's localized display text.
  - Changed the vanilla `Settings` button to be the Mod Settings button's clone template as well as its sole ordering anchor.
  - Existing `ModMenuButton` instances are now reconfigured and repositioned immediately after the vanilla Settings button whenever the Settings tab is activated, repairing their ordering if another UI modification previously moved them.
  - Removed Save & Quit/Main Menu and `childCount`-relative placement from Mod Settings ordering, leaving later positions available for other mods to insert their own settings buttons independently.
- **`mods/ModMenus/ModMenus.csproj`**
  - Version increased from **1.0.1** to **1.0.2**.