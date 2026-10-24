# ModMenus modding guide

ModMenus adds a Mod Settings menu that other mods can put their settings in. To add yours, include `JSON/Mod Menu/modmenu.json` in your mod.

The file is an array of UI elements, shown in order:

```json
[
	{
		"type": "text",
		"labelID": "MYMOD__MODMENU__EXPLAIN"
	},
	{
		"type": "slider",
		"varID": "MyMod_Count",
		"labelID": "MYMOD__MODMENU__COUNT",
		"minValue": 1,
		"maxValue": 25,
		"defaultValue": 5
	}
]
```

Supported element types are text, sliders, checkboxes, dropdowns, text fields, secrets and keys. Each one's `labelID` is the ID of a text entry defined in `JSON/Constants/constants.json`.

## How your settings appear

- Each enabled mod with a `modmenu.json` gets its own tab in the column on the left of the menu. Clicking the tab shows that mod's settings, under its name.
- The tab shows your mod's `title` from `info.json` (or the text it names in `constants.json`). Tabs are in alphabetical order of these names.
- A name too long for its tab is shown on two lines, ending in "…". The name at the top of your settings is always shown in full.
- Apply saves every mod's settings, including those on tabs the player never opened. Cancel throws away every change.
- Every tab has a "Use in all saves" box under its name, above your rows. While it's ticked, Apply also saves the tab's settings for all the player's saves and new games (see [How settings are stored](#how-settings-are-stored)).
- Reset to Defaults sets the settings on the shown tab back to their defaults. Nothing is saved until the player presses Apply, and Cancel throws the defaults away. Rows with `"reset": false` keep their value, and so do secrets. Reset doesn't untick "Use in all saves", so on a tab where it's ticked, Apply saves the defaults for all saves.
- The menu fits the player's screen. A mod that makes the game's popups narrower than about 600 units cuts off the right end of sliders.
- Keys (`"type": "keybind"`) are not on your tab. Every mod's keys are on one tab, Hotkeys, first in the column, under each mod's name; the game's own keys come first, under "Idol Manager". A text row just above a key goes with it, so it can head your keys. If all your rows are keys, your mod has no tab of its own. Reset to Defaults on the Hotkeys tab resets every mod's keys.

## type: text

```json
{
	"type": "text",
	"labelID": "CUSTOMAUDITION__MODMENU__EXPLAIN"
}
```

- **labelID**: ID for text defined in `JSON/Constants/constants.json`

## type: slider

```json
{
	"type": "slider",
	"varID": "CustomAudition_Count",
	"labelID": "CUSTOMAUDITION__MODMENU__COUNT",
	"minValue": 1,
	"maxValue": 25,
	"defaultValue": 5
}
```

Slider elements output an integer as a string to an in-game variable.

- **varID**: ID for the in-game variable storing the value
- **labelID**: ID for text defined in `JSON/Constants/constants.json`
- **minValue** and **maxValue**: Optional integers. Default range: 0 to 100
- **defaultValue**: Optional integer. Default: midpoint of range
- **step**: Optional whole number of at least 1. The slider saves only `minValue`, `minValue + step`, `minValue + 2 × step` and so on, and `maxValue`, so `maxValue` can be picked even when the range isn't a whole number of steps. A `defaultValue` or saved value between steps moves to the nearest step. Default: 1

With `"minValue": 0, "maxValue": 100, "step": 25` the slider saves 0, 25, 50, 75 or 100.

## type: checkbox

```json
{
	"type": "checkbox",
	"varID": "AuditionAgeLimit_TogglePopup",
	"labelID": "AUDITIONAGELIMIT__MODMENU__TOGGLE",
	"defaultValue": false
}
```

Checkbox elements output `"1"` or `"0"` as a string to an in-game variable, representing true or false.

- **varID**: ID for the in-game variable storing the value
- **labelID**: ID for text defined in `JSON/Constants/constants.json`
- **defaultValue**: Optional boolean. Default: false

## type: dropdown

```json
{
	"type": "dropdown",
	"varID": "AuditionAgeLimit_TogglePopup",
	"labelID": "AUDITIONAGELIMIT__MODMENU__TOGGLE",
	"itemIDList": ["YES", "NO"],
	"defaultValue": 0
}
```

Dropdown elements output an index (0, 1, 2, etc.) as a string to an in-game variable, corresponding to the selected item's position.

- **varID**: ID for the in-game variable storing the value
- **labelID**: ID for text defined in `JSON/Constants/constants.json`
- **itemIDList**: Required array of strings. Each string is the ID for text defined in `JSON/Constants/constants.json`
- **defaultValue**: Optional integer. Default: 0

## type: input

```json
{
	"type": "input",
	"varID": "MyMod_AgencyMotto",
	"labelID": "MYMOD__MODMENU__MOTTO",
	"defaultValue": "Shine bright",
	"maxLength": 30
}
```

Text field elements let the player type free text, and output it to an in-game variable exactly as typed. It can be empty.

- **varID**: ID for the in-game variable storing the value
- **labelID**: ID for text defined in `JSON/Constants/constants.json`
- **defaultValue**: Optional string. Default: empty
- **maxLength**: Optional integer, the most characters the player can type. Default: no limit
- **inputType**: Optional. `"integer"` lets the player type only a whole number, and `"decimal"` only a number with a decimal point. Without it, the field takes any text.
- **minValue** and **maxValue**: Optional numbers, for a field with `inputType`. Each one works without the other.

A field with `inputType`:

- When the player leaves the field, a number outside `minValue` and `maxValue` moves to the nearest limit, and a decimal in an `"integer"` field is rounded. An empty field goes back to the last number it held. Apply checks the field the same way, even if the player is still typing in it.
- Its value is saved as plain digits (`"42"`, `"-3"`, `"0.5"`), like a slider's.
- Its default is `defaultValue`, checked the same way. Without a `defaultValue`, it is 0, or the nearest limit if 0 is outside them.

```json
{
	"type": "input",
	"varID": "MyMod_Seats",
	"labelID": "MYMOD__MODMENU__SEATS",
	"inputType": "integer",
	"minValue": 1,
	"maxValue": 5000,
	"defaultValue": "500"
}
```

## type: secret

```json
{
	"type": "secret",
	"secretID": "My Mod/apikey",
	"labelID": "MYMOD__MODMENU__APIKEY"
}
```

Secret elements are for API keys, passwords and other secrets. The field shows asterisks and is write-only: it's empty every time the menu opens, and a saved secret is never shown again.

- **secretID**: Where the secret is kept: a name, or a folder and a name separated by `/` (`"My Mod/apikey"`). Each part has only letters, digits, spaces, `-`, `_` and `.`, and doesn't start or end with a space or a dot. The secret is saved in `%USERPROFILE%\AppData\LocalLow\Glitch Pitch\Idol Manager\ModConfig\<secretID>.key`, here `ModConfig\My Mod\apikey.key`. Rows in several mods can use the same folder, or the same secret.
- **labelID**: ID for text defined in `JSON/Constants/constants.json`
- A secret row has no `varID`, `defaultValue` or `maxLength`. The options for every row (`ignore`, `enabledIf`) work as usual.

What the player sees:

- A grey hint in the empty field: "Paste your key", or "Paste a new key to replace it" once one is saved.
- A line under the field. It says "No key saved", or "Saved key ending in ...x7Qa" for a secret of 16 characters or more. A shorter secret, such as a PIN, shows only "A key is saved", because its last characters would give most of it away.
- A Remove button beside the field while a secret is saved. Pressing it changes it to Undo, and the line says the key is removed when the player presses Apply. Cancel or Undo keeps the secret.

What Apply does:

- Text typed in the field replaces the saved secret. Spaces at either end are dropped.
- An empty field keeps the saved secret.
- After Remove, Apply deletes the file. Typing a new secret after Remove cancels the removal, and Apply saves the new one.

How secrets are kept:

- A secret is never in a save, in the game's variables, or in the "Use in all saves" file, and Steam Cloud doesn't sync the `ModConfig` folder. So one secret works for every save on that PC, whether "Use in all saves" is ticked or not. A tab with a secret row says so under its "Use in all saves" box.
- The file is plain text, not encrypted. Tell players where it is, and suggest a key with a spending limit where the service offers one.
- Reset to Defaults leaves secrets alone, and `MenuValue` never returns them.

Read a secret in your mod with `ModMenusUtils.GetSecret(secretID)`. It returns the secret, or null when none is saved. The file is read again only when it changes, so call `GetSecret` whenever you need the secret instead of keeping a copy. `ModMenusUtils.HasSecret(secretID)` says whether one is saved.

To act when the player changes a secret, such as testing a new key, add a handler to `ModMenusUtils.SecretsChanged`, an `Action<string[]>` event. After an Apply that changed any secret, once every other setting is saved, it's raised with the IDs of the secrets that changed. A handler that throws is logged, and the other handlers still run.

```csharp
/// <summary>ModMenus' secrets, found by name so the mod also runs without ModMenus.</summary>
private static void ListenForKeyChanges()
{
    EventInfo changed = AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType("ModMenus.ModMenusUtils"))
        .FirstOrDefault(type => type != null)?.GetEvent("SecretsChanged");
    changed?.AddEventHandler(null, new Action<string[]>(ids =>
    {
        if (ids.Contains("My Mod/apikey"))
            TestTheKey();
    }));
}
```

Add the handler once, when the game starts (for example in a `mainScript.Start` postfix), when every mod is loaded. Without ModMenus, your mod can read the same file itself: it's UTF-8 text holding only the secret. If your mod references ModMenus' DLL, use `ModMenus.ModMenusUtils.GetSecret` and `SecretsChanged` directly instead.

## type: keybind

```json
{
	"type": "keybind",
	"varID": "MyMod_JumpKey",
	"labelID": "MYMOD__MODMENU__JUMP",
	"defaultValue": "J"
}
```

Key elements let the player pick a key for your mod. The row shows the key in a box. Clicking the box waits for a key: the next key pressed, with any Ctrl, Shift or Alt held, becomes the row's key. Right, middle and extra mouse buttons count as keys; a left click anywhere else stops the wait. The × beside the box leaves the row with no key.

- **varID**: ID for the in-game variable storing the key
- **labelID**: ID for text defined in `JSON/Constants/constants.json`
- **defaultValue**: Optional. The key, as a Unity `KeyCode` name (`"J"`, `"Alpha4"` for the 4 above the letters, `"F5"`, `"Mouse2"` for the middle button), after any of `Ctrl+`, `Shift+` and `Alt+` (`"Ctrl+J"`). Default: no key

Read the key in your mod with `ModMenusUtils.KeybindPressed(varID)` (pressed this frame), `KeybindHeld(varID)` (held down) or `KeybindReleased(varID)` (let go this frame), instead of `Input.GetKeyDown` and the others:

- They read the player's key, or the row's default if none is saved, so they work on the main menu too.
- A key with modifiers is pressed only with exactly those modifiers held. A key without modifiers isn't pressed while the modifiers held make it another row's key: with Ctrl+F and F both used, Ctrl+F isn't also F.
- They return false while the Mod Settings menu is open, and until the key that was just picked is let go, so the key pressed to pick a row doesn't also do what it's for.
- They return false if no enabled mod has a key row with that varID. `ModMenusUtils.HasKeybind(varID)` says whether one does, so your mod can use its own key without ModMenus:

```csharp
/// <summary>ModMenus' key rows, found by name so the mod also runs without ModMenus.</summary>
private static bool JumpPressed()
{
    Type utils = AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType("ModMenus.ModMenusUtils"))
        .FirstOrDefault(type => type != null);
    MethodInfo has = utils?.GetMethod("HasKeybind");
    MethodInfo pressed = utils?.GetMethod("KeybindPressed");
    if (has != null && pressed != null && (bool)has.Invoke(null, new object[] { "MyMod_JumpKey" }))
        return (bool)pressed.Invoke(null, new object[] { "MyMod_JumpKey" });
    return Input.GetKeyDown(KeyCode.J);
}
```

Look the methods up once and keep them, as this runs every frame. If your mod references ModMenus' DLL, call `ModMenus.ModMenusUtils.KeybindPressed` directly instead.

A key that another shown key row also has is marked on both rows, in red under the box, with the other rows' names. Your keys count as working on the agency screen: they're marked against the game's keys that work there too, but not against keys that only work elsewhere, such as in a dialogue. The player can still apply keys used twice.

The key is saved by its name, as in `defaultValue`: `"Ctrl+J"`, or `"None"` for no key.

## Options for every row

These go on any element, alongside its `type`:

- **ignore**: Optional boolean. When true, the element is left out of the menu. Useful for testing. Default: false
- **enabledIf**: Optional varID of another setting. The row is shown only while that setting is on (see below).
- **reset**: Optional boolean. When false, Reset to Defaults leaves the row as it is. Use it for rows the player wouldn't want reset, such as a typed name. Default: true

### Showing a setting only when another is on (`enabledIf`)

```json
[
	{
		"type": "checkbox",
		"varID": "MyMod_Custom",
		"labelID": "MYMOD__MODMENU__CUSTOM"
	},
	{
		"type": "slider",
		"varID": "MyMod_CustomCount",
		"labelID": "MYMOD__MODMENU__COUNT",
		"enabledIf": "MyMod_Custom"
	}
]
```

The slider is shown only while the checkbox is ticked. When a row is hidden, the rows under it move up.

- A setting is on when its value isn't empty and isn't 0. So a checkbox is on when ticked, a slider when above 0, a dropdown on any item but the first, and a text field when it has text in it.
- The menu uses the value shown, even before the player presses Apply.
- `enabledIf` can name a setting on another mod's tab. If no row in the menu has that varID, ModMenus reads it with `variables.Get`, so your mod can show a row from its own code by setting that variable, or by working it out (see below).
- A row that depends on a hidden row is hidden too, whatever the hidden row's value.
- Rows whose settings lead back to themselves (A depends on B, and B on A) are always shown, and a warning is logged.
- A hidden row keeps its value, and Apply still saves it. Its default is saved when a game starts, like every other setting's. Your mod decides what a hidden setting means: for example, it can ignore the count while the checkbox is unticked.

### Showing a setting for a value your mod works out (`MenuValue`)

`enabledIf` only checks whether a setting is on. To show a row for anything else, such as one item of a dropdown, give `enabledIf` a varID that no row has, and answer it in a Harmony postfix on `variables.Get`. ModMenus reads it each time the player changes something in the menu. No row has that varID, so it's never saved.

To follow the player's choices before they press Apply, read them with `ModMenusUtils.MenuValue(varID)`. It returns what the open menu shows for that varID, on any tab, as Apply would save it: a checkbox's `"1"` or `"0"`, a dropdown's item counted from 0, a slider's number, a text field's text. It returns null while the menu is closed, if no row has that varID, and on ModMenus versions that don't have it; read the saved value with `variables.Get` then.

Example: an address field shown only while the dropdown is on its third item.

```json
[
	{
		"type": "dropdown",
		"varID": "MyMod_Service",
		"labelID": "MYMOD__MODMENU__SERVICE",
		"itemIDList": ["MYMOD__MODMENU__FIRST", "MYMOD__MODMENU__SECOND", "MYMOD__MODMENU__OTHER"]
	},
	{
		"type": "input",
		"varID": "MyMod_Address",
		"labelID": "MYMOD__MODMENU__ADDRESS",
		"enabledIf": "MyMod_UsesAddress"
	}
]
```

```csharp
[HarmonyPatch(typeof(variables), nameof(variables.Get))]
public class variables_Get
{
    public static void Postfix(string name, ref string __result)
    {
        if (name == "MyMod_UsesAddress")
        {
            string service = MenuValue("MyMod_Service") ?? variables.Get("MyMod_Service");
            __result = service == "2" ? "1" : "0";
        }
    }

    /// <summary>ModMenus' MenuValue, found by name so the mod also runs without ModMenus.</summary>
    private static string MenuValue(string varID)
    {
        MethodInfo method = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("ModMenus.ModMenusUtils"))
            .FirstOrDefault(type => type != null)?.GetMethod("MenuValue");
        return method?.Invoke(null, new object[] { varID }) as string;
    }
}
```

If your mod references ModMenus' DLL, call `ModMenus.ModMenusUtils.MenuValue` directly instead.

## How settings are stored

- Settings are saved in the player's save file, so each save has its own. A new game starts with none saved.
- A tab whose "Use in all saves" box is ticked also has its settings saved in `data\modmenus_settings.txt`, beside the saves in `%USERPROFILE%\AppData\LocalLow\Glitch Pitch\Idol Manager\`. Steam Cloud syncs that folder, so the settings follow the player to other computers. The file holds every row on the tab except secrets. The Hotkeys tab's box covers every mod's keys; your tab's box covers your other rows.
- When a game starts or a save loads, ModMenus gives each setting on a tab whose box is ticked the value from that file, replacing the save's. Every other setting with no saved value yet gets its default (the `defaultValue`, or the default described above for each type); other saved values are never changed. So with ModMenus installed, `variables.Get(varID)` returns your setting from the start.
- While a tab's box is ticked, no save can keep different settings for that tab. When the player unticks it, each save keeps the settings it has at that moment.
- Secrets are kept apart from every other setting, in their own files, the same for every save (see [type: secret](#type-secret)).
- If a mod is turned off, its values in the file stay there for when it's back, but they aren't put into saves.
- Without ModMenus, `variables.Get(varID)` returns null. If your mod works without ModMenus, give it a default in code that matches the `defaultValue` in your modmenu.json.
- Values are saved as plain digits whatever the player's language (`"7"`, `"-2"`), so `int.Parse` and `float.Parse` with `CultureInfo.InvariantCulture` read them. A text field's value is the text itself.
- The game's `variables.Set` turns the exact values `male_name` and `female_name` into a random name, so a text field whose text is exactly one of those saves a random name instead.
- The menu is registered as popup type 999 (`(PopupManager._type)999`), or, if another mod already registered a popup as 999, the next number no popup has. If your mod adds its own popup, use a different number.

## Mistakes in modmenu.json

ModMenus skips what it can't use and writes a warning to the BepInEx log, starting with `[ModMenus]`:

- A file that isn't valid JSON, or isn't a list (`[ ... ]`), is left out; other mods' settings still show.
- A slider whose `defaultValue` is outside its range is left out.
- A slider whose `step` isn't a whole number of at least 1 moves in steps of 1.
- A text field with `"password": true` is left out. Text fields have no password option, so the row can't show or share a secret; use a [secret](#type-secret) row.
- A secret whose `secretID` isn't a name, or a folder and a name, of the allowed characters is left out.
- A dropdown whose saved or default choice is past the end of its list shows its last item.
- A key whose `defaultValue` isn't a key starts with no key.

## Tips

- Retrieve values using `variables.Get(varID)`, where `varID` is the element's varID.
- While the menu is open, `ModMenusUtils.MenuValue(varID)` gives what it shows, applied or not (see [Showing a setting for a value your mod works out](#showing-a-setting-for-a-value-your-mod-works-out-menuvalue)).
- Implement null checks for `variables.Get(varID)` to handle cases where ModMenus is not installed, or the value was edited by hand.
- Read secrets with `GetSecret`: they're never in the variables.
- Read keys with `KeybindPressed` rather than `variables.Get`: it handles modifiers, defaults and the menu being open.
- To add vertical spacing, use a text element with an empty labelID (`""`).
- Always define text in `constants.json` for translation support. If absolutely necessary, ModMenus can handle raw text from labelID or itemIDList fields.
