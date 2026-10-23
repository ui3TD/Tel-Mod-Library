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

Supported element types are text, sliders, checkboxes, dropdowns and text fields. Each one's `labelID` is the ID of a text entry defined in `JSON/Constants/constants.json`.

## How your settings appear

- Each enabled mod with a `modmenu.json` gets its own tab in the column on the left of the menu. Clicking the tab shows that mod's settings, under its name.
- The tab shows your mod's `title` from `info.json` (or the text it names in `constants.json`). Tabs are in alphabetical order of these names.
- A name too long for its tab is shown on two lines, ending in "…". The name at the top of your settings is always shown in full.
- Apply saves every mod's settings, including those on tabs the player never opened. Cancel throws away every change.
- Reset to Defaults sets the settings on the shown tab back to their defaults. Nothing is saved until the player presses Apply, and Cancel throws the defaults away. Rows with `"reset": false` keep their value.
- The menu fits the player's screen. A mod that makes the game's popups narrower than about 600 units cuts off the right end of sliders.

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
- **password**: Optional boolean. When true, the field shows asterisks instead of what's typed, for API keys and other secrets. The value is still saved exactly as typed, in the save file, so a mod that must keep a secret out of saves has to move it elsewhere itself. Default: false
- **inputType**: Optional. `"integer"` lets the player type only a whole number, and `"decimal"` only a number with a decimal point. Without it, the field takes any text.
- **minValue** and **maxValue**: Optional numbers, for a field with `inputType`. Each one works without the other.

A field with `inputType`:

- When the player leaves the field, a number outside `minValue` and `maxValue` moves to the nearest limit, and a decimal in an `"integer"` field is rounded. An empty field goes back to the last number it held. Apply checks the field the same way, even if the player is still typing in it.
- Its value is saved as plain digits (`"42"`, `"-3"`, `"0.5"`), like a slider's.
- Its default is `defaultValue`, checked the same way. Without a `defaultValue`, it is 0, or the nearest limit if 0 is outside them.
- The `password` option is ignored, because the field can only show the number.

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

## Options for every row

These go on any element, alongside its `type`:

- **ignore**: Optional boolean. When true, the element is left out of the menu. Useful for testing. Default: false
- **enabledIf**: Optional varID of another setting. The row is shown only while that setting is on (see below).
- **reset**: Optional boolean. When false, Reset to Defaults leaves the row as it is. Use it for rows the player wouldn't want reset, such as a typed name, or a key your mod removes when the field is applied empty. Default: true

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
- When a game starts or a save loads, ModMenus saves the default of every setting that has no saved value yet (the `defaultValue`, or the default described above for each type). Saved values are never changed. So with ModMenus installed, `variables.Get(varID)` returns your setting from the start.
- Without ModMenus, `variables.Get(varID)` returns null. If your mod works without ModMenus, give it a default in code that matches the `defaultValue` in your modmenu.json.
- Values are saved as plain digits whatever the player's language (`"7"`, `"-2"`), so `int.Parse` and `float.Parse` with `CultureInfo.InvariantCulture` read them. A text field's value is the text itself.
- The game's `variables.Set` turns the exact values `male_name` and `female_name` into a random name, so a text field whose text is exactly one of those saves a random name instead.
- The menu is registered as popup type 999 (`(PopupManager._type)999`). If your mod adds its own popup, use a different number.

## Mistakes in modmenu.json

ModMenus skips what it can't use and writes a warning to the BepInEx log, starting with `[ModMenus]`:

- A file that isn't valid JSON, or isn't a list (`[ ... ]`), is left out; other mods' settings still show.
- A slider whose `defaultValue` is outside its range is left out.
- A slider whose `step` isn't a whole number of at least 1 moves in steps of 1.
- A text field with both `inputType` and `password` ignores `password`.
- A dropdown whose saved or default choice is past the end of its list shows its last item.

## Tips

- Retrieve values using `variables.Get(varID)`, where `varID` is the element's varID.
- While the menu is open, `ModMenusUtils.MenuValue(varID)` gives what it shows, applied or not (see [Showing a setting for a value your mod works out](#showing-a-setting-for-a-value-your-mod-works-out-menuvalue)).
- Implement null checks for `variables.Get(varID)` to handle cases where ModMenus is not installed, or the value was edited by hand.
- To add vertical spacing, use a text element with an empty labelID (`""`).
- Always define text in `constants.json` for translation support. If absolutely necessary, ModMenus can handle raw text from labelID or itemIDList fields.
