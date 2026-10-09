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

Supported element types are text, sliders, checkboxes and dropdowns. Each one's `labelID` is the ID of a text entry defined in `JSON/Constants/constants.json`.

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

## How settings are stored

- Settings are saved in the player's save file, so each save has its own. A new game starts with none saved.
- When a game starts or a save loads, ModMenus saves the default of every setting that has no saved value yet (the `defaultValue`, or the default described above for each type). Saved values are never changed. So with ModMenus installed, `variables.Get(varID)` returns your setting from the start.
- Without ModMenus, `variables.Get(varID)` returns null. If your mod works without ModMenus, give it a default in code that matches the `defaultValue` in your modmenu.json.
- Values are saved as plain digits whatever the player's language (`"7"`, `"-2"`), so `int.Parse` and `float.Parse` with `CultureInfo.InvariantCulture` read them.
- The menu is registered as popup type 999 (`(PopupManager._type)999`). If your mod adds its own popup, use a different number.

## Mistakes in modmenu.json

ModMenus skips what it can't use and writes a warning to the BepInEx log, starting with `[ModMenus]`:

- A file that isn't valid JSON, or isn't a list (`[ ... ]`), is left out; other mods' settings still show.
- A slider whose `defaultValue` is outside its range is left out.
- A dropdown whose saved or default choice is past the end of its list shows its last item.

## Tips

- Retrieve values using `variables.Get(varID)`, where `varID` is the element's varID.
- Implement null checks for `variables.Get(varID)` to handle cases where ModMenus is not installed, or the value was edited by hand.
- To add vertical spacing, use a text element with an empty labelID (`""`).
- Always define text in `constants.json` for translation support. If absolutely necessary, ModMenus can handle raw text from labelID or itemIDList fields.
- For testing, add `"ignore": true` to any element to exclude it from parsing.
